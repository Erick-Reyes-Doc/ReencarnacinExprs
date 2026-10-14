using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Primera interfaz funcional de combate (provisional).
// Muestra el turno activo, las cuatro habilidades del loadout y las
// unidades por posicion, y permite elegir habilidad y objetivo con el
// mouse. No contiene reglas de combate: todas las acciones se ejecutan
// con AbilitySystem y los turnos avanzan con TurnManager.
//
// La jerarquia se genera con BuildUI() (en el editor, para guardarla en
// la escena, o en tiempo de ejecucion si falta). Usa uGUI con la fuente
// integrada porque TextMeshPro Essentials no esta importado.
public class CombatHudController : MonoBehaviour
{
    private const int SlotCount = 8;   // 4 aliados + 4 enemigos
    private const int AbilitySlots = 4;

    [Header("Referencias de combate")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private PositionManager positionManager;

    [Header("Interfaz generada por BuildUI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Text roundText;
    [SerializeField] private Text activeText;
    [SerializeField] private Text infoText;
    [SerializeField] private Text messageText;
    [SerializeField] private Text logText;
    [SerializeField] private Text bannerText;
    [SerializeField] private Text prepText;
    [SerializeField] private Text detailText;
    [SerializeField] private Text hintText;
    [SerializeField] private Button[] abilityButtons = new Button[AbilitySlots];
    [SerializeField] private Text[] abilityLabels = new Text[AbilitySlots];
    [SerializeField] private Image[] abilityImages = new Image[AbilitySlots];
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button endTurnButton;

    [Header("Derrota total")]
    [Tooltip("Escena a la que se vuelve tras perder (debe estar en Build Settings).")]
    [SerializeField] private string menuSceneName = "Menu";
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private Button returnToMenuButton;
    [SerializeField] private Button[] tileButtons = new Button[SlotCount];
    [SerializeField] private Image[] tileImages = new Image[SlotCount];
    [SerializeField] private Text[] tileTexts = new Text[SlotCount];
    [SerializeField] private RectTransform[] tileBars = new RectTransform[SlotCount];

    // Colores provisionales (fantasia oscura, fondo azul grisaceo).
    private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.14f, 0.92f);
    private static readonly Color AllyColor = new Color(0.14f, 0.25f, 0.38f, 1f);
    private static readonly Color EnemyColor = new Color(0.38f, 0.16f, 0.17f, 1f);
    private static readonly Color EmptyColor = new Color(0.12f, 0.13f, 0.17f, 0.7f);
    private static readonly Color ActiveColor = new Color(0.30f, 0.45f, 0.62f, 1f);
    private static readonly Color TargetColor = new Color(0.78f, 0.62f, 0.18f, 1f);
    private static readonly Color ButtonColor = new Color(0.19f, 0.23f, 0.32f, 1f);
    private static readonly Color ButtonBlocked = new Color(0.12f, 0.13f, 0.17f, 1f);
    private static readonly Color ButtonSelected = new Color(0.45f, 0.36f, 0.12f, 1f);
    private static readonly Color TextColor = new Color(0.90f, 0.92f, 0.96f, 1f);
    private static readonly Color DimTextColor = new Color(0.55f, 0.58f, 0.66f, 1f);

    private BattleUnit[] allUnits = new BattleUnit[0];
    private BattleUnit lastActive;

    // Seleccion en curso.
    private AbilityData selectedAbility;
    private readonly List<BattleUnit> validTargets = new List<BattleUnit>();
    private AbilityData pendingConfirm;

    private readonly Queue<string> recentLog = new Queue<string>();

    // ------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------

    private void Start()
    {
        if (canvas == null)
            BuildUI();

        CacheUnits();
        WireButtons();
    }

    private void OnEnable()
    {
        Application.logMessageReceived += OnLogMessage;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= OnLogMessage;
    }

    private void Update()
    {
        Refresh();
    }

    // Vuelve a leer las unidades de la escena (tras cargar un encuentro).
    public void RefreshUnits()
    {
        CacheUnits();
    }

    private void CacheUnits()
    {
        List<BattleUnit> list = new List<BattleUnit>();

        foreach (BattleUnit unit in FindObjectsByType<BattleUnit>())
        {
            if (unit.gameObject.scene == gameObject.scene)
                list.Add(unit);
        }

        allUnits = list.ToArray();
    }

    // Conecta los botones (los listeners no se serializan en la escena).
    public void WireButtons()
    {
        for (int i = 0; i < AbilitySlots; i++)
        {
            int index = i;
            abilityButtons[i].onClick.RemoveAllListeners();
            abilityButtons[i].onClick.AddListener(
                () => OnAbilityClicked(index)
            );
        }

        for (int i = 0; i < SlotCount; i++)
        {
            int slot = i;
            tileButtons[i].onClick.RemoveAllListeners();
            tileButtons[i].onClick.AddListener(
                () => OnTileClicked(slot)
            );
        }

        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(OnCancelClicked);

        endTurnButton.onClick.RemoveAllListeners();
        endTurnButton.onClick.AddListener(OnEndTurnClicked);

        if (returnToMenuButton != null)
        {
            returnToMenuButton.onClick.RemoveAllListeners();
            returnToMenuButton.onClick.AddListener(() => ReturnToMenu());
        }
    }

    // La escena de menu existe y esta incluida en Build Settings.
    public bool CanReturnToMenu()
    {
        if (string.IsNullOrEmpty(menuSceneName))
            return false;

#if UNITY_EDITOR
        // En el editor se consulta Build Settings directamente.
        foreach (UnityEditor.EditorBuildSettingsScene scene in
                 UnityEditor.EditorBuildSettings.scenes)
        {
            if (scene.enabled &&
                System.IO.Path.GetFileNameWithoutExtension(scene.path) ==
                menuSceneName)
                return true;
        }

        return false;
#else
        return Application.CanStreamedLevelBeLoaded(menuSceneName);
#endif
    }

    // Vuelve al menu principal solo si la escena es valida.
    public bool ReturnToMenu()
    {
        if (!CanReturnToMenu())
        {
            Say("No se puede volver al menu: la escena '" + menuSceneName +
                "' no esta en Build Settings.", true);
            return false;
        }

        SceneManager.LoadScene(menuSceneName);
        return true;
    }

    // ------------------------------------------------------------
    // Mensajes
    // ------------------------------------------------------------

    private static readonly string[] LogKeywords =
    {
        "utiliza", "causa", "prepara", "lanza", "cura ", "derrotado",
        "VICTORIA", "DERROTA", "esquiva", "recibe", "sufre", "aturdid",
        "cancela", "intercepta", "transforma", "Experiencia",
        "adopta", "obtiene", "subio", "recupera", "inmune", "pasa su turno",
        "revive"
    };

    // Registro basico: ultimas lineas relevantes de combate.
    private void OnLogMessage(string message, string stack, LogType type)
    {
        if (type != LogType.Log)
            return;

        foreach (string keyword in LogKeywords)
        {
            if (message.Contains(keyword))
            {
                recentLog.Enqueue(message);

                while (recentLog.Count > 4)
                    recentLog.Dequeue();

                return;
            }
        }
    }

    private string feedback = "";
    private bool feedbackIsError;

    private void Say(string text, bool error = false)
    {
        feedback = text;
        feedbackIsError = error;
    }

    // ------------------------------------------------------------
    // Acciones
    // ------------------------------------------------------------

    private BattleUnit ActivePlayerUnit()
    {
        if (turnManager == null || turnManager.BattleFinished)
            return null;

        BattleUnit unit = turnManager.CurrentUnit;

        return unit != null && unit.IsAlive && unit.Side == TeamSide.Player
            ? unit
            : null;
    }

    private void CancelSelection()
    {
        selectedAbility = null;
        pendingConfirm = null;
        validTargets.Clear();
    }

    private void OnCancelClicked()
    {
        if (selectedAbility == null && pendingConfirm == null)
        {
            Say("No hay seleccion que cancelar.");
            return;
        }

        CancelSelection();
        Say("Seleccion cancelada. Elige otra habilidad.");
    }

    private void OnEndTurnClicked()
    {
        BattleUnit user = ActivePlayerUnit();

        if (user == null)
        {
            Say("Solo puedes terminar el turno de un protagonista.", true);
            return;
        }

        CancelSelection();
        Say(user.Data.unitName + " termina su turno.");
        turnManager.EndTurn();
    }

    private void OnAbilityClicked(int index)
    {
        BattleUnit user = ActivePlayerUnit();

        if (user == null)
        {
            Say(turnManager != null && turnManager.BattleFinished
                ? "El combate ha terminado."
                : "No es el turno de un protagonista.", true);
            return;
        }

        CharacterAbilityLoadout loadout =
            user.GetComponent<CharacterAbilityLoadout>();

        if (loadout == null || index >= loadout.Abilities.Count)
            return;

        AbilityData ability = loadout.Abilities[index];

        selectedAbility = null;
        validTargets.Clear();

        // Preparacion diferida: se confirma con un segundo clic.
        if (ability.requiresPreparation && pendingConfirm != ability)
        {
            CancelSelection();
            pendingConfirm = ability;
            Say(ability.abilityName + " requiere preparacion: se anuncia " +
                "ahora y impacta en el siguiente turno de " +
                user.Data.unitName + ". Pulsa de nuevo para confirmar.");
            return;
        }

        pendingConfirm = null;

        switch (ability.targetType)
        {
            case AbilityTargetType.Self:
                Execute(user, ability, () =>
                    abilitySystem.ExecuteAbility(user, user, ability));
                break;

            case AbilityTargetType.AllAllies:
                Execute(user, ability, () =>
                    abilitySystem.ExecuteGroupRegeneration(user, ability));
                break;

            case AbilityTargetType.AllEnemies:
                Execute(user, ability, () =>
                    abilitySystem.ExecuteAreaAbility(user, ability));
                break;

            default:
                BeginTargetSelection(user, ability);
                break;
        }
    }

    // Habilidad individual: lista de objetivos validos.
    private void BeginTargetSelection(BattleUnit user, AbilityData ability)
    {
        TeamSide side = ability.abilityType == AbilityType.Attack
            ? (user.Side == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player)
            : user.Side;

        List<BattleUnit> candidates = new List<BattleUnit>();

        for (int position = 1; position <= 4; position++)
        {
            if (!ability.CanTargetPosition(position))
                continue;

            BattleUnit unit =
                positionManager.GetUnitAtPosition(side, position);

            if (unit != null && unit.IsAlive)
                candidates.Add(unit);
        }

        if (candidates.Count == 0)
        {
            Say("No hay objetivos validos para " + ability.abilityName + ".", true);
            return;
        }

        // Comprobacion con la validacion existente (cooldown, posicion,
        // movimiento...). El motivo se toma de sus mensajes.
        bool usable = false;
        string reason = Capture(() =>
            usable = abilitySystem.CanUseAbility(user, candidates[0], ability));

        if (!usable)
        {
            Say(reason, true);
            return;
        }

        selectedAbility = ability;
        validTargets.AddRange(candidates);
        Say("Elige un objetivo para " + ability.abilityName +
            " (o cancela).");
    }

    private void OnTileClicked(int slot)
    {
        BattleUnit unit = TileUnit(slot);

        if (selectedAbility == null)
        {
            Say(unit == null
                ? "Posicion vacia."
                : unit.Data.unitName + ": " + unit.CurrentHealth + "/" +
                  unit.MaxHealth + " HP. Elige primero una habilidad.");
            return;
        }

        BattleUnit user = ActivePlayerUnit();

        if (user == null)
        {
            CancelSelection();
            return;
        }

        if (unit == null || !validTargets.Contains(unit))
        {
            Say("Objetivo no valido para " + selectedAbility.abilityName + ".", true);
            return;
        }

        AbilityData ability = selectedAbility;
        Execute(user, ability, () =>
            abilitySystem.ExecuteAbility(user, unit, ability));
    }

    // Ejecuta la accion real. Si falla, muestra el motivo y no avanza
    // el turno; si funciona, el turno se consume una sola vez.
    private void Execute(BattleUnit user, AbilityData ability, Func<bool> action)
    {
        bool success = false;
        string reason = Capture(() => success = action());

        if (!success)
        {
            Say(reason, true);
            return;
        }

        CancelSelection();

        Say(ability.requiresPreparation
            ? user.Data.unitName + " prepara " + ability.abilityName +
              ": impactara en su siguiente turno."
            : user.Data.unitName + " uso " + ability.abilityName + ".");

        turnManager.EndTurn();
    }

    // Ejecuta 'action' y devuelve el ultimo mensaje emitido (el motivo
    // de la validacion si algo fallo).
    private string Capture(Action action)
    {
        string last = "Accion no valida.";

        Application.LogCallback callback = (message, stack, type) =>
        {
            if (type == LogType.Log || type == LogType.Warning)
                last = message;
        };

        Application.logMessageReceived += callback;

        try
        {
            action();
        }
        finally
        {
            Application.logMessageReceived -= callback;
        }

        return last;
    }

    // ------------------------------------------------------------
    // Refresco
    // ------------------------------------------------------------

    // Unidad viva en la casilla indicada (0-3 aliados P4..P1,
    // 4-7 enemigos E1..E4).
    public BattleUnit TileUnit(int slot)
    {
        if (positionManager == null)
            return null;

        bool ally = slot < 4;
        int position = ally ? 4 - slot : slot - 3;

        BattleUnit unit = positionManager.GetUnitAtPosition(
            ally ? TeamSide.Player : TeamSide.Enemy,
            position
        );

        return unit != null && unit.IsAlive ? unit : null;
    }

    private static void SetText(Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

    private static string StatusName(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Bleeding: return "Sangrado";
            case StatusEffectType.Burn: return "Quemadura";
            case StatusEffectType.Poison: return "Veneno";
            case StatusEffectType.Mark: return "Marca";
            case StatusEffectType.Stun: return "Aturdido";
            case StatusEffectType.Weaken: return "Debil";
            case StatusEffectType.Vulnerability: return "Vulnerable";
            case StatusEffectType.DefenseBoost: return "DEF+";
            case StatusEffectType.Guard: return "Guardia";
            default: return type.ToString();
        }
    }

    private string StatusLine(BattleUnit unit)
    {
        string text = "";

        StatusEffectController effects =
            unit.GetComponent<StatusEffectController>();

        if (effects != null)
        {
            foreach (StatusEffectInstance effect in effects.GetActiveEffects())
                text += StatusName(effect.Type) + effect.RemainingTurns + " ";
        }

        RegenerationEffect regeneration =
            unit.GetComponent<RegenerationEffect>();

        if (regeneration != null && regeneration.IsActive)
            text += "Regen ";

        PreparedAbilityController prepared =
            unit.GetComponent<PreparedAbilityController>();

        if (prepared != null && prepared.HasPreparation)
            text += "[Prepara " + prepared.PreparedAbility.abilityName + "] ";

        return text;
    }

    private void Refresh()
    {
        if (turnManager == null || abilitySystem == null || canvas == null)
            return;

        BattleUnit active = turnManager.CurrentUnit;

        // Cambio de turno: se descarta la seleccion anterior.
        if (active != lastActive)
        {
            lastActive = active;
            CancelSelection();
        }

        // Una seleccion pendiente solo es valida en turno de jugador.
        if (selectedAbility != null && ActivePlayerUnit() == null)
            CancelSelection();

        SetText(roundText, "Ronda " + turnManager.CurrentRound);

        bool finished = turnManager.BattleFinished;
        bool playerTurn = ActivePlayerUnit() != null;

        // Cabecera y panel del personaje activo.
        if (finished)
        {
            SetText(activeText, "Combate terminado");
            SetText(infoText, "");
        }
        else if (active != null)
        {
            string side = active.Side == TeamSide.Player
                ? "Turno de " : "Turno enemigo: ";
            SetText(activeText, side + active.Data.unitName);
            SetText(infoText,
                active.Data.unitName + "\nHP " + active.CurrentHealth +
                " / " + active.MaxHealth + "\nPosicion P" + active.Position);
        }

        // Banner de victoria / derrota.
        string banner = "";
        if (finished)
        {
            bool playersAlive = false;
            foreach (BattleUnit unit in allUnits)
                if (unit != null && unit.Side == TeamSide.Player && unit.IsAlive)
                    playersAlive = true;

            banner = playersAlive ? "VICTORIA" : "DERROTA";
            bannerText.color = playersAlive
                ? new Color(0.95f, 0.8f, 0.3f)
                : new Color(0.85f, 0.25f, 0.25f);
        }
        SetText(bannerText, banner);

        // Panel de derrota total: permite ver el resultado y volver al menu.
        if (defeatPanel != null)
        {
            bool showDefeat = finished && banner == "DERROTA";

            if (defeatPanel.activeSelf != showDefeat)
                defeatPanel.SetActive(showDefeat);
        }

        // Botones de habilidad del loadout activo.
        CharacterAbilityLoadout loadout = playerTurn
            ? active.GetComponent<CharacterAbilityLoadout>()
            : null;

        AbilityCooldownController cooldowns = playerTurn
            ? active.GetComponent<AbilityCooldownController>()
            : null;

        for (int i = 0; i < AbilitySlots; i++)
        {
            bool has = loadout != null && i < loadout.Abilities.Count;

            if (!has)
            {
                SetText(abilityLabels[i], playerTurn ? "-" : "");
                abilityImages[i].color = ButtonBlocked;
                abilityLabels[i].color = DimTextColor;
                continue;
            }

            AbilityData ability = loadout.Abilities[i];
            int remaining = cooldowns != null
                ? cooldowns.GetRemainingTurns(ability) : 0;
            bool blocked = remaining > 0 ||
                !ability.CanUseFromPosition(active.Position);

            string status = remaining > 0
                ? "Cooldown " + remaining
                : (!ability.CanUseFromPosition(active.Position)
                    ? "Posicion no valida"
                    : (ability.requiresPreparation ? "Lista (prepara)" : "Lista"));

            SetText(abilityLabels[i], ability.abilityName + "\n" + status);
            abilityLabels[i].color = blocked ? DimTextColor : TextColor;
            abilityImages[i].color =
                (selectedAbility == ability || pendingConfirm == ability)
                    ? ButtonSelected
                    : (blocked ? ButtonBlocked : ButtonColor);
        }

        // Detalle de la habilidad seleccionada.
        AbilityData detail = selectedAbility != null
            ? selectedAbility : pendingConfirm;

        SetText(detailText, detail == null ? "" : Describe(detail));

        // Preparacion de Meteorito (o similar) en curso.
        string prep = "";
        foreach (BattleUnit unit in allUnits)
        {
            if (unit == null) continue;
            PreparedAbilityController prepared =
                unit.GetComponent<PreparedAbilityController>();
            if (prepared != null && prepared.HasPreparation)
                prep += unit.Data.unitName + " prepara " +
                        prepared.PreparedAbility.abilityName +
                        ": impacta en su siguiente turno.  ";
        }
        SetText(prepText, prep);

        // Mensajes.
        SetText(messageText, feedback);
        messageText.color = feedbackIsError
            ? new Color(0.95f, 0.55f, 0.45f) : TextColor;

        SetText(logText, string.Join("\n", recentLog.ToArray()));

        SetText(hintText, finished
            ? ""
            : (playerTurn
                ? "Elige una habilidad. Y: transformar a Noelle (depuracion)."
                : (active != null &&
                   active.GetComponent<EnemyAIController>() != null
                    ? "Turno enemigo: actua automaticamente."
                    : "Turno enemigo: pulsa A para que ataque.")));

        cancelButton.interactable = !finished;
        endTurnButton.interactable = playerTurn;

        // Casillas de unidades.
        for (int slot = 0; slot < SlotCount; slot++)
        {
            BattleUnit unit = TileUnit(slot);
            bool ally = slot < 4;
            int position = ally ? 4 - slot : slot - 3;
            string label = (ally ? "P" : "E") + position;

            if (unit == null)
            {
                SetText(tileTexts[slot], label + "\n(vacio)");
                tileImages[slot].color = EmptyColor;
                tileBars[slot].anchorMax = new Vector2(0f, 1f);
                tileTexts[slot].color = DimTextColor;
                continue;
            }

            SetText(tileTexts[slot],
                unit.Data.unitName + "  " + label +
                "\nHP " + unit.CurrentHealth + "/" + unit.MaxHealth +
                "\n" + StatusLine(unit));
            tileTexts[slot].color = TextColor;

            tileBars[slot].anchorMax = new Vector2(
                Mathf.Clamp01((float)unit.CurrentHealth / unit.MaxHealth), 1f);

            Color baseColor = ally ? AllyColor : EnemyColor;
            tileImages[slot].color =
                validTargets.Contains(unit) ? TargetColor
                : (unit == active ? ActiveColor : baseColor);
        }
    }

    private static string Describe(AbilityData ability)
    {
        string target;
        switch (ability.targetType)
        {
            case AbilityTargetType.Self: target = "uno mismo"; break;
            case AbilityTargetType.AllAllies: target = "todos los aliados"; break;
            case AbilityTargetType.AllEnemies: target = "todos los enemigos"; break;
            default:
                target = ability.abilityType == AbilityType.Attack
                    ? "un enemigo" : "un aliado";
                break;
        }

        string text = ability.abilityName + " - objetivo: " + target +
                      " - cooldown " + ability.cooldown;

        if (ability.requiresPreparation)
            text += " - REQUIERE PREPARACION (impacta en el siguiente turno)";

        if (ability.userMovement != UserMovement.None)
            text += " - mueve al usuario";

        return text;
    }

    // ------------------------------------------------------------
    // Construccion de la interfaz
    // ------------------------------------------------------------

    private Font font;

    private RectTransform Box(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return rect;
    }

    private Text Label(string name, Transform parent, Vector2 min, Vector2 max,
        int size, TextAnchor anchor, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = new Vector2(8, 4); rect.offsetMax = new Vector2(-8, -4);
        Text text = go.GetComponent<Text>();
        text.font = font; text.fontSize = size; text.alignment = anchor;
        text.color = color; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Button MakeButton(string name, Transform parent, Vector2 min, Vector2 max,
        Color color, out Text label, out Image image, int size = 22)
    {
        RectTransform rect = Box(name, parent, min, max, color);
        image = rect.GetComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        button.colors = colors;
        label = Label("Texto", rect, Vector2.zero, Vector2.one, size,
            TextAnchor.MiddleCenter, TextColor);
        return button;
    }

    // Genera (o regenera) toda la interfaz bajo este GameObject.
    public void BuildUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Camera cam = null;
        foreach (Camera c in FindObjectsByType<Camera>())
            if (c.gameObject.scene == gameObject.scene) cam = c;

        GameObject canvasGo = new GameObject("Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();

        if (cam != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Transform root = canvasGo.transform;

        // Barra superior.
        RectTransform top = Box("BarraSuperior", root, new Vector2(0, 0.93f), Vector2.one, PanelColor);
        roundText = Label("Ronda", top, new Vector2(0, 0), new Vector2(0.18f, 1), 30, TextAnchor.MiddleLeft, TextColor);
        activeText = Label("TurnoActivo", top, new Vector2(0.18f, 0), new Vector2(0.62f, 1), 32, TextAnchor.MiddleCenter, new Color(0.95f, 0.85f, 0.5f));
        prepText = Label("Preparacion", top, new Vector2(0.55f, 0), new Vector2(1, 1), 20, TextAnchor.MiddleRight, new Color(0.95f, 0.7f, 0.4f));

        // Casillas: aliados P4..P1 a la izquierda, enemigos E1..E4 a la derecha.
        for (int slot = 0; slot < SlotCount; slot++)
        {
            float x0 = slot < 4 ? 0.015f + slot * 0.118f : 0.515f + (slot - 4) * 0.118f;
            RectTransform tile = Box("Casilla_" + slot, root, new Vector2(x0, 0.775f), new Vector2(x0 + 0.112f, 0.925f), AllyColor);
            tileImages[slot] = tile.GetComponent<Image>();
            Button b = tile.gameObject.AddComponent<Button>();
            b.targetGraphic = tileImages[slot];
            tileButtons[slot] = b;
            tileTexts[slot] = Label("Texto", tile, new Vector2(0, 0.12f), Vector2.one, 20, TextAnchor.UpperLeft, TextColor);
            RectTransform barBg = Box("BarraFondo", tile, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.12f), new Color(0.05f, 0.05f, 0.08f, 1f));
            tileBars[slot] = Box("Vida", barBg, Vector2.zero, Vector2.one, slot < 4 ? new Color(0.35f, 0.75f, 0.45f) : new Color(0.85f, 0.35f, 0.3f));
        }

        // Mensajes y registro.
        RectTransform mid = Box("Mensajes", root, new Vector2(0.015f, 0.635f), new Vector2(0.985f, 0.77f), new Color(0.05f, 0.07f, 0.11f, 0.93f));
        messageText = Label("Mensaje", mid, new Vector2(0, 0.58f), Vector2.one, 24, TextAnchor.MiddleCenter, TextColor);
        logText = Label("Registro", mid, new Vector2(0, 0), new Vector2(1, 0.58f), 16, TextAnchor.LowerCenter, DimTextColor);
        bannerText = Label("Banner", root, new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.6f), 90, TextAnchor.MiddleCenter, Color.white);
        bannerText.fontStyle = FontStyle.Bold;

        // Panel inferior del personaje activo.
        RectTransform bottom = Box("PanelPersonaje", root, Vector2.zero, new Vector2(1, 0.34f), PanelColor);
        infoText = Label("Info", bottom, new Vector2(0.01f, 0.1f), new Vector2(0.2f, 0.95f), 26, TextAnchor.UpperLeft, TextColor);
        for (int i = 0; i < AbilitySlots; i++)
        {
            float x0 = 0.215f + i * 0.158f;
            abilityButtons[i] = MakeButton("Habilidad_" + i, bottom, new Vector2(x0, 0.3f), new Vector2(x0 + 0.15f, 0.92f),
                ButtonColor, out abilityLabels[i], out abilityImages[i], 24);
        }
        detailText = Label("Detalle", bottom, new Vector2(0.215f, 0.12f), new Vector2(0.84f, 0.3f), 18, TextAnchor.MiddleLeft, new Color(0.85f, 0.78f, 0.55f));
        hintText = Label("Ayuda", bottom, new Vector2(0.215f, 0f), new Vector2(0.84f, 0.12f), 16, TextAnchor.MiddleLeft, DimTextColor);
        Text t1, t2; Image i1, i2;
        cancelButton = MakeButton("Cancelar", bottom, new Vector2(0.86f, 0.55f), new Vector2(0.99f, 0.92f), ButtonColor, out t1, out i1, 22);
        t1.text = "Cancelar";
        endTurnButton = MakeButton("TerminarTurno", bottom, new Vector2(0.86f, 0.12f), new Vector2(0.99f, 0.49f), ButtonColor, out t2, out i2, 22);
        t2.text = "Terminar turno";

        // Panel de derrota total (oculto hasta perder).
        RectTransform defeat = Box("PanelDerrota", root, new Vector2(0.38f, 0.355f), new Vector2(0.62f, 0.44f), PanelColor);
        Text returnLabel; Image returnImage;
        returnToMenuButton = MakeButton("VolverMenu", defeat, new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), ButtonColor, out returnLabel, out returnImage, 28);
        returnLabel.text = "Volver al menú";
        defeatPanel = defeat.gameObject;
        defeatPanel.SetActive(false);

        // EventSystem compatible con el nuevo Input System.
        EventSystem existing = null;
        foreach (EventSystem es in FindObjectsByType<EventSystem>())
            if (es.gameObject.scene == gameObject.scene) existing = es;

        if (existing == null)
        {
            GameObject esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            esGo.transform.SetParent(transform, false);
            esGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
