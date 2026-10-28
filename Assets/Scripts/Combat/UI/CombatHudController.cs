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

    [Header("Objetos")]
    [SerializeField] private Button itemsButton;
    [SerializeField] private Text resourcesText;

    [Header("Derrota total")]
    [Tooltip("Escena a la que se vuelve tras perder (debe estar en Build Settings).")]
    [SerializeField] private string menuSceneName = "Menu";
    [SerializeField] private GameObject defeatPanel;
    [SerializeField] private Button returnToMenuButton;

    [Header("Victoria")]
    [Tooltip("Escena del mapa a la que se vuelve tras ganar (debe estar en Build Settings).")]
    [SerializeField] private string mapSceneName = "Map";
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private Button continueButton;

    private bool leavingScene;

    [SerializeField] private Button[] tileButtons = new Button[SlotCount];
    [SerializeField] private Image[] tileImages = new Image[SlotCount];
    [SerializeField] private Text[] tileTexts = new Text[SlotCount];
    [SerializeField] private RectTransform[] tileBars = new RectTransform[SlotCount];

    // Rediseno (Paso 64.2): paneles y marcos provisionales a partir de los
    // sprites de MapArtSet. Sustituir los sprites del MapArtSet por los
    // marcos finales no requiere tocar este codigo.
    [Header("Rediseno del HUD (generado por BuildUI)")]
    [SerializeField] private MapArtSet art;
    [SerializeField] private Text regionText;
    [SerializeField] private Image[] turnChips = new Image[8];
    [SerializeField] private Text[] turnChipTexts = new Text[8];
    [SerializeField] private Text[] tileStatus = new Text[SlotCount];
    [SerializeField] private Image[] tileBorders = new Image[SlotCount];
    [SerializeField] private RectTransform[] unitMarkers = new RectTransform[SlotCount];
    [SerializeField] private Button[] itemButtons = new Button[2];
    [SerializeField] private Image[] itemImages = new Image[2];
    [SerializeField] private Text[] itemTexts = new Text[2];
    [SerializeField] private Text[] itemDescriptions = new Text[2];
    [SerializeField] private Text abilityTitleText;
    [SerializeField] private GameObject toastRoot;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private EncounterLoader encounterLoader;

    // Sprites de Assets/Sprites/Buttons (recortados al marco visible, con
    // bordes 9-slice definidos en su importacion). Si alguno falta, el HUD
    // usa los paneles provisionales de MapArtSet.
    [Header("Sprites del HUD (Paso 64.3)")]
    [SerializeField] private Sprite headerSprite;       // BARRA 1
    [SerializeField] private Sprite dividerSprite;      // BARRA 2
    [SerializeField] private Sprite panelSprite;        // marco 1
    [SerializeField] private Sprite wideFrameSprite;    // borde 3
    [SerializeField] private Sprite chipSprite;         // marco 4
    [SerializeField] private Sprite buttonNormal;       // Button_Normal
    [SerializeField] private Sprite buttonHover;        // Button_Hover
    [SerializeField] private Sprite buttonDisabled;     // Button_Disabled
    [SerializeField] private Sprite pointSprite;        // PUNTO

    [Header("Iconos de habilidades (Paso 64.4)")]
    [SerializeField] private AbilityIconSet iconSet;
    [SerializeField] private Image[] abilityIcons = new Image[AbilitySlots];
    [SerializeField] private Text[] abilityCooldowns = new Text[AbilitySlots];

    [Header("Referencia visual (Paso 64.7)")]
    [SerializeField] private Sprite cardSprite;         // HUD3 recortado: tarjetas de unidad
    [SerializeField] private Sprite abilityFrameSprite; // HUD6 recortado: botones de habilidad
    [SerializeField] private Image infoIcon;
    [SerializeField] private Material frameMaterial;    // HudFrameSoft: saturacion/brillo de los marcos
    [SerializeField] private Text[] itemCounts = new Text[2];

    [Header("Estilo plano (Paso 926)")]
    [SerializeField] private Sprite[] consumableIcons = new Sprite[2];   // curacion y antidoto
    [SerializeField] private Text cancelLabel;

    // Nombres de presentacion de los protagonistas (solo HUD: no cambia los
    // datos del personaje ni su identidad en el combate).
    private static string DisplayName(BattleUnit unit)
    {
        if (unit == null || unit.Data == null)
            return "";

        switch (unit.Data.unitName)
        {
            case "Caballero": return "Valerian";
            case "Arquera": return "Elaina";
            // Nombre corto para tarjetas y turnos; el nombre completo se ve en la cabecera.
            case "Capitán de las Fuerzas Expedicionarias": return "Capitán";
            default: return unit.Data.unitName;
        }
    }

    private static string Abbreviation(BattleUnit unit)
    {
        string name = DisplayName(unit);

        switch (name)
        {
            case "Valerian": return "VAL";
            case "Elaina": return "ELA";
            case "Noelle": return "NOE";
            case "Evanora": return "EVA";
            default: return name.Length > 3 ? name.Substring(0, 3).ToUpper() : name.ToUpper();
        }
    }

    private enum ButtonState { Normal, Selected, Blocked }

    // Icono de una habilidad (solo presentacion). Atenuado si no se puede
    // usar; el contador de recarga se dibuja encima.
    private void ShowAbilityIcon(int i, AbilityData ability, bool blocked, int cooldown)
    {
        if (abilityIcons[i] == null)
            return;

        Sprite sprite = ability != null && iconSet != null ? iconSet.Get(ability) : null;
        bool show = sprite != null;

        if (abilityIcons[i].enabled != show)
            abilityIcons[i].enabled = show;

        if (show)
        {
            if (abilityIcons[i].sprite != sprite)
                abilityIcons[i].sprite = sprite;

            abilityIcons[i].color = blocked ? new Color(0.38f, 0.38f, 0.42f, 1f) : Color.white;
        }

        if (abilityCooldowns[i] != null)
            SetText(abilityCooldowns[i], cooldown > 0 ? cooldown.ToString() : "");
    }


    // Estilo del HUD: paneles planos azul marino con bordes finos (Outline) en lugar de los marcos
    // ornamentales. El dorado queda para titulos, botones principales y elementos seleccionados.
    private static readonly bool FlatStyle = true;

    // Los marcos ornamentales de los sprites traen un bisel negro integrado que se ve como una banda oscura
    // dentro del borde dorado; los contenedores usan una linea dorada fina sobre el relleno azul marino.
    private static readonly bool OrnateContainers = false;

    private bool Skinned => !FlatStyle && buttonNormal != null && buttonHover != null && buttonDisabled != null;

    private static readonly Color LineBlue = new Color(0.239f, 0.290f, 0.361f, 1f);       // azul grisaceo
    private static readonly Color AgedGold = new Color(0.62f, 0.52f, 0.34f, 1f);          // dorado envejecido
    private static readonly Color RaisedNavy = new Color(0.130f, 0.176f, 0.247f, 1f);     // fondo de seleccion
    private static readonly Color CardLine = new Color(0.33f, 0.41f, 0.52f, 1f);       // borde de tarjetas de unidad
    private static readonly Color HoverNavy = new Color(0.112f, 0.150f, 0.212f, 1f);

    // Borde fino de un elemento (Outline de 1,5 px). 'interactive' anade el componente de hover.
    private static void AddFlatBorder(GameObject go, Color border, bool interactive, float width = 1.5f)
    {
        Outline outline = go.GetComponent<Outline>();

        if (outline == null)
            outline = go.AddComponent<Outline>();

        outline.effectColor = border;
        outline.effectDistance = new Vector2(width, -width);
        outline.useGraphicAlpha = false;

        if (!interactive)
            return;

        HudFrameStyle style = go.GetComponent<HudFrameStyle>();

        if (style == null)
            style = go.AddComponent<HudFrameStyle>();

        style.normalBorder = border;
    }

    private static Color Dim(Color c, float k)
    {
        return new Color(c.r * k, c.g * k, c.b * k, c.a);
    }

    // Estado visual de un boton: con sprites ilustrados se cambia el
    // sprite (normal / resaltado / deshabilitado); sin ellos, el color.
    private void SetButtonState(Image image, ButtonState state, bool ability = false)
    {
        if (image == null)
            return;

        if (FlatStyle)
        {
            // Normal / Hover / Seleccionado / Deshabilitado: relleno azul y borde fino (dorado solo al seleccionar).
            HudFrameStyle style = image.GetComponent<HudFrameStyle>();
            Outline border = image.GetComponent<Outline>();
            bool hovered = style != null && style.Hovered && state != ButtonState.Blocked;

            image.color = state == ButtonState.Selected ? RaisedNavy
                : (state == ButtonState.Blocked ? ButtonBlocked : (hovered ? HoverNavy : ButtonColor));

            if (border != null)
                border.effectColor = state == ButtonState.Selected ? GoldBright
                    : (state == ButtonState.Blocked ? Dim(LineBlue, 0.65f)
                        : (hovered ? AgedGold : (style != null ? style.normalBorder : LineBlue)));

            return;
        }

        if (Skinned)
        {
            // Seleccionado = relleno ambar suave (HUD8); disponible = marco
            // dorado discreto; bloqueado = mismo marco atenuado.
            Sprite frame = ability && abilityFrameSprite != null ? abilityFrameSprite : buttonNormal;
            Sprite blocked = ability && abilityFrameSprite != null ? abilityFrameSprite : buttonDisabled;
            Sprite sprite = state == ButtonState.Selected ? buttonHover
                : (state == ButtonState.Blocked ? blocked : frame);

            if (image.sprite != sprite)
                image.sprite = sprite;

            image.color = state == ButtonState.Selected ? FrameSelected
                : (state == ButtonState.Blocked ? FrameDisabled : FrameTint);
            SetFill(image, state == ButtonState.Selected ? RaisedNavy
                : (state == ButtonState.Blocked ? ButtonBlocked : ButtonColor));
            return;
        }

        image.color = state == ButtonState.Selected ? ButtonSelected
            : (state == ButtonState.Blocked ? ButtonBlocked : ButtonColor);
    }

    // Paleta: negro azulado, paneles oscuros y oro apagado.
    private static readonly Color BackColor = new Color(0.063f, 0.082f, 0.118f, 1f);       // #10151E
    private static readonly Color PanelColor = new Color(0.090f, 0.122f, 0.169f, 1f);  // #171F2B
    private static readonly Color SurfaceColor = new Color(0.145f, 0.169f, 0.208f, 1f);   // #252B35
    private static readonly Color GoldColor = new Color(0.659f, 0.541f, 0.345f, 1f);      // #A88A58 dorado normal
    private static readonly Color GoldBright = new Color(0.831f, 0.659f, 0.357f, 1f);     // #D4A85B dorado seleccionado
    private static readonly Color AllyColor = new Color(0.090f, 0.122f, 0.169f, 1f);
    private static readonly Color EnemyColor = new Color(0.118f, 0.110f, 0.160f, 1f);
    private static readonly Color EmptyColor = new Color(0.12f, 0.13f, 0.17f, 0.7f);
    private static readonly Color ActiveColor = new Color(0.17f, 0.22f, 0.30f, 0.98f);
    private static readonly Color TargetColor = new Color(0.95f, 0.45f, 0.35f, 1f);
    private static readonly Color ButtonColor = new Color(0.078f, 0.102f, 0.145f, 1f);
    private static readonly Color ButtonBlocked = new Color(0.058f, 0.074f, 0.106f, 1f);
    private static readonly Color ButtonSelected = new Color(0.40f, 0.30f, 0.12f, 1f);
    private static readonly Color TextColor = new Color(0.902f, 0.867f, 0.792f, 1f);      // #E6DDCA
    private static readonly Color DimTextColor = new Color(0.46f, 0.47f, 0.50f, 1f);
    private static readonly Color SecondaryText = new Color(0.710f, 0.667f, 0.588f, 1f);  // #B5AA96
    // Tintes que apagan los marcos dorados originales sin tocar los PNG.
    private static readonly Color FrameTint = new Color(0.60f, 0.58f, 0.52f, 1f);
    private static readonly Color FrameSelected = new Color(0.95f, 0.88f, 0.72f, 1f);
    private static readonly Color FrameDisabled = new Color(0.40f, 0.40f, 0.44f, 1f);
    private static readonly Color HealthColor = new Color(0.62f, 0.14f, 0.16f, 1f);
    private static readonly Color HealthEnemyColor = new Color(0.72f, 0.20f, 0.16f, 1f);

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

        ApplyFont();
        CacheUnits();
        WireButtons();
    }

    private void OnEnable()
    {
        Application.logMessageReceived += OnLogMessage;
        EnemyAIController.ActionPerformed += OnEnemyAction;
        BossPhaseController.PhaseChanged += OnBossPhaseChanged;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= OnLogMessage;
        EnemyAIController.ActionPerformed -= OnEnemyAction;
        BossPhaseController.PhaseChanged -= OnBossPhaseChanged;
    }

    // Cambio de fase de un jefe (solo presentacion).
    private void OnBossPhaseChanged(BossPhaseController boss, BossPhaseData.Phase previous, BossPhaseData.Phase next)
    {
        if (next == null)
            return;

        if (!string.IsNullOrEmpty(next.announcement))
            phaseMessage = next.announcement;

        if (string.IsNullOrEmpty(next.bannerText) || bannerText == null)
            return;

        // El rotulo se crea la primera vez que hace falta (no se guarda en la escena).
        if (phaseNotice == null)
        {
            phaseNotice = Label("AvisoFase", bannerText.transform.parent,
                new Vector2(0.735f, 0.800f), new Vector2(0.990f, 0.875f), 30,
                TextAnchor.MiddleCenter, new Color(1f, 0.74f, 0.34f));
            phaseNotice.fontStyle = FontStyle.Bold;
            phaseNotice.resizeTextForBestFit = true;
            phaseNotice.resizeTextMinSize = 16;
            phaseNotice.resizeTextMaxSize = 30;
            phaseNotice.horizontalOverflow = HorizontalWrapMode.Wrap;
            Outline outline = phaseNotice.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        phaseNotice.text = next.bannerText;
        phaseNoticeUntil = Time.unscaledTime + PhaseNoticeSeconds;
    }

    private static string PhaseTag(BattleUnit unit)
    {
        BossPhaseController phases = unit != null ? unit.GetComponent<BossPhaseController>() : null;

        return phases != null && phases.CurrentIndex > 0
            ? "<color=#F1B35A>FASE" + (phases.CurrentIndex + 1) + "</color> "
            : "";
    }

    // Aviso de la marca tactica del Capitan (solo presentacion).
    private void OnEnemyAction(BattleUnit self, AbilityData ability, BattleUnit target, bool special)
    {
        // Habilidad con frase de uso (p. ej. Ultima Orden): frase ahora y, un
        // instante despues, el nombre y los efectos realmente aplicados.
        if (ability != null && self != null && !string.IsNullOrEmpty(ability.useAnnouncement))
        {
            Say(ability.useAnnouncement);

            List<string> names = new List<string>();
            int affected = 0;

            foreach (AbilityStatusEffect status in ability.statusEffects)
            {
                if (status == null)
                    continue;

                string label = status.type == StatusEffectType.Weaken
                    ? "Debilitamiento " + status.potency + "% ATK (" + status.duration + (status.duration == 1 ? " turno)" : " turnos)")
                    : status.type.ToString();
                names.Add(label);

                foreach (BattleUnit unit in allUnits)
                {
                    if (unit == null || !unit.IsAlive || unit.Side == self.Side)
                        continue;

                    StatusEffectController fx = unit.GetComponent<StatusEffectController>();
                    StatusEffectInstance applied = fx != null ? fx.GetEffect(status.type) : null;

                    if (applied != null && applied.Source == self)
                        affected++;
                }
            }

            followUpText = ability.abilityName + (names.Count > 0
                ? ": " + string.Join(", ", names) + " sobre " + affected + (affected == 1 ? " protagonista." : " protagonistas.")
                : ".");
            followUpAt = Time.unscaledTime + 1.8f;
            return;
        }

        // Anuncio de un ataque preparado por areas (Fuego de Supresion).
        if (ability != null && self != null && ability.requiresPreparation &&
            ability.selectableAreas != null && ability.selectableAreas.Count > 0)
        {
            PreparedAbilityController pc = self.GetComponent<PreparedAbilityController>();

            if (pc != null && pc.PreparedArea != null)
                Say("El " + DisplayName(self) + " prepara fuego sobre " +
                    pc.PreparedArea.label + ": " + pc.PreparedArea.PositionsText() + ".");

            return;
        }

        if (ability == null || target == null || ability.statusEffects == null ||
            !ability.statusEffects.Exists(s => s != null && s.type == StatusEffectType.PriorityTarget))
            return;

        StatusEffectController effects = target.GetComponent<StatusEffectController>();
        StatusEffectInstance mark = effects != null ? effects.GetEffect(StatusEffectType.PriorityTarget) : null;

        Say(mark != null && mark.Source == self
            ? "El " + DisplayName(self) + " designa a " + DisplayName(target) + " como objetivo prioritario."
            : DisplayName(target) + " esquiva la designación del " + DisplayName(self) + ".");
    }

    private void Update()
    {
        // Esc cancela cualquier seleccion pendiente (habilidad, objeto o
        // movimiento) sin consumir el turno.
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && HasPendingSelection())
            OnCancelClicked();

        Refresh();
    }

    private bool HasPendingSelection()
    {
        return selectedAbility != null || pendingConfirm != null || previewItem >= 0 ||
               moveMode || itemMode || itemSelected >= 0;
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

        for (int i = 0; i < moveDirButtons.Length; i++)
        {
            if (moveDirButtons[i] == null)
                continue;

            int index = i;
            moveDirButtons[i].onClick.RemoveAllListeners();
            moveDirButtons[i].onClick.AddListener(() => OnMoveDirectionClicked(index));
        }

        if (moveButton != null)
        {
            moveButton.onClick.RemoveAllListeners();
            moveButton.onClick.AddListener(OnMoveClicked);

            MapHoverTarget hover = moveButton.GetComponent<MapHoverTarget>();

            if (hover != null)
            {
                hover.entered = () => moveHover = true;
                hover.exited = () => moveHover = false;
            }
        }

        endTurnButton.onClick.RemoveAllListeners();
        endTurnButton.onClick.AddListener(OnEndTurnClicked);

        if (itemsButton != null)
        {
            itemsButton.onClick.RemoveAllListeners();
            itemsButton.onClick.AddListener(OnItemsClicked);
        }

        for (int i = 0; i < itemButtons.Length; i++)
        {
            if (itemButtons[i] == null)
                continue;

            int slot = i;
            itemButtons[i].onClick.RemoveAllListeners();
            itemButtons[i].onClick.AddListener(() => OnConsumableClicked(slot));
        }

        if (returnToMenuButton != null)
        {
            returnToMenuButton.onClick.RemoveAllListeners();
            returnToMenuButton.onClick.AddListener(() => ReturnToMenu());
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() => ContinueToMap());
        }
    }

    // Tras una victoria: vuelve al mapa conservando la sesion.
    public bool ContinueToMap()
    {
        if (leavingScene || turnManager == null || !turnManager.BattleFinished ||
            turnManager.PartyDefeated || !GameSession.IsActive)
            return false;

        if (!GameSession.IsSceneInBuild(mapSceneName))
        {
            Say("No se puede volver al mapa: la escena '" + mapSceneName +
                "' no esta en Build Settings.", true);
            return false;
        }

        leavingScene = true;
        SceneManager.LoadScene(mapSceneName);
        return true;
    }

    // La escena de menu existe y esta incluida en Build Settings.
    public bool CanReturnToMenu()
    {
        return GameSession.IsSceneInBuild(menuSceneName);
    }

    // Descarta la sesion y comprueba la escena del menu. No carga la
    // escena (lo hace ReturnToMenu). Solo descarta si el menu es valido,
    // para no dejar al jugador sin salida.
    public bool PrepareReturnToMenu()
    {
        if (!CanReturnToMenu())
        {
            Say("No se puede volver al menu: la escena '" + menuSceneName +
                "' no esta en Build Settings.", true);
            return false;
        }

        // Sin reintento: XP, niveles, vida y etapa de Noelle se pierden.
        GameSession.EndSession();
        return true;
    }

    // Vuelve al menu principal descartando toda la sesion.
    public bool ReturnToMenu()
    {
        if (leavingScene || !PrepareReturnToMenu())
            return false;

        leavingScene = true;
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

    // Aviso de cambio de fase de un jefe: mensaje (se muestra en el toast al
    // siguiente Refresh, para no ser pisado por "X uso Y") y rotulo temporal.
    private string phaseMessage = "";
    private string followUpText = "";
    private float followUpAt;
    private Text phaseNotice;
    private float phaseNoticeUntil;
    private const float PhaseNoticeSeconds = 3.5f;

    private string feedback = "";
    private bool feedbackIsError;

    private float feedbackTime;

    private void Say(string text, bool error = false)
    {
        feedback = text;
        feedbackIsError = error;
        feedbackTime = Time.unscaledTime;
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

    // Menu de objetos abierto y objeto elegido (0 pocion, 1 antidoto).
    private bool itemMode;
    private int itemSelected = -1;

    // Consumible solo consultado (sin existencias): muestra su informacion
    // pero nunca se puede usar.
    private int previewItem = -1;

    // ------------------------------------------------------------
    // Movimiento tactico (Paso 64.8). Las reglas de formacion son las de
    // PositionManager (CanMoveUnit / MoveUnit, que ya intercambia con el
    // aliado de la casilla destino). Mover consume la accion completa y
    // termina el turno con TurnManager.EndTurn; no inicia cooldowns.
    // ------------------------------------------------------------

    [Header("Movimiento tactico (generado por BuildUI)")]
    [SerializeField] private Button moveButton;
    [SerializeField] private Image moveButtonImage;
    [SerializeField] private GameObject moveContainer;
    [SerializeField] private Button[] moveDirButtons = new Button[2];
    [SerializeField] private Image[] moveDirImages = new Image[2];
    [SerializeField] private Text[] moveDirTexts = new Text[2];

    private bool moveMode;
    private int moveDirection;      // 0 sin elegir, -1 avanzar, +1 retroceder
    private bool moveHover;

    private const string MoveInfo =
        "Permite avanzar o retroceder una posición. Consume la acción del personaje activo.";

    // Evalua una direccion para el protagonista activo.
    // Devuelve true si es valida; 'swap' es el aliado que cambia de lugar.
    private bool EvaluateMove(BattleUnit user, int direction, out BattleUnit swap, out int target, out string reason)
    {
        swap = null;
        reason = "";
        target = user.Position + direction;

        if (target < 1)
        {
            reason = "Ya estás en P1: no se puede avanzar.";
            return false;
        }

        if (target > 4)
        {
            reason = "Ya estás en P4: no se puede retroceder.";
            return false;
        }

        // La formacion se compacta al caer unidades: el destino debe estar
        // ocupado por un aliado vivo (intercambio), nunca un hueco.
        swap = positionManager.GetUnitAtPosition(TeamSide.Player, target);

        if (swap == null)
        {
            reason = "No hay ningún aliado en P" + target + ".";
            return false;
        }

        if (!positionManager.CanMoveUnit(user, direction))
        {
            reason = "Movimiento no permitido.";
            return false;
        }

        return true;
    }

    // Texto de una direccion: titulo y subtitulo (destino / aliado, o motivo breve).
    private bool MoveOptionLines(BattleUnit user, int direction, out string title, out string subtitle)
    {
        string name = direction < 0 ? "Avanzar" : "Retroceder";

        if (EvaluateMove(user, direction, out BattleUnit swap, out int target, out _))
        {
            title = name + " a P" + target;
            subtitle = "Intercambia posición con " + DisplayName(swap);
            return true;
        }

        title = name;
        int raw = user.Position + direction;
        subtitle = raw < 1 || raw > 4
            ? "No disponible desde P" + user.Position
            : "No hay ningún aliado en P" + raw;
        return false;
    }

    // Muestra u oculta los cuatro botones de habilidad y el contenedor de
    // movimiento, y refresca los dos cuadros de direccion.
    private void RefreshMoveOptions(BattleUnit active, bool playerTurn)
    {
        bool show = moveMode && playerTurn && active != null;

        if (moveContainer != null && moveContainer.activeSelf != show)
            moveContainer.SetActive(show);

        foreach (Button b in abilityButtons)
            if (b != null && b.gameObject.activeSelf == show)
                b.gameObject.SetActive(!show);

        if (!show)
            return;

        for (int i = 0; i < 2; i++)
        {
            int dir = i == 0 ? -1 : 1;
            bool ok = MoveOptionLines(active, dir, out string title, out string subtitle);
            moveDirTexts[i].supportRichText = true;
            SetText(moveDirTexts[i], "<size=28><b>" + title + "</b></size>\n<size=20>" + subtitle + "</size>");
            moveDirTexts[i].color = ok ? TextColor : DimTextColor;
            SetButtonState(moveDirImages[i], moveDirection == dir ? ButtonState.Selected
                : (ok ? ButtonState.Normal : ButtonState.Blocked), true);
        }
    }

    private void OnMoveClicked()
    {
        BattleUnit user = ActivePlayerUnit();

        if (user == null)
        {
            Say(turnManager != null && turnManager.BattleFinished
                ? "El combate ha terminado."
                : "No es el turno de un protagonista.", true);
            return;
        }

        if (moveMode)
        {
            CancelSelection();
            Say("Movimiento cancelado.");
            return;
        }

        CancelSelection();
        moveMode = true;
        Say("Elige una dirección. Esc para volver.");
    }

    // Clic en uno de los dos botones de direccion (0 avanzar, 1 retroceder).
    // Primer clic: vista previa del destino. Segundo clic: confirma.
    private void OnMoveDirectionClicked(int index)
    {
        BattleUnit user = ActivePlayerUnit();

        if (user == null)
        {
            CancelSelection();
            return;
        }

        if (index > 1)
            return;

        int direction = index == 0 ? -1 : 1;

        if (!EvaluateMove(user, direction, out BattleUnit swap, out int target, out string reason))
        {
            moveDirection = 0;
            validTargets.Clear();
            Say(reason, true);
            return;
        }

        if (moveDirection != direction)
        {
            moveDirection = direction;
            validTargets.Clear();
            validTargets.Add(swap);
            Say(DisplayName(user) + ": P" + user.Position + " → P" + target +
                " (intercambia con " + DisplayName(swap) + "). Pulsa de nuevo para confirmar.");
            return;
        }

        int from = user.Position;
        string userName = DisplayName(user);
        string swapName = DisplayName(swap);

        if (!positionManager.MoveUnit(user, direction))
        {
            Say("No se pudo realizar el movimiento.", true);
            return;
        }

        CancelSelection();
        Say(userName + " se mueve de P" + from + " a P" + user.Position + " (intercambia con " + swapName + ").");
        Debug.Log(userName + " utiliza Mover: de P" + from + " a P" + user.Position);
        turnManager.EndTurn();
    }

    private void CancelSelection()
    {
        moveMode = false;
        moveDirection = 0;
        previewItem = -1;
        selectedAbility = null;
        pendingConfirm = null;
        itemMode = false;
        itemSelected = -1;
        validTargets.Clear();
    }

    private void OnCancelClicked()
    {
        if (selectedAbility == null && pendingConfirm == null && previewItem < 0 && !moveMode &&
            !itemMode && itemSelected < 0)
        {
            Say("No hay seleccion que cancelar.");
            return;
        }

        CancelSelection();
        Say("Seleccion cancelada. Elige otra habilidad.");
    }

    // Abre o cierra el menu de objetos (solo en turno de un protagonista).
    private void OnItemsClicked()
    {
        BattleUnit user = ActivePlayerUnit();

        if (user == null)
        {
            Say(turnManager != null && turnManager.BattleFinished
                ? "El combate ha terminado."
                : "No es el turno de un protagonista.", true);
            return;
        }

        if (itemMode)
        {
            CancelSelection();
            Say("Menu de objetos cerrado.");
            return;
        }

        CancelSelection();
        itemMode = true;
        Say("Elige un objeto. Usarlo consume tu accion.");
    }

    // Panel de consumibles permanente: 0 pocion, 1 antidoto. Reutiliza la
    // misma seleccion de objetivo y el mismo ConsumableService que el menu
    // de objetos anterior; no duplica ninguna regla.
    private void OnConsumableClicked(int slot)
    {
        BattleUnit user = ActivePlayerUnit();

        if (user == null)
        {
            // Fuera de turno solo se consulta la informacion.
            previewItem = previewItem == slot ? -1 : slot;
            Say(turnManager != null && turnManager.BattleFinished
                ? "El combate ha terminado."
                : "No es el turno de un protagonista.", true);
            return;
        }

        if (itemSelected == slot || previewItem == slot)
        {
            CancelSelection();
            Say("Seleccion cancelada. Elige otra accion.");
            return;
        }

        CancelSelection();

        // Sin existencias: solo se muestra su informacion.
        if (GameSession.GetCount(ItemType(slot)) <= 0)
        {
            previewItem = slot;
            Say("No te quedan " + ConsumableService.Name(ItemType(slot)) + ".", true);
            return;
        }

        OnItemSlotClicked(slot);

        // Si ningun aliado puede usarlo ahora (p. ej. vida completa) la
        // seleccion se rechaza, pero su informacion debe verse igualmente.
        if (itemSelected < 0)
            previewItem = slot;
    }

    private static GameSession.Consumable ItemType(int slot)
    {
        return slot == 0
            ? GameSession.Consumable.Potion
            : GameSession.Consumable.Antidote;
    }

    // Clic en uno de los cuatro botones mientras el menu de objetos esta
    // abierto: 0 pocion, 1 antidoto, 2 volver.
    private void OnItemSlotClicked(int slot)
    {
        if (slot == 2)
        {
            CancelSelection();
            Say("Menu de objetos cerrado.");
            return;
        }

        if (slot > 2)
            return;

        GameSession.Consumable type = ItemType(slot);

        validTargets.Clear();
        itemSelected = -1;

        string reason = "";

        for (int position = 1; position <= 4; position++)
        {
            BattleUnit ally = positionManager.GetUnitAtPosition(
                TeamSide.Player, position);

            if (ally == null)
                continue;

            if (ConsumableService.CanUse(type, ally, out string why))
                validTargets.Add(ally);
            else if (reason == "")
                reason = why;
        }

        if (validTargets.Count == 0)
        {
            Say(GameSession.GetCount(type) <= 0
                ? "No te quedan " + ConsumableService.Name(type) + "."
                : "Ningun aliado puede usar " +
                  ConsumableService.Name(type) + ". " + reason, true);
            return;
        }

        itemSelected = slot;
        Say("Elige un aliado para " + ConsumableService.Name(type) +
            " (o cancela).");
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
        Say(DisplayName(user) + " termina su turno.");
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

        // Con el menu de objetos abierto los botones son los objetos.
        if (itemMode)
        {
            OnItemSlotClicked(index);
            return;
        }

        // En modo movimiento los dos primeros botones son Avanzar / Retroceder.
        if (moveMode)
        {
            OnMoveDirectionClicked(index);
            return;
        }

        CharacterAbilityLoadout loadout =
            user.GetComponent<CharacterAbilityLoadout>();

        if (loadout == null || index >= loadout.Abilities.Count)
            return;

        AbilityData ability = loadout.Abilities[index];
        previewItem = -1;

        selectedAbility = null;
        validTargets.Clear();

        // Preparacion diferida: se confirma con un segundo clic.
        if (ability.requiresPreparation && pendingConfirm != ability)
        {
            CancelSelection();
            pendingConfirm = ability;
            Say(ability.abilityName + " requiere preparacion: se anuncia " +
                "ahora y impacta en el siguiente turno de " +
                DisplayName(user) + ". Pulsa de nuevo para confirmar.");
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

        // Objeto elegido: el aliado clicado lo recibe. Si el uso es
        // valido consume 1 objeto y la accion del turno; si no, nada.
        if (itemSelected >= 0)
        {
            BattleUnit itemUser = ActivePlayerUnit();

            if (itemUser == null)
            {
                CancelSelection();
                return;
            }

            if (unit == null || !validTargets.Contains(unit))
            {
                Say("Aliado no valido para ese objeto.", true);
                return;
            }

            if (ConsumableService.TryUse(
                    ItemType(itemSelected), unit, out string message))
            {
                CancelSelection();
                Say(message);
                turnManager.EndTurn();
            }
            else
            {
                Say(message, true);
            }

            return;
        }

        if (selectedAbility == null)
        {
            Say(unit == null
                ? "Posicion vacia."
                : DisplayName(unit) + ": " + unit.CurrentHealth + "/" +
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
            ? DisplayName(user) + " prepara " + ability.abilityName +
              ": impactara en su siguiente turno."
            : DisplayName(user) + " uso " + ability.abilityName + ".");

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
        if ((selectedAbility != null || itemMode || itemSelected >= 0) &&
            ActivePlayerUnit() == null)
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
            SetText(activeText, side + DisplayName(active));
            SetText(infoText,
                DisplayName(active) + "     HP " + active.CurrentHealth +
                " / " + active.MaxHealth + "     Posición P" + active.Position);
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

        if (victoryPanel != null)
        {
            bool showVictory = finished && banner == "VICTORIA" &&
                               GameSession.IsActive;

            if (victoryPanel.activeSelf != showVictory)
                victoryPanel.SetActive(showVictory);
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
            // Modo movimiento: los cuatro botones de habilidad se ocultan (los dos
            // cuadros de Avanzar / Retroceder viven en su propio contenedor).
            if (playerTurn && moveMode)
                continue;

            bool has = loadout != null && i < loadout.Abilities.Count;

            if (!has)
            {
                SetText(abilityLabels[i], playerTurn ? "-" : "");
                SetButtonState(abilityImages[i], ButtonState.Blocked, true);
                ShowAbilityIcon(i, null, true, 0);
                abilityLabels[i].color = DimTextColor;
                continue;
            }

            AbilityData ability = loadout.Abilities[i];
            int remaining = cooldowns != null
                ? cooldowns.GetRemainingTurns(ability) : 0;
            bool blocked = remaining > 0 ||
                !ability.CanUseFromPosition(active.Position);

            string status = remaining > 0
                ? "<color=#C9965A>Recarga " + remaining + "</color>"
                : (!ability.CanUseFromPosition(active.Position)
                    ? "<color=#C98A8A>Solo desde " + PositionList(ability.usablePositions) + "</color>"
                    : "");

            SetText(abilityLabels[i], status == "" ? ability.abilityName : ability.abilityName + "\n" + status);
            abilityLabels[i].color = blocked ? DimTextColor : TextColor;
            ShowAbilityIcon(i, ability, blocked, remaining);
            SetButtonState(abilityImages[i],
                (selectedAbility == ability || pendingConfirm == ability)
                    ? ButtonState.Selected
                    : (blocked ? ButtonState.Blocked : ButtonState.Normal), true);
        }

        // Detalle de la habilidad seleccionada.
        AbilityData detail = selectedAbility != null
            ? selectedAbility : pendingConfirm;

        // Consumible elegido (o consultado sin existencias).
        int infoItem = itemSelected >= 0 ? itemSelected : previewItem;

        bool showMoveInfo = moveMode || (moveHover && detail == null && infoItem < 0 && playerTurn);

        if (showMoveInfo)
        {
            SetText(abilityTitleText, "Movimiento táctico");

            if (moveMode && active != null)
            {
                SetText(detailText, MoveInfo + "\nPosición actual: P" + active.Position +
                    (moveDirection == 0 ? "." : "") +
                    (moveDirection == 0 ? "\nElige una dirección." : "\nPulsa de nuevo la dirección elegida para confirmar."));
            }
            else
            {
                SetText(detailText, MoveInfo);
            }
        }
        else if (infoItem >= 0)
        {
            SetText(abilityTitleText, ConsumableService.Name(ItemType(infoItem)));
            SetText(detailText, ItemDescription(infoItem) +
                (GameSession.GetCount(ItemType(infoItem)) <= 0
                    ? "\nSin existencias."
                    : (itemSelected == infoItem
                        ? "\nConsume 1 objeto y la acción del turno."
                        : "\nNo se puede usar ahora (sin objetivo válido o fuera de turno).")));
        }
        else
        {
            SetText(abilityTitleText, detail == null ? "Información" : detail.abilityName);
            SetText(detailText, detail == null
                ? (playerTurn ? "Selecciona una habilidad o un consumible." : "")
                : Describe(detail));
        }

        // Icono de la opcion consultada (habilidad real o icono provisional del consumible).
        if (infoIcon != null)
        {
            Sprite shown = null;

            if (infoItem >= 0)
                shown = consumableIcons != null && infoItem < consumableIcons.Length && consumableIcons[infoItem] != null
                    ? consumableIcons[infoItem]
                    : (art != null ? (infoItem == 0 ? art.potionIcon : art.antidoteIcon) : null);
            else if (detail != null && iconSet != null)
                shown = iconSet.Get(detail);

            if (infoIcon.sprite != shown)
                infoIcon.sprite = shown;

            if (infoIcon.enabled != (shown != null))
                infoIcon.enabled = shown != null;
        }


        if (itemsButton != null)
            itemsButton.interactable = playerTurn;

        // Preparacion de Meteorito (o similar) en curso.
        string prep = "";
        foreach (BattleUnit unit in allUnits)
        {
            if (unit == null) continue;
            PreparedAbilityController prepared =
                unit.GetComponent<PreparedAbilityController>();
            if (prepared != null && prepared.HasPreparation)
            {
                if (prepared.PreparedArea != null)
                    prep += DisplayName(unit) + " prepara " +
                            prepared.PreparedAbility.abilityName + " sobre " +
                            prepared.PreparedArea.label + " (" +
                            prepared.PreparedArea.PositionsText() +
                            "): impacta en su siguiente turno.  ";
                else
                    prep += DisplayName(unit) + " prepara " +
                            prepared.PreparedAbility.abilityName +
                            ": impacta en su siguiente turno.  ";
            }
        }
        SetText(prepText, prep);

        // Cambio de fase de un jefe: mensaje narrativo y rotulo temporal.
        if (phaseMessage != "")
        {
            Say(phaseMessage);
            phaseMessage = "";
        }

        // Segundo mensaje de una habilidad con frase de uso.
        if (followUpText != "" && Time.unscaledTime >= followUpAt)
        {
            Say(followUpText);
            followUpText = "";
        }

        if (phaseNotice != null)
        {
            bool showNotice = Time.unscaledTime < phaseNoticeUntil && !finished;

            if (phaseNotice.gameObject.activeSelf != showNotice)
                phaseNotice.gameObject.SetActive(showNotice);
        }

        // Mensajes.
        // Los mensajes caducan (los antiguos no se quedan en pantalla).
        bool showToast = feedback != "" && Time.unscaledTime - feedbackTime < (feedbackIsError ? 5f : 4f);
        SetText(messageText, feedback);
        // Error en ambar claro (legible sobre fondo oscuro), informacion en crema.
        messageText.color = feedbackIsError
            ? new Color(0.97f, 0.76f, 0.46f) : TextColor;

        if (toastRoot != null && toastRoot.activeSelf != showToast)
            toastRoot.SetActive(showToast);

        // El registro interno se conserva (oculto) para diagnostico.
        SetText(logText, string.Join("\n", recentLog.ToArray()));

        string hint;
        if (finished)
            hint = "";
        else if (moveMode)
            hint = moveDirection == 0 ? "Elige Avanzar o Retroceder. Esc para volver." : "Pulsa de nuevo la dirección para confirmar. Esc para volver.";
        else if (itemSelected >= 0)
            hint = "Elige un aliado resaltado para usar el objeto.";
        else if (selectedAbility != null)
            hint = "Elige un objetivo resaltado. Esc para cancelar.";
        else if (pendingConfirm != null)
            hint = "Pulsa de nuevo la habilidad para confirmar.";
        else if (playerTurn)
            hint = "";
        else
            hint = active != null && active.GetComponent<EnemyAIController>() != null
                ? "Turno enemigo: actua automaticamente."
                : "Turno enemigo: pulsa A para que ataque.";

        SetText(hintText, hint);

        RefreshHeader(active);
        RefreshConsumables(playerTurn);
        RefreshMoveOptions(active, playerTurn);

        // VOLVER vive dentro de las opciones de movimiento (solo visible en modo Mover) y regresa a las
        // habilidades sin gastar turno. Cancelar habilidades u objetos se hace con Esc o volviendo a pulsarlos.
        if (cancelButton != null)
            cancelButton.interactable = !finished;

        if (moveButton != null)
        {
            moveButton.interactable = playerTurn;
            SetButtonState(moveButtonImage, moveMode ? ButtonState.Selected : (playerTurn ? ButtonState.Normal : ButtonState.Blocked));
        }
        endTurnButton.interactable = playerTurn;

        // Casillas de unidades.
        for (int slot = 0; slot < SlotCount; slot++)
        {
            BattleUnit unit = TileUnit(slot);
            bool ally = slot < 4;
            int position = ally ? 4 - slot : slot - 3;
            string label = (ally ? "P" : "E") + position;

            // Posicion vacia: la tarjeta se oculta (el boton sigue existiendo).
            if (unit == null)
            {
                SetText(tileTexts[slot], label + "\n(vacio)");
                tileBars[slot].anchorMax = new Vector2(0f, 1f);
                SetText(tileStatus[slot], "");

                if (tileImages[slot].gameObject.activeSelf)
                    tileImages[slot].gameObject.SetActive(false);

                if (unitMarkers[slot] != null && unitMarkers[slot].gameObject.activeSelf)
                    unitMarkers[slot].gameObject.SetActive(false);

                continue;
            }

            if (!tileImages[slot].gameObject.activeSelf)
                tileImages[slot].gameObject.SetActive(true);

            SetText(tileTexts[slot],
                DisplayName(unit) + "  " + label +
                "\nHP " + unit.CurrentHealth + "/" + unit.MaxHealth);
            tileTexts[slot].color = TextColor;
            // La tarjeta de un jefe da mas espacio a los estados (menos al nombre).
            bool bossCard = unit.Data != null && unit.Data.isBoss;
            tileStatus[slot].rectTransform.anchorMin = new Vector2(bossCard ? 0.50f : 0.58f, 0.36f);

            bool threatened = IsThreatenedByPreparedArea(unit);
            SetText(tileStatus[slot],
                PhaseTag(unit) + (threatened ? "<color=#F28B3C>FUEGO</color> " : "") + StatusTags(unit));

            tileBars[slot].anchorMax = new Vector2(
                Mathf.Clamp01((float)unit.CurrentHealth / unit.MaxHealth), 1f);

            bool isTarget = validTargets.Contains(unit);
            bool isActive = unit == active && !finished;

            if (FlatStyle)
            {
                // Tarjeta plana: relleno azul oscuro y borde fino. Activo = borde dorado envejecido y fondo algo
                // mas claro; objetivo valido = borde dorado; amenaza de area = borde naranja.
                tileImages[slot].color = isTarget ? HoverNavy
                    : (isActive ? RaisedNavy : (ally ? AllyColor : EnemyColor));

                Outline tileOutline = tileImages[slot].GetComponent<Outline>();

                if (tileOutline != null)
                    tileOutline.effectColor = isTarget ? GoldBright
                        : (isActive ? GoldBright
                            : (threatened ? new Color(0.90f, 0.52f, 0.25f, 1f)
                                : CardLine));
            }
            else if (Skinned)
            {
                // Con marcos ilustrados el color de la tarjeta es un tinte del marco.
                tileImages[slot].color = isTarget ? new Color(1f, 0.82f, 0.50f, 1f)
                    : (isActive ? FrameSelected
                        : (threatened ? new Color(1f, 0.50f, 0.32f, 1f)
                            : (ally ? new Color(0.50f, 0.50f, 0.48f, 1f) : new Color(0.58f, 0.48f, 0.45f, 1f))));
                SetFill(tileImages[slot], isTarget ? HoverNavy : (isActive ? RaisedNavy : (ally ? AllyColor : EnemyColor)));
            }
            else
            {
                tileImages[slot].color = isActive ? ActiveColor : (ally ? AllyColor : EnemyColor);
            }

            if (tileBorders[slot] != null)
                tileBorders[slot].color = isTarget ? GoldBright
                    : (isActive ? GoldColor : new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.28f));

            PlaceUnitCard(slot, unit, isActive, isTarget);
        }
    }

    // ------------------------------------------------------------
    // Rediseno: cabecera, consumibles y tarjetas de unidad
    // ------------------------------------------------------------

    private static string PositionList(bool[] flags)
    {
        string text = "";

        for (int i = 0; flags != null && i < flags.Length; i++)
            if (flags[i]) text += (text == "" ? "" : ", ") + "P" + (i + 1);

        return text == "" ? "-" : text;
    }

    private static string ItemDescription(int slot)
    {
        // Texto generado por ConsumableService (mismas reglas que TryUse).
        return ConsumableService.Description(ItemType(slot));
    }

    private static string ZoneLabel(EnemyArchetypeData.Zone zone)
    {
        switch (zone)
        {
            case EnemyArchetypeData.Zone.Pradera: return "PRADERA";
            case EnemyArchetypeData.Zone.Bosque: return "BOSQUE";
            case EnemyArchetypeData.Zone.Corrupcion: return "CORRUPCION";
            default: return "JEFE";
        }
    }

    // Region, codigo del encuentro y orden de turnos (solo lectura).
    private void RefreshHeader(BattleUnit active)
    {
        if (encounterLoader == null)
            encounterLoader = FindAnyObjectByType<EncounterLoader>();

        EncounterData encounter = encounterLoader != null ? encounterLoader.Current : null;

        if (regionText != null)
        {
            SetText(regionText, encounter != null
                ? ZoneLabel(encounter.zone) + "  " + encounter.encounterId + "\n<size=19>" + encounter.displayName + "</size>"
                : "COMBATE");
        }

        List<BattleUnit> order = turnManager.GetTurnOrderSnapshot();
        int activeIndex = active != null ? order.IndexOf(active) : -1;
        int shown = 0;

        for (int i = 0; i < order.Count && shown < turnChips.Length; i++)
        {
            BattleUnit unit = order[i];

            if (unit == null || !unit.IsAlive)
                continue;

            bool ally = unit.Side == TeamSide.Player;
            bool acted = activeIndex >= 0 && i < activeIndex;
            bool isActive = i == activeIndex && !turnManager.BattleFinished;
            string abbr = Abbreviation(unit);

            if (!turnChips[shown].gameObject.activeSelf)
                turnChips[shown].gameObject.SetActive(true);

            SetText(turnChipTexts[shown], abbr);
            Color fill = ally ? new Color(0.095f, 0.130f, 0.185f, 1f) : new Color(0.150f, 0.105f, 0.140f, 1f);
            turnChips[shown].color = isActive ? Color.Lerp(fill, GoldBright, 0.14f) : (acted ? Dim(fill, 0.7f) : fill);
            Outline chipOutline = turnChips[shown].GetComponent<Outline>();
            if (chipOutline != null)
                chipOutline.effectColor = isActive ? GoldBright : (acted ? Dim(LineBlue, 0.6f) : LineBlue);
            turnChipTexts[shown].color = acted ? DimTextColor : (isActive ? Color.white : TextColor);
            shown++;
        }

        for (int i = shown; i < turnChips.Length; i++)
            if (turnChips[i].gameObject.activeSelf)
                turnChips[i].gameObject.SetActive(false);
    }

    // Cantidades reales del inventario y disponibilidad segun el turno.
    private void RefreshConsumables(bool playerTurn)
    {
        for (int i = 0; i < itemButtons.Length; i++)
        {
            if (itemButtons[i] == null)
                continue;

            GameSession.Consumable type = ItemType(i);
            int count = GameSession.GetCount(type);
            bool usable = playerTurn && count > 0;

            SetText(itemTexts[i], ConsumableService.Name(type));
            if (itemCounts[i] != null)
            {
                SetText(itemCounts[i], "x" + count);
                itemCounts[i].color = usable ? TextColor : DimTextColor;
            }
            itemTexts[i].color = usable ? TextColor : DimTextColor;
            Transform itemIcon = itemButtons[i].transform.Find("Icono");
            if (itemIcon != null)
                itemIcon.GetComponent<Image>().color = usable ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);
            SetButtonState(itemImages[i], (itemSelected == i || previewItem == i) ? ButtonState.Selected
                : (usable ? ButtonState.Normal : ButtonState.Blocked));
        }
    }

    // Estados compactos con duracion: SNG2 = sangrado, 2 turnos.
    private static string StatusTag(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Bleeding: return "SNG";
            case StatusEffectType.Burn: return "QUE";
            case StatusEffectType.Poison: return "VEN";
            case StatusEffectType.Mark: return "MAR";
            case StatusEffectType.Stun: return "ATU";
            case StatusEffectType.Weaken: return "DEB";
            case StatusEffectType.Vulnerability: return "VUL";
            case StatusEffectType.DefenseBoost: return "DEF+";
            case StatusEffectType.Guard: return "GRD";
            case StatusEffectType.PriorityTarget: return "OBJ";
            default: return type.ToString().Substring(0, Mathf.Min(3, type.ToString().Length)).ToUpper();
        }
    }

    // true si la unidad ocupa hoy una posicion amenazada por el area que un
    // rival tiene preparada (se calcula por posiciones, no por personajes).
    private bool IsThreatenedByPreparedArea(BattleUnit unit)
    {
        if (unit == null || !unit.IsAlive)
            return false;

        foreach (BattleUnit caster in allUnits)
        {
            if (caster == null || !caster.IsAlive || caster.Side == unit.Side)
                continue;

            PreparedAbilityController prepared = caster.GetComponent<PreparedAbilityController>();

            if (prepared != null && prepared.HasPreparation &&
                prepared.PreparedArea != null && prepared.PreparedArea.Contains(unit.Position))
                return true;
        }

        return false;
    }

    private static bool IsPositive(StatusEffectType type)
    {
        return type == StatusEffectType.DefenseBoost || type == StatusEffectType.Guard;
    }

    private string StatusTags(BattleUnit unit)
    {
        List<string> tags = new List<string>();

        // La preparacion pendiente es lo mas importante de un jefe: va primero.
        PreparedAbilityController prepared = unit.GetComponent<PreparedAbilityController>();

        if (prepared != null && prepared.HasPreparation)
            tags.Add("<color=#F1C879>PRE</color>");

        StatusEffectController effects = unit.GetComponent<StatusEffectController>();

        if (effects != null)
        {
            foreach (StatusEffectInstance effect in effects.GetActiveEffects())
            {
                string color = IsPositive(effect.Type) ? "#8FD3A0" : "#E58A7B";
                tags.Add("<color=" + color + ">" + StatusTag(effect.Type) + effect.RemainingTurns + "</color>");
            }
        }

        RegenerationEffect regeneration = unit.GetComponent<RegenerationEffect>();

        if (regeneration != null && regeneration.IsActive)
            tags.Add("<color=#8FD3A0>REG</color>");

        // Los jefes pueden acumular muchos estados: se muestran los primeros
        // y se resume el resto ("+2") para que la tarjeta siga legible.
        const int BossMaxTags = 4;
        int extra = 0;

        if (unit.Data != null && unit.Data.isBoss && tags.Count > BossMaxTags)
        {
            extra = tags.Count - BossMaxTags;
            tags.RemoveRange(BossMaxTags, extra);
        }

        string text = string.Join(" ", tags);

        if (extra > 0)
            text += " <color=#B9B2A3>+" + extra + "</color>";

        return text + (text == "" ? "" : " ");
    }

    // Coloca la tarjeta bajo los pies de la unidad y el marcador sobre su
    // cabeza. Con Match=Altura del escalador, la altura del lienzo equivale
    // a 2 x tamano ortografico de la camara, asi que la conversion no
    // depende de la resolucion.
    private void PlaceUnitCard(int slot, BattleUnit unit, bool isActive, bool isTarget)
    {
        Camera cam = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        if (cam == null || canvasRect == null)
            return;

        float ppu = canvasRect.rect.height / (2f * cam.orthographicSize);
        SpriteRenderer sr = unit.GetComponentInChildren<SpriteRenderer>();
        Vector3 center = unit.transform.position;
        float feet = sr != null ? sr.bounds.min.y + sr.bounds.size.y * 0.09f : center.y - 1.5f;
        float head = sr != null ? sr.bounds.max.y - sr.bounds.size.y * 0.06f : center.y + 1.5f;
        float x = (center.x - cam.transform.position.x) * ppu;

        RectTransform card = (RectTransform)tileImages[slot].transform;
        // La tarjeta nunca baja de los paneles inferiores (margen de 8 px sobre su borde).
        float cardY = (feet - 0.08f - cam.transform.position.y) * ppu - card.sizeDelta.y * 0.5f;
        float minCardY = -canvasRect.rect.height * 0.5f + canvasRect.rect.height * 0.297f + 8f + card.sizeDelta.y * 0.5f;
        card.anchoredPosition = new Vector2(x, Mathf.Max(cardY, minCardY));

        RectTransform marker = unitMarkers[slot];

        if (marker == null)
            return;

        bool show = isActive || isTarget;

        if (marker.gameObject.activeSelf != show)
            marker.gameObject.SetActive(show);

        if (show)
        {
            marker.anchoredPosition = new Vector2(x, (head - cam.transform.position.y) * ppu + (pointSprite != null ? 40f : 26f));
            marker.GetComponent<Image>().color = pointSprite != null
                ? (isTarget ? new Color(1f, 0.62f, 0.5f, 1f) : Color.white)
                : (isTarget ? GoldBright : GoldColor);
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

        string text = string.IsNullOrEmpty(ability.description) ? "" : ability.description + "\n";

        text += "Objetivo: " + target + "   Recarga: " + ability.cooldown;

        if (ability.targetType == AbilityTargetType.Single)
            text += "\nSe usa desde: " + PositionList(ability.usablePositions) +
                    "   Alcanza: " + PositionList(ability.targetPositions);
        else
            text += "\nSe usa desde: " + PositionList(ability.usablePositions);

        if (ability.requiresPreparation)
            text += "\nREQUIERE PREPARACION: impacta en el siguiente turno.";

        if (ability.userMovement != UserMovement.None)
            text += "\nMueve al usuario.";

        return text;
    }

    // ------------------------------------------------------------
    // Construccion de la interfaz
    // ------------------------------------------------------------

    private Font font;

    private RectTransform Box(string name, Transform parent, Vector2 min, Vector2 max, Color color, Sprite sprite = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().sprite = sprite;
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


    // ---- Estilo: sprites de Assets/Sprites/HUD (o MapArtSet como respaldo) ----

    // Aplica un sprite 9-slice. 'borderScale' = escala de los bordes (1 = tamano
    // del sprite; menor = bordes mas finos). Equivale a ajustar
    // Pixels Per Unit Multiplier para que ornamentos y esquinas no se deformen.
    private void Slice(Image image, Sprite sprite, float borderScale)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.01f, borderScale);
        image.color = FrameTint;
        // Material de UI que suaviza el dorado de los PNG (solo marcos y botones).
        if (frameMaterial != null)
            image.material = frameMaterial;

        // Solo se dibuja el marco del sprite; el interior es un relleno azul marino propio (hijo "Relleno",
        // inscrito dentro del borde) cuyo color depende del estado del elemento.
        image.fillCenter = false;

        float k = Mathf.Max(0.01f, image.pixelsPerUnit * image.pixelsPerUnitMultiplier);
        Vector4 b = sprite != null ? sprite.border / k * 0.5f : Vector4.zero;   // cubre la banda oscura interior del marco
        RectTransform fill = Box("Relleno", image.transform, Vector2.zero, Vector2.one, ButtonColor);
        fill.offsetMin = new Vector2(b.x, b.y);
        fill.offsetMax = new Vector2(-b.z, -b.w);
        fill.GetComponent<Image>().raycastTarget = false;
        fill.SetAsFirstSibling();
    }

    // Color del relleno interior de un elemento con marco (ver Slice).
    private static void SetFill(Image frame, Color color)
    {
        if (frame == null)
            return;

        Transform fill = frame.transform.Find("Relleno");

        if (fill == null)
            return;

        Image fillImage = fill.GetComponent<Image>();

        if (fillImage != null && fillImage.color != color)
            fillImage.color = color;
    }

    // Respaldo provisional (marcos de MapArtSet) cuando no hay sprites del HUD.
    private Image Skin(RectTransform rect, Color frameColor)
    {
        Image image = rect.GetComponent<Image>();

        if (art != null && art.roundedRect != null)
        {
            image.sprite = art.roundedRect;
            image.type = Image.Type.Sliced;
        }

        if (art == null || art.frame == null)
            return null;

        RectTransform border = Box("Borde", rect, Vector2.zero, Vector2.one, frameColor);
        Image borderImage = border.GetComponent<Image>();
        borderImage.sprite = art.frame;
        borderImage.type = Image.Type.Sliced;
        borderImage.raycastTarget = false;
        return borderImage;
    }

    // Panel con marco. 'sprite' null = panelSprite.
    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color,
        Sprite sprite = null, float borderScale = 0.34f)
    {
        RectTransform rect = Box(name, parent, min, max, color);
        sprite = sprite != null ? sprite : panelSprite;

        // Los contenedores principales conservan el marco ornamental fino del sprite; los elementos
        // secundarios usan bordes planos (ver SkinButton y las tarjetas).
        if (OrnateContainers && sprite != null)
        {
            Slice(rect.GetComponent<Image>(), sprite, borderScale);
            SetFill(rect.GetComponent<Image>(), color);
        }
        else if (FlatStyle)
            AddFlatBorder(rect.gameObject, AgedGold, false, 2f);
        else
            Skin(rect, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.85f));

        return rect;
    }

    // Boton con marco: el estado visual lo fija SetButtonState; el cursor
    // encima solo aclara el color.
    private Button SkinButton(string name, Transform parent, Vector2 min, Vector2 max,
        Color color, out Text label, out Image image, int size, Color frameColor,
        float borderScale = 0.28f, Sprite frame = null)
    {
        Button button = MakeButton(name, parent, min, max, color, out label, out image, size);

        if (FlatStyle)
        {
            // Boton plano: el estado lo pinta SetButtonState. Borde dorado envejecido solo en los botones
            // principales (los que se crean con el dorado "normal"); el resto, azul grisaceo.
            button.transition = Selectable.Transition.None;
            AddFlatBorder(image.gameObject, frameColor.a >= 0.8f ? AgedGold : LineBlue, true);
        }
        else if (Skinned)
        {
            Slice(image, frame != null ? frame : buttonNormal, borderScale);
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.10f, 1.04f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            button.colors = colors;
        }
        else
        {
            Skin(image.rectTransform, frameColor);
        }

        return button;
    }

    // Linea fina dorada (Image sencilla).
    private void Divider(Transform parent, float y, float xMin = 0.06f, float xMax = 0.94f)
    {
        RectTransform line = Box("Separador", parent, new Vector2(xMin, y), new Vector2(xMax, y),
            FlatStyle ? LineBlue : new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.55f));
        line.offsetMin = new Vector2(0, -1f);
        line.offsetMax = new Vector2(0, 1f);
        line.GetComponent<Image>().raycastTarget = false;
    }

    // El texto se reduce (hasta un minimo) en lugar de desbordar su marco.
    private static void Fit(Text text, int min, int max)
    {
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = min;
        text.resizeTextMaxSize = max;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private static Font serifFont;

    // Fuente con serifa del sistema para parecerse a la referencia; si no
    // existe, se queda la fuente integrada.
    private Font ResolveFont()
    {
        if (serifFont == null)
            serifFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Palatino Linotype", "Times New Roman" }, 24);

        return serifFont != null ? serifFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // Las fuentes del SO no se serializan en la escena: se reasignan al iniciar.
    private void ApplyFont()
    {
        font = ResolveFont();

        foreach (Text text in GetComponentsInChildren<Text>(true))
            text.font = font;
    }

    // Iconos de los consumibles: sprites del proyecto "curacion" y "antidoto" (se asignan al construir en el editor).
    private void EnsureConsumableIcons()
    {
#if UNITY_EDITOR
        if (consumableIcons == null || consumableIcons.Length < 2)
            consumableIcons = new Sprite[2];

        string[] paths = { "Assets/Sprites/Iconos/curacion.png", "Assets/Sprites/Iconos/antidoto.png" };
        string[] subNames = { "curacion_0", "antidoto_1" };

        for (int i = 0; i < 2; i++)
        {
            if (consumableIcons[i] != null)
                continue;

            foreach (UnityEngine.Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(paths[i]))
            {
                if (asset is Sprite sprite && sprite.name == subNames[i])
                    consumableIcons[i] = sprite;
            }

            if (consumableIcons[i] == null)
                consumableIcons[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
        }
#endif
    }

    // Genera (o regenera) toda la interfaz bajo este GameObject. Distribucion
    // calcada de la referencia visual (1920x1080): cabecera, mensaje central,
    // tarjetas bajo cada unidad y tres paneles inferiores (consumibles,
    // personaje y habilidades, informacion y acciones). Todo vive dentro de un
    // contenedor 16:9 centrado, asi las pantallas mas anchas solo amplian el
    // escenario.
    public void BuildUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        font = ResolveFont();

        Camera cam = null;
        foreach (Camera c in FindObjectsByType<Camera>())
            if (c.gameObject.scene == gameObject.scene) cam = c;

        GameObject canvasGo = new GameObject("Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvasRect = (RectTransform)canvasGo.transform;

        if (cam != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            cam.backgroundColor = new Color(0.095f, 0.125f, 0.17f, 0f);
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        canvas.sortingOrder = 100;

        // Referencia 1920x1080 con ajuste por altura.
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        // Contenedor 16:9 centrado.
        GameObject frameGo = new GameObject("Marco", typeof(RectTransform));
        frameGo.transform.SetParent(canvasGo.transform, false);
        RectTransform frameRect = (RectTransform)frameGo.transform;
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(1920f, 1080f);
        frameRect.anchoredPosition = Vector2.zero;
        Transform root = frameGo.transform;

        Color gold = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.85f);
        Color goldDim = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.45f);

        // ---------------- Cabecera ----------------
        RectTransform top = Box("BarraSuperior", root, new Vector2(0.005f, 0.915f), new Vector2(0.995f, 0.995f), BackColor);
        RectTransform topLine = Box("LineaDorada", top, Vector2.zero, new Vector2(1, 0), gold);
        topLine.offsetMax = new Vector2(0, 3);

        if (OrnateContainers && headerSprite != null)
        {
            Slice(top.GetComponent<Image>(), headerSprite, 0.40f);
            SetFill(top.GetComponent<Image>(), BackColor);
            topLine.gameObject.SetActive(false);
        }
        else if (FlatStyle)
        {
            AddFlatBorder(top.gameObject, AgedGold, false, 2f);
            topLine.gameObject.SetActive(false);
        }

        // Divisiones discretas entre encuentro, ronda/turno y orden de turnos.
        foreach (float dividerX in new[] { 0.292f, 0.552f })
        {
            RectTransform vLine = Box("Division", top, new Vector2(dividerX, 0.20f), new Vector2(dividerX, 0.80f), LineBlue);
            vLine.offsetMin = new Vector2(-1f, 0f);
            vLine.offsetMax = new Vector2(1f, 0f);
            vLine.GetComponent<Image>().raycastTarget = false;
        }

        // Sin Coronas en el combate: el espacio liberado se reparte entre encuentro, ronda/turno y orden de turnos.
        regionText = Label("Region", top, new Vector2(0.02f, 0.05f), new Vector2(0.275f, 0.95f), 28, TextAnchor.MiddleLeft, GoldBright);
        regionText.fontStyle = FontStyle.Bold;
        regionText.supportRichText = true;
        regionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        regionText.verticalOverflow = VerticalWrapMode.Truncate;
        roundText = Label("Ronda", top, new Vector2(0.315f, 0.50f), new Vector2(0.54f, 0.95f), 28, TextAnchor.MiddleLeft, TextColor);
        roundText.fontStyle = FontStyle.Bold;
        roundText.horizontalOverflow = HorizontalWrapMode.Overflow;
        activeText = Label("TurnoActivo", top, new Vector2(0.315f, 0.05f), new Vector2(0.54f, 0.50f), 24, TextAnchor.MiddleLeft, GoldBright);
        activeText.horizontalOverflow = HorizontalWrapMode.Overflow;

        for (int i = 0; i < turnChips.Length; i++)
        {
            float x0 = 0.565f + i * 0.0525f;
            // Fichas simples: solo la activa lleva borde dorado.
            RectTransform chip = Box("Turno_" + i, top, new Vector2(x0, 0.20f), new Vector2(x0 + 0.0475f, 0.80f), SurfaceColor);
            turnChips[i] = chip.GetComponent<Image>();
            Outline chipOutline = chip.gameObject.AddComponent<Outline>();
            chipOutline.effectDistance = new Vector2(1.5f, -1.5f);
            chipOutline.effectColor = LineBlue;

            turnChipTexts[i] = Label("Texto", chip, Vector2.zero, Vector2.one, 19, TextAnchor.MiddleCenter, TextColor);
            turnChipTexts[i].fontStyle = FontStyle.Bold;
            turnChipTexts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            chip.gameObject.SetActive(false);
        }

        // Las Coronas no se muestran en el combate (la economia no cambia).
        resourcesText = null;

        // ---------------- Unidades (tarjetas) y marcadores ----------------
        RectTransform units = Box("Unidades", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0));
        units.GetComponent<Image>().raycastTarget = false;

        for (int slot = 0; slot < SlotCount; slot++)
        {
            bool ally = slot < 4;
            RectTransform tile = Box("Casilla_" + slot, units, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), ally ? AllyColor : EnemyColor);
            tile.pivot = new Vector2(0.5f, 0.5f);
            tile.sizeDelta = new Vector2(202f, 98f);
            tile.anchoredPosition = new Vector2(ally ? -600f + slot * 200f : 200f + (slot - 4) * 200f, -330f);
            tileImages[slot] = tile.GetComponent<Image>();

            if (FlatStyle)
            {
                AddFlatBorder(tile.gameObject, CardLine, false);
                tileBorders[slot] = null;
            }
            else if (cardSprite != null || panelSprite != null)
            {
                Slice(tileImages[slot], cardSprite != null ? cardSprite : panelSprite, 0.24f);
                tileBorders[slot] = null;
            }
            else
            {
                tileBorders[slot] = Skin(tile, goldDim);
            }

            Button b = tile.gameObject.AddComponent<Button>();
            b.targetGraphic = tileImages[slot];
            b.transition = Selectable.Transition.None;
            tileButtons[slot] = b;

            tileTexts[slot] = Label("Texto", tile, new Vector2(0.07f, 0.38f), new Vector2(0.58f, 0.95f), 18, TextAnchor.UpperLeft, TextColor);
            Fit(tileTexts[slot], 12, 18);
            RectTransform barBg = Box("BarraFondo", tile, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.32f), new Color(0.04f, 0.05f, 0.07f, 1f));
            tileBars[slot] = Box("Vida", barBg, Vector2.zero, Vector2.one, ally ? HealthColor : HealthEnemyColor);
            AddFlatBorder(barBg.gameObject, CardLine, false);
            tileStatus[slot] = Label("Estado", tile, new Vector2(0.58f, 0.36f), new Vector2(0.96f, 0.96f), 14, TextAnchor.UpperRight, TextColor);
            tileStatus[slot].supportRichText = true;
            Fit(tileStatus[slot], 9, 14);
        }

        RectTransform markers = Box("Marcadores", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0));
        markers.GetComponent<Image>().raycastTarget = false;

        for (int slot = 0; slot < SlotCount; slot++)
        {
            RectTransform marker = Box("Marcador_" + slot, markers, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pointSprite != null ? Color.white : GoldColor,
                pointSprite != null ? pointSprite : (art != null ? art.triangle : null));
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = pointSprite != null ? new Vector2(44f, 49f) : new Vector2(32f, 26f);
            marker.localRotation = Quaternion.Euler(0f, 0f, pointSprite != null ? 0f : 180f);
            marker.GetComponent<Image>().preserveAspect = true;
            marker.GetComponent<Image>().raycastTarget = false;
            unitMarkers[slot] = marker;
            marker.gameObject.SetActive(false);
        }

        // ---------------- Mensaje contextual ----------------
        RectTransform toast = Box("Mensajes", root, new Vector2(0.275f, 0.832f), new Vector2(0.725f, 0.895f), new Color(0.075f, 0.098f, 0.141f, 1f));

        if (FlatStyle)
            AddFlatBorder(toast.gameObject, AgedGold, false);
        else if (headerSprite != null)
        {
            Slice(toast.GetComponent<Image>(), headerSprite, 0.30f);
            SetFill(toast.GetComponent<Image>(), PanelColor);
        }
        else
            Skin(toast, goldDim);

        messageText = Label("Mensaje", toast, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), 22, TextAnchor.MiddleCenter, TextColor);
        Fit(messageText, 13, 22);
        toastRoot = toast.gameObject;
        toastRoot.SetActive(false);

        // Registro interno (oculto): se conserva para diagnostico.
        RectTransform logRoot = Box("Registro", root, Vector2.zero, new Vector2(0.01f, 0.01f), new Color(0, 0, 0, 0));
        logText = Label("Texto", logRoot, Vector2.zero, Vector2.one, 12, TextAnchor.LowerLeft, DimTextColor);
        logRoot.gameObject.SetActive(false);

        prepText = Label("Preparacion", root, new Vector2(0.20f, 0.765f), new Vector2(0.80f, 0.805f), 16, TextAnchor.MiddleCenter, new Color(0.95f, 0.78f, 0.50f));
        bannerText = Label("Banner", root, new Vector2(0.2f, 0.62f), new Vector2(0.8f, 0.76f), 80, TextAnchor.MiddleCenter, Color.white);
        bannerText.fontStyle = FontStyle.Bold;

        // ---------------- Panel inferior izquierdo: consumibles ----------------
        EnsureConsumableIcons();
        RectTransform items = Panel("PanelConsumibles", root, new Vector2(0.005f, 0.017f), new Vector2(0.227f, 0.297f), PanelColor, headerSprite, 0.40f);
        Text itemsTitle = Label("Titulo", items, new Vector2(0.08f, 0.79f), new Vector2(0.95f, 0.95f), 24, TextAnchor.MiddleLeft, GoldBright);
        itemsTitle.fontStyle = FontStyle.Bold;
        itemsTitle.text = "CONSUMIBLES";

        bool sk = Skinned;

        for (int i = 0; i < 2; i++)
        {
            float y0 = i == 0 ? 0.456f : 0.110f;
            float y1 = i == 0 ? 0.753f : 0.420f;
            Text rowLabel; Image rowImage;
            itemButtons[i] = SkinButton(i == 0 ? "Pocion" : "Antidoto", items, new Vector2(0.07f, y0), new Vector2(0.93f, y1),
                ButtonColor, out rowLabel, out rowImage, 20, goldDim, 0.28f);
            itemImages[i] = rowImage;
            rowLabel.gameObject.SetActive(false);

            Sprite itemSprite = consumableIcons != null && i < consumableIcons.Length && consumableIcons[i] != null
                ? consumableIcons[i]
                : (art != null ? (i == 0 ? art.potionIcon : art.antidoteIcon) : null);
            RectTransform icon = Box("Icono", itemButtons[i].transform, new Vector2(0.03f, 0.08f), new Vector2(0.21f, 0.92f), Color.white, itemSprite);
            Image iconImage = icon.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.enabled = iconImage.sprite != null;

            // Solo icono, nombre y cantidad (los detalles van en el panel derecho).
            itemTexts[i] = Label("Nombre", itemButtons[i].transform, new Vector2(0.27f, 0.08f), new Vector2(0.78f, 0.92f), 21, TextAnchor.MiddleLeft, TextColor);
            itemTexts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            itemCounts[i] = Label("Cantidad", itemButtons[i].transform, new Vector2(0.78f, 0.08f), new Vector2(0.92f, 0.92f), 21, TextAnchor.MiddleRight, TextColor);
            itemCounts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // ---------------- Panel inferior central: personaje y habilidades ----------------
        RectTransform bottom = Panel("PanelPersonaje", root, new Vector2(0.234f, 0.017f), new Vector2(0.746f, 0.297f), PanelColor, headerSprite, 0.40f);
        infoText = Label("Info", bottom, new Vector2(0.035f, 0.78f), new Vector2(0.965f, 0.95f), 24, TextAnchor.MiddleLeft, TextColor);
        infoText.fontStyle = FontStyle.Bold;
        infoText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // Cuatro habilidades en una sola fila, mismo tamano y espaciado uniforme.
        for (int i = 0; i < AbilitySlots; i++)
        {
            float x0 = 0.030f + i * 0.2395f;
            abilityButtons[i] = SkinButton("Habilidad_" + i, bottom, new Vector2(x0, 0.09f), new Vector2(x0 + 0.2250f, 0.74f),
                ButtonColor, out abilityLabels[i], out abilityImages[i], 19, goldDim, 0.35f, abilityFrameSprite);
            abilityLabels[i].supportRichText = true;

            // Icono arriba (sin deformar), nombre y estado debajo.
            RectTransform iconRect = Box("Icono", abilityButtons[i].transform, new Vector2(0.20f, 0.36f), new Vector2(0.80f, 0.97f), Color.white);
            Image iconImage = iconRect.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.enabled = false;
            abilityIcons[i] = iconImage;

            Text cooldown = Label("Recarga", iconRect, Vector2.zero, Vector2.one, 30, TextAnchor.MiddleCenter, Color.white);
            cooldown.fontStyle = FontStyle.Bold;
            cooldown.horizontalOverflow = HorizontalWrapMode.Overflow;
            Outline outline = cooldown.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            abilityCooldowns[i] = cooldown;

            RectTransform labelRect = abilityLabels[i].rectTransform;
            labelRect.anchorMin = new Vector2(0.04f, 0.03f);
            labelRect.anchorMax = new Vector2(0.96f, 0.34f);
            abilityLabels[i].alignment = TextAnchor.MiddleCenter;
            Fit(abilityLabels[i], 12, 19);
        }
        // Contenedor propio de movimiento: solo dos cuadros iguales, centrados y
        // con separacion uniforme (aparece unicamente en modo Mover).
        RectTransform moveBox = Box("OpcionesMovimiento", bottom, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.76f), new Color(0, 0, 0, 0));
        moveBox.GetComponent<Image>().raycastTarget = false;
        moveContainer = moveBox.gameObject;

        for (int i = 0; i < 2; i++)
        {
            // Tres cuadros: Avanzar, Retroceder y Volver (mas estrecho).
            float x0 = i == 0 ? 0.01f : 0.375f;
            Text dirLabel; Image dirImage;
            moveDirButtons[i] = SkinButton(i == 0 ? "Avanzar" : "Retroceder", moveBox, new Vector2(x0, 0.02f), new Vector2(x0 + 0.35f, 0.98f),
                ButtonColor, out dirLabel, out dirImage, 26, goldDim, 0.35f, abilityFrameSprite);
            moveDirImages[i] = dirImage;
            moveDirTexts[i] = dirLabel;
            dirLabel.supportRichText = true;
            dirLabel.alignment = TextAnchor.MiddleCenter;
            dirLabel.fontStyle = FontStyle.Normal;
            RectTransform lr = dirLabel.rectTransform;
            lr.anchorMin = new Vector2(0.06f, 0.08f);
            lr.anchorMax = new Vector2(0.94f, 0.92f);
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            dirLabel.resizeTextForBestFit = false;
            dirLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            dirLabel.verticalOverflow = VerticalWrapMode.Truncate;
        }

        // Volver: regresa a las habilidades sin gastar turno (mismo metodo que Esc).
        Text backLabel; Image backImage;
        cancelButton = SkinButton("Volver", moveBox, new Vector2(0.74f, 0.02f), new Vector2(0.99f, 0.98f),
            ButtonColor, out backLabel, out backImage, 26, goldDim, 0.35f, abilityFrameSprite);
        backLabel.text = "Volver";
        backLabel.alignment = TextAnchor.MiddleCenter;
        backLabel.fontStyle = FontStyle.Bold;
        cancelLabel = backLabel;

        moveContainer.SetActive(false);


        // ---------------- Panel inferior derecho: informacion y acciones ----------------
        RectTransform info = Panel("PanelInformacion", root, new Vector2(0.752f, 0.017f), new Vector2(0.996f, 0.297f), PanelColor, headerSprite, 0.40f);
        // Panel sencillo: titulo, texto, separador y dos botones (sin iconos ni boton de cancelar).
        abilityTitleText = Label("TituloHabilidad", info, new Vector2(0.07f, 0.80f), new Vector2(0.93f, 0.95f), 26, TextAnchor.MiddleLeft, GoldBright);
        abilityTitleText.fontStyle = FontStyle.Bold;
        Fit(abilityTitleText, 16, 26);
        infoIcon = null;

        detailText = Label("Detalle", info, new Vector2(0.07f, 0.47f), new Vector2(0.93f, 0.79f), 18, TextAnchor.UpperLeft, TextColor);
        Fit(detailText, 11, 18);
        Divider(info, 0.455f, 0.07f, 0.93f);
        hintText = Label("Ayuda", info, new Vector2(0.07f, 0.285f), new Vector2(0.93f, 0.44f), 18, TextAnchor.MiddleLeft, TextColor);
        Fit(hintText, 12, 18);

        Text t2, t3; Image i2, i3;

        // Mover (sustituye al antiguo Cancelar permanente) y Terminar turno.
        moveButton = SkinButton("Mover", info, new Vector2(0.07f, 0.07f), new Vector2(0.485f, 0.25f), ButtonColor, out t3, out i3, 21, gold, 0.28f);
        t3.text = "Mover";
        moveButtonImage = i3;
        moveButton.gameObject.AddComponent<MapHoverTarget>();

        endTurnButton = SkinButton("TerminarTurno", info, new Vector2(0.515f, 0.07f), new Vector2(0.93f, 0.25f), ButtonColor, out t2, out i2, 21, gold, 0.28f);
        t2.text = "Terminar turno";

        // ---------------- Derrota y victoria ----------------
        RectTransform defeat = Panel("PanelDerrota", root, new Vector2(0.38f, 0.50f), new Vector2(0.62f, 0.59f), PanelColor, headerSprite, 0.40f);
        Text returnLabel; Image returnImage;
        returnToMenuButton = SkinButton("VolverMenu", defeat, new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f), ButtonColor, out returnLabel, out returnImage, 28, gold, 0.24f);
        returnLabel.text = "Volver al menú";
        defeatPanel = defeat.gameObject;
        defeatPanel.SetActive(false);

        RectTransform victory = Panel("PanelVictoria", root, new Vector2(0.38f, 0.50f), new Vector2(0.62f, 0.59f), PanelColor, headerSprite, 0.40f);
        Text continueLabel; Image continueImage;
        continueButton = SkinButton("Continuar", victory, new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.86f), ButtonColor, out continueLabel, out continueImage, 28, gold, 0.24f);
        continueLabel.text = "Continuar";
        victoryPanel = victory.gameObject;
        victoryPanel.SetActive(false);

        // Fuente con serifa en todo el HUD.
        foreach (Text text in canvasGo.GetComponentsInChildren<Text>(true))
            text.font = font;

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
