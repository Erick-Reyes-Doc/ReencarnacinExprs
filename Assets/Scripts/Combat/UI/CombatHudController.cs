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

    // Pausa del combate: reutiliza el menu de pausa del mapa (MapPauseMenu, prefab MenuPausa) con Continuar y
    // Menu principal. El escudo del encabezado es el boton que lo abre.
    [SerializeField] private MapPauseMenu pauseMenu;
    [SerializeField] private HudFrameStyle shieldHover;
    [SerializeField] private Button shieldButton;
    [SerializeField] private Outline shieldGlow;
    private bool combatFrozen;
    private float timeScaleBeforePause = 1f;

    private bool paused => pauseMenu != null && pauseMenu.IsOpen;
    public bool IsPaused => paused;

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

    // HUD de combate nuevo (Assets/Sprites/UICombate+). Se cargan al construir en el editor y se guardan en la escena.
    [Header("HUD nuevo (UICombate+)")]
    [SerializeField] private Sprite encounterSprite;
    [SerializeField] private Sprite turnPlateSprite;
    [SerializeField] private Sprite turnNormalSprite;
    [SerializeField] private Sprite turnActiveSprite;
    [SerializeField] private Sprite playerInfoSprite;
    [SerializeField] private Sprite consumablesFrameSprite;
    [SerializeField] private Sprite abilitiesFrameSprite;
    [SerializeField] private Sprite infoFrameSprite;
    [SerializeField] private Text cooldownText;
    [SerializeField] private Sprite abilityNormalSprite;
    [SerializeField] private Sprite abilityHoverSprite;
    [SerializeField] private Sprite moveSprite;
    [SerializeField] private Sprite endTurnSprite;
    [SerializeField] private Sprite typeIconSprite;
    [SerializeField] private Sprite rangeIconSprite;
    [SerializeField] private Sprite moveIconSprite;
    [SerializeField] private Text statsText;
    [SerializeField] private Image[] turnFrames = new Image[8];
    [SerializeField] private RectTransform[] groundOvals = new RectTransform[SlotCount];
    [SerializeField] private RectTransform[] activeLines = new RectTransform[SlotCount];
    [SerializeField] private Sprite selectionSprite;

    [Header("Indicador de turno activo (sprite 'seleccion')")]
    [Tooltip("Ancho minimo del indicador (1 = ancho normal).")]
    [SerializeField] private float selectionMinScale = 1f;
    [Tooltip("Ancho maximo del indicador (1.1 = 110 %). Solo cambia el ancho, nunca la altura.")]
    [SerializeField] private float selectionMaxScale = 1.10f;
    [Tooltip("Duracion en segundos de un ciclo completo (expandir y contraer).")]
    [SerializeField] private float selectionPeriod = 1f;
    [Tooltip("Ancho en pixeles del indicador dorado (igual que el marco rojo de objetivos).")]
    [SerializeField] private float selectionWidth = 175f;
    [Tooltip("Desplazamiento vertical del indicador respecto al centro de la barra (negativo = mas abajo).")]
    [SerializeField] private float selectionOffsetY = -20f;
    [Header("Iconos de estados (fila sobre la barra de vida)")]
    [Tooltip("Tamano en pixeles de cada icono.")]
    [SerializeField] private float statusIconSize = 26f;
    [Tooltip("Separacion en pixeles entre iconos.")]
    [SerializeField] private float statusIconSpacing = 2f;
    [Tooltip("Distancia en pixeles entre el borde superior de la barra y la fila de iconos.")]
    [SerializeField] private float statusIconGap = 10f;
    // Orden: Sangrado, Veneno, Quemadura, Debilitamiento, Vulnerabilidad, Proteccion, Regeneracion, Aturdimiento, Marca, Defensa elevada, Preparacion.
    [SerializeField] private Sprite[] statusIcons = new Sprite[StatusIconCount];
    private const int StatusIconCount = 11;
    [SerializeField] private RectTransform statusTip;
    [SerializeField] private Text statusTipText;
    private int statusTipSlot = -1;

    [SerializeField] private Sprite allyFrameSprite;
    [SerializeField] private RectTransform[] allyFrames = new RectTransform[4];
    private readonly List<BattleUnit> allyBenefitTargets = new List<BattleUnit>();
    private bool allyBenefitGroup;   // true: habilidad grupal (un solo tramo continuo); false: un indicador por objetivo

    [SerializeField] private Sprite targetFrameSprite;
    [SerializeField] private Sprite skullSprite;

    [Header("Objetivos validos (marco rojo) y objetivo señalado (calavera)")]
    [Tooltip("Ancho en pixeles del marco rojo.")]
    [SerializeField] private float targetFrameWidth = 175f;
    [Tooltip("Desplazamiento vertical del marco rojo respecto al centro de la barra (negativo = mas abajo).")]
    [SerializeField] private float targetFrameOffsetY = -20f;
    [Tooltip("Velocidad horizontal de la calavera dorada (pixeles por segundo).")]
    [SerializeField] private float skullSpeed = 1500f;
    [Tooltip("Ancho en pixeles de la calavera dorada.")]
    [SerializeField] private float skullWidth = 60f;
    [Tooltip("Desplazamiento vertical de la calavera respecto al centro de la barra (negativo = debajo).")]
    [SerializeField] private float skullOffsetY = -56f;

    [SerializeField] private RectTransform[] targetFrames = new RectTransform[SlotCount];
    [SerializeField] private RectTransform[] targetZones = new RectTransform[SlotCount];
    [SerializeField] private Button[] targetZoneButtons = new Button[SlotCount];
    [SerializeField] private RectTransform skull;
    private BattleUnit focusTarget;
    [SerializeField] private RectTransform hpTip;
    [SerializeField] private Text hpTipText;
    private int hpTipSlot = -1;
    [SerializeField] private GameObject[] statRows = new GameObject[3];
    [SerializeField] private Text[] statTexts = new Text[3];

    private bool NewHud => playerInfoSprite != null && abilityNormalSprite != null;

    // Retratos provisionales del orden de turnos: para usar uno definitivo, anadir aqui el nombre de la unidad
    // (UnitData.unitName) y su sprite.
    [System.Serializable]
    public class PortraitOverride
    {
        public string unitName;
        public Sprite portrait;
    }

    [SerializeField] private PortraitOverride[] portraitOverrides = new PortraitOverride[0];
    [SerializeField] private Image[] turnPortraits = new Image[8];

    private const float HeaderHeight = 156f;
    private const float TurnAreaCenter = 1505f;   // centro del area derecha (de la division a la orilla)
    private const float TurnPitch = 88f;

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

        // Botones con sprites ilustrados: el sprite cambia entre normal y resaltado (seleccion o cursor encima).
        HudFrameStyle spriteStyle = image.GetComponent<HudFrameStyle>();

        if (spriteStyle != null && spriteStyle.normalSprite != null)
        {
            bool over = spriteStyle.Hovered && state != ButtonState.Blocked;
            Sprite wanted = state == ButtonState.Selected || over ? spriteStyle.hoverSprite : spriteStyle.normalSprite;

            if (image.sprite != wanted)
                image.sprite = wanted;

            image.color = state == ButtonState.Blocked ? new Color(0.55f, 0.55f, 0.58f, 1f) : Color.white;
            return;
        }

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
        ConnectPauseMenu();
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
        DisconnectPauseMenu();
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
        phaseNoticeUntil = Time.time + PhaseNoticeSeconds;
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
            followUpAt = Time.time + 1.8f;
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

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !CombatIntroTransition.IsPlaying)
        {
            // Pausa: Esc abre o cierra el menu (Esc dentro del menu vuelve atras / reanuda). Sin pausa, cancela
            // primero una seleccion pendiente (comportamiento de siempre) y, si no hay ninguna, abre la pausa.
            if (paused)
                pauseMenu.HandleEscape();
            else if (HasPendingSelection())
                OnCancelClicked();
            else
                OpenPause();
        }

        UpdateShieldGlow();

        // En pausa el combate y su interfaz no avanzan (el menu se encarga de sus propios botones).
        if (paused)
            return;

        Refresh();
        AnimateSelection();
    }

    // Pulso suave y continuo del indicador dorado: solo el ancho, entre selectionMinScale y selectionMaxScale.
    private void AnimateSelection()
    {
        // El marco rojo de objetivos validos es estatico (sin animacion).
        float wave = 0.5f - 0.5f * Mathf.Cos(Time.time * 2f * Mathf.PI / Mathf.Max(0.05f, selectionPeriod));
        float scaleX = Mathf.Lerp(selectionMinScale, selectionMaxScale, wave);

        // El indicador verde de aliados tampoco se anima (sin pulso).

        if (selectionSprite == null)
            return;

        for (int slot = 0; slot < SlotCount; slot++)
        {
            RectTransform marker = activeLines[slot];

            if (marker != null && marker.gameObject.activeSelf)
                marker.localScale = new Vector3(scaleX, 1f, 1f);
        }
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

            // Zona sobre el sprite del objetivo valido: mismo clic que la barra.
            if (targetZoneButtons[i] != null)
            {
                targetZoneButtons[i].onClick.RemoveAllListeners();
                targetZoneButtons[i].onClick.AddListener(
                    () => OnTileClicked(slot)
                );
            }
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

        // El escudo del encabezado abre el menu de pausa.
        if (shieldButton != null)
        {
            shieldButton.onClick.RemoveAllListeners();
            shieldButton.onClick.AddListener(() => OpenPause());
        }
    }

    // ------------------------------------------------------------
    // Pausa
    // ------------------------------------------------------------

    // Enlaza el menu de pausa de la escena (prefab MenuPausa, el mismo del mapa).
    private void ConnectPauseMenu()
    {
        if (pauseMenu == null)
            pauseMenu = FindAnyObjectByType<MapPauseMenu>(FindObjectsInactive.Include);

        if (pauseMenu == null)
            return;

        pauseMenu.Opened -= FreezeCombat;
        pauseMenu.Closed -= UnfreezeCombat;
        pauseMenu.Opened += FreezeCombat;
        pauseMenu.Closed += UnfreezeCombat;
        pauseMenu.ExitToMenuHandler = ReturnToMenu;   // descarta la sesion y carga el menu (flujo de siempre)
    }

    private void DisconnectPauseMenu()
    {
        if (pauseMenu != null)
        {
            pauseMenu.Opened -= FreezeCombat;
            pauseMenu.Closed -= UnfreezeCombat;
        }

        UnfreezeCombat();   // nunca dejar el juego congelado
    }

    // Abre la pausa (boton del escudo o Esc). No se abre con el combate terminado.
    public bool OpenPause()
    {
        if (pauseMenu == null || paused || leavingScene || CombatIntroTransition.IsPlaying || turnManager == null || turnManager.BattleFinished)
            return false;

        return pauseMenu.Open();
    }

    private void FreezeCombat()
    {
        if (combatFrozen)
            return;

        combatFrozen = true;
        timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
    }

    private void UnfreezeCombat()
    {
        if (!combatFrozen)
            return;

        combatFrozen = false;
        Time.timeScale = timeScaleBeforePause;
    }

    // Destello dorado suave del marco y las espadas al pasar el cursor sobre el escudo (sin elementos nuevos).
    private void UpdateShieldGlow()
    {
        if (shieldGlow == null)
            return;

        bool lit = shieldHover != null && shieldHover.Hovered && !paused;
        Color wanted = lit ? new Color(GoldBright.r, GoldBright.g, GoldBright.b, 0.55f) : new Color(GoldBright.r, GoldBright.g, GoldBright.b, 0f);

        if (shieldGlow.effectColor != wanted)
            shieldGlow.effectColor = wanted;
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
        feedbackTime = Time.time;
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
            SetText(statsText, "");
        }
        else if (active != null)
        {
            // En el HUD nuevo la placa Turno muestra siempre el nombre de quien juega.
            string side = active.Side == TeamSide.Player || statsText != null
                ? "Turno de " : "Turno enemigo: ";
            SetText(activeText, side + DisplayName(active));

            if (statsText != null)
            {
                SetText(infoText, "Habilidades de " + DisplayName(active));
                SetText(statsText, "HP " + active.CurrentHealth + " / " + active.MaxHealth +
                    "   |   Posición P" + active.Position);
            }
            else
            {
                SetText(infoText,
                    DisplayName(active) + "     HP " + active.CurrentHealth +
                    " / " + active.MaxHealth + "     Posición P" + active.Position);
            }
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

        // Detalle de la habilidad seleccionada (o, sin seleccion, la que esta bajo el cursor).
        AbilityData detail = selectedAbility != null
            ? selectedAbility : pendingConfirm;

        if (detail == null && NewHud && playerTurn && loadout != null && !moveMode && !itemMode)
        {
            for (int i = 0; i < AbilitySlots && i < loadout.Abilities.Count; i++)
            {
                HudFrameStyle over = abilityImages[i] != null ? abilityImages[i].GetComponent<HudFrameStyle>() : null;

                if (over != null && over.Hovered)
                    detail = loadout.Abilities[i];
            }
        }

        // Aliados beneficiados: objetivos validos de la habilidad beneficiosa elegida, o todo el equipo al consultar
        // (cursor sobre) una habilidad que lo afecta automaticamente. Nada al ejecutar, cancelar o cambiar de habilidad.
        allyBenefitTargets.Clear();
        allyBenefitGroup = false;

        if (NewHud && playerTurn && active != null)
        {
            if (selectedAbility != null)
            {
                if (selectedAbility.abilityType != AbilityType.Attack && selectedAbility.targetType != AbilityTargetType.Self)
                    allyBenefitTargets.AddRange(validTargets);
            }
            else if (detail != null && detail.abilityType != AbilityType.Attack && detail.targetType == AbilityTargetType.AllAllies)
            {
                allyBenefitGroup = true;
                for (int slot = 0; slot < 4; slot++)
                {
                    BattleUnit ally = TileUnit(slot);

                    if (ally != null && ally.IsAlive)
                        allyBenefitTargets.Add(ally);
                }
            }
        }

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
                : AbilityDescription(detail));
        }

        RefreshStatRows(showMoveInfo || infoItem >= 0 ? null : detail);

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
        if (followUpText != "" && Time.time >= followUpAt)
        {
            Say(followUpText);
            followUpText = "";
        }

        if (phaseNotice != null)
        {
            bool showNotice = Time.time < phaseNoticeUntil && !finished;

            if (phaseNotice.gameObject.activeSelf != showNotice)
                phaseNotice.gameObject.SetActive(showNotice);
        }

        // Mensajes.
        // Los mensajes caducan (los antiguos no se quedan en pantalla).
        bool showToast = feedback != "" && Time.time - feedbackTime < (feedbackIsError ? 5f : 4f);
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
            if (moveButtonImage.GetComponent<HudFrameStyle>() == null && moveSprite != null)
                moveButtonImage.color = moveMode ? new Color(1f, 0.90f, 0.62f, 1f) : Color.white;   // el sprite ya trae texto e icono
            else
                SetButtonState(moveButtonImage, moveMode ? ButtonState.Selected : (playerTurn ? ButtonState.Normal : ButtonState.Blocked));
        }
        endTurnButton.interactable = playerTurn;

        // Seleccion de objetivo de una habilidad: el objetivo señalado es el primero valido hasta que el cursor
        // pase sobre otro valido (el cursor sobre uno fuera de alcance no cambia nada).
        bool picking = NewHud && selectedAbility != null && validTargets.Count > 0;

        if (!picking)
            focusTarget = null;
        else if (focusTarget == null || !focusTarget.IsAlive || !validTargets.Contains(focusTarget))
            focusTarget = validTargets[0];

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

                if (groundOvals[slot] != null && groundOvals[slot].gameObject.activeSelf)
                    groundOvals[slot].gameObject.SetActive(false);

                if (hpTip != null && hpTipSlot == slot)
                {
                    hpTip.gameObject.SetActive(false);
                    hpTipSlot = -1;
                }

                if (targetZones[slot] != null && targetZones[slot].gameObject.activeSelf)
                    targetZones[slot].gameObject.SetActive(false);

                if (statusTip != null && statusTipSlot == slot)
                {
                    statusTip.gameObject.SetActive(false);
                    statusTipSlot = -1;
                }

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

            if (NewHud)
            {
                // Marco ilustrado InfJugador: el activo (y los objetivos validos) llevan un borde dorado discreto.
                // Barra pequena: el borde dorado marca solo al objetivo; el turno activo lo indica la linea dorada.
                if (activeLines[slot] != null && activeLines[slot].gameObject.activeSelf != isActive)
                    activeLines[slot].gameObject.SetActive(isActive);

                // Objetivo valido de una habilidad: marco rojo (tinte verdoso si es de apoyo); el cursor sobre el sprite
                // o la barra lo convierte en el objetivo señalado. Los objetos usan el borde dorado de siempre.
                UpdateStatusIcons(slot, unit);

                bool abilityTarget = picking && isTarget && unit.IsAlive;

                // Rojo = enemigos (habilidades ofensivas). Las beneficiosas usan el indicador verde de aliados.
                bool redFrame = abilityTarget && selectedAbility.abilityType == AbilityType.Attack;

                if (targetFrames[slot] != null && targetFrames[slot].gameObject.activeSelf != redFrame)
                    targetFrames[slot].gameObject.SetActive(redFrame);

                RectTransform zone = targetZones[slot];

                if (zone != null)
                {
                    if (zone.gameObject.activeSelf != abilityTarget)
                        zone.gameObject.SetActive(abilityTarget);

                    HudFrameStyle zoneStyle = zone.GetComponent<HudFrameStyle>();
                    HudFrameStyle barStyle = tileImages[slot].GetComponent<HudFrameStyle>();

                    if (abilityTarget && ((zoneStyle != null && zoneStyle.Hovered) || (barStyle != null && barStyle.Hovered)))
                        focusTarget = unit;
                }

                Outline tileOutline = tileImages[slot].GetComponent<Outline>();

                if (tileOutline != null)
                    tileOutline.effectColor = isTarget && !picking ? new Color(GoldBright.r, GoldBright.g, GoldBright.b, 0.95f)
                        : (threatened ? new Color(0.90f, 0.52f, 0.25f, 0.9f) : new Color(0f, 0f, 0f, 0f));
            }
            else if (FlatStyle)
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

        UpdateAllyFrames();
        UpdateSkull();
    }

    // Indicador verde bajo los aliados beneficiados por la habilidad elegida (o, en habilidades de todo el equipo,
    // bajo la que se esta consultando). Los tramos de posiciones contiguas comparten un solo indicador estirado;
    // posicion y anchura salen de las casillas reales, no de valores fijos.
    private void UpdateAllyFrames()
    {
        if (allyFrameSprite == null)
            return;

        bool[] mark = new bool[4];

        foreach (BattleUnit unit in allyBenefitTargets)
        {
            for (int slot = 0; slot < 4; slot++)
                if (unit != null && unit.IsAlive && TileUnit(slot) == unit)
                    mark[slot] = true;
        }

        float height = targetFrameWidth * allyFrameSprite.rect.height / allyFrameSprite.rect.width;
        int used = 0;

        for (int s = 0; s < 4; )
        {
            if (!mark[s]) { s++; continue; }

            int e = s;

            // Habilidad grupal: un tramo continuo; individual: un indicador por aliado valido.
            while (allyBenefitGroup && e + 1 < 4 && mark[e + 1])
                e++;

            if (used < allyFrames.Length && allyFrames[used] != null)
            {
                Vector2 first = tileImages[s].rectTransform.anchoredPosition;
                Vector2 last = tileImages[e].rectTransform.anchoredPosition;
                RectTransform frame = allyFrames[used];
                frame.sizeDelta = new Vector2(Mathf.Abs(last.x - first.x) + targetFrameWidth, height);
                frame.anchoredPosition = new Vector2((first.x + last.x) * 0.5f, first.y + targetFrameOffsetY);

                if (!frame.gameObject.activeSelf)
                    frame.gameObject.SetActive(true);

                used++;
            }

            s = e + 1;
        }

        for (int i = used; i < allyFrames.Length; i++)
            if (allyFrames[i] != null && allyFrames[i].gameObject.activeSelf)
                allyFrames[i].gameObject.SetActive(false);
    }

    // Calavera dorada bajo el objetivo señalado: se desliza en horizontal (skullSpeed px/s) hasta el nuevo objetivo.
    private void UpdateSkull()
    {
        if (skull == null)
            return;

        int slot = -1;

        for (int i = 0; focusTarget != null && i < SlotCount; i++)
            if (TileUnit(i) == focusTarget) { slot = i; break; }

        if (slot < 0)
        {
            if (skull.gameObject.activeSelf)
                skull.gameObject.SetActive(false);

            return;
        }

        Vector2 goal = tileImages[slot].rectTransform.anchoredPosition + new Vector2(0f, skullOffsetY);

        if (!skull.gameObject.activeSelf)
        {
            skull.anchoredPosition = goal;
            skull.gameObject.SetActive(true);
        }
        else
        {
            float x = Mathf.MoveTowards(skull.anchoredPosition.x, goal.x, skullSpeed * Time.deltaTime);
            skull.anchoredPosition = new Vector2(x, goal.y);
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

    private string titleSizeKey;
    private int titleSizeValue = 34;
    private int lastChipCount = -1;
    private readonly Dictionary<Sprite, Sprite> portraitCache = new Dictionary<Sprite, Sprite>();

    // Mayor tamano de letra (36..22) con el que el titulo cabe en una sola linea del area de texto.
    public int FitTitleSize(string title)
    {
        float maxWidth = regionText.rectTransform.rect.width > 10f ? regionText.rectTransform.rect.width - 4f : 390f;
        TextGenerationSettings settings = regionText.GetGenerationSettings(new Vector2(10000f, 200f));
        settings.richText = false;
        settings.resizeTextForBestFit = false;
        settings.horizontalOverflow = HorizontalWrapMode.Overflow;
        TextGenerator generator = new TextGenerator();

        for (int size = 36; size > 22; size--)
        {
            settings.fontSize = size;

            if (generator.GetPreferredWidth(title, settings) / Mathf.Max(0.01f, regionText.pixelsPerUnit) <= maxWidth)
                return size;
        }

        return 22;
    }

    // Retrato provisional: recorte de la cabeza del sprite de combate (no se modifica el sprite original).
    // Para usar retratos definitivos basta rellenar 'portraitOverrides' con el nombre de la unidad.
    private Sprite PortraitFor(BattleUnit unit)
    {
        if (unit == null || unit.Data == null)
            return null;

        if (portraitOverrides != null)
            foreach (PortraitOverride entry in portraitOverrides)
                if (entry != null && entry.portrait != null && entry.unitName == unit.Data.unitName)
                    return entry.portrait;

        Sprite source = unit.Data.characterSprite;

        if (source == null)
        {
            SpriteRenderer renderer = unit.GetComponentInChildren<SpriteRenderer>();
            source = renderer != null ? renderer.sprite : null;
        }

        if (source == null)
            return null;

        if (portraitCache.TryGetValue(source, out Sprite cached) && cached != null)
            return cached;

        // Area visible del sprite (malla ajustada) para ignorar el margen transparente.
        Rect r = source.textureRect;
        Rect body = r;
        Vector2[] verts = source.vertices;

        if (verts != null && verts.Length > 2)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            foreach (Vector2 v in verts)
            {
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }

            float ppu = source.pixelsPerUnit;
            Rect fitted = Rect.MinMaxRect(
                r.x + source.pivot.x + minX * ppu, r.y + source.pivot.y + minY * ppu,
                r.x + source.pivot.x + maxX * ppu, r.y + source.pivot.y + maxY * ppu);

            if (fitted.width > 4f && fitted.height > 4f && r.Contains(new Vector2(fitted.center.x, fitted.center.y)))
                body = fitted;
        }

        bool ally = unit.Side == TeamSide.Player;
        float side = Mathf.Min(body.height, body.width * (ally ? 0.62f : 0.55f));
        float centerX = body.x + body.width * (ally ? 0.42f : 0.50f);
        Rect crop = new Rect(
            Mathf.Clamp(centerX - side * 0.5f, body.x, Mathf.Max(body.x, body.xMax - side)),
            body.yMax - side,
            side,
            side);

        Sprite portrait = Sprite.Create(source.texture, crop, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        portrait.name = "Retrato_" + unit.Data.unitName;
        portraitCache[source] = portrait;
        return portrait;
    }

    // Region, codigo del encuentro y orden de turnos (solo lectura).
    private void RefreshHeader(BattleUnit active)
    {
        if (encounterLoader == null)
            encounterLoader = FindAnyObjectByType<EncounterLoader>();

        EncounterData encounter = encounterLoader != null ? encounterLoader.Current : null;

        if (regionText != null)
        {
            // Titulo narrativo de la mision (el mismo del mapa) y debajo region y numero de encuentro.
            string title = encounter != null ? (MapProgression.TitleOf(encounter.encounterId) ?? encounter.displayName) : null;
            int titleSize = 28;

            if (NewHud && title != null)
            {
                // El titulo usa el mayor tamano que cabe en una linea dentro del marco (no se corta ni invade la ronda).
                if (titleSizeKey != title)
                {
                    titleSizeKey = title;
                    titleSizeValue = FitTitleSize(title);
                }

                titleSize = titleSizeValue;
            }
            else if (title != null && title.Length > 20)
            {
                titleSize = 25;
            }

            int subSize = NewHud ? 22 : 19;

            SetText(regionText, encounter != null
                ? "<size=" + titleSize + ">" + title + "</size>\n<size=" + subSize + "><color=#F3F0E8>" + MapProgression.SubtitleOf(encounter.encounterId) + "</color></size>"
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

            // Retrato provisional (recorte del sprite); si no hay, queda la abreviatura.
            Sprite portrait = turnPortraits[shown] != null ? PortraitFor(unit) : null;

            if (turnPortraits[shown] != null)
            {
                if (turnPortraits[shown].sprite != portrait)
                    turnPortraits[shown].sprite = portrait;

                turnPortraits[shown].enabled = portrait != null;
                // Mismo tinte que el sprite en escena (los protagonistas comparten sprite y se distinguen por color).
                SpriteRenderer unitRenderer = unit.GetComponentInChildren<SpriteRenderer>();
                Color tint = unitRenderer != null ? unitRenderer.color : Color.white;
                float shade = acted && !isActive ? 0.75f : 1f;
                turnPortraits[shown].color = new Color(tint.r * shade, tint.g * shade, tint.b * shade, 1f);
            }

            SetText(turnChipTexts[shown], portrait != null ? "" : abbr);
            Color fill = ally ? new Color(0.095f, 0.130f, 0.185f, 1f) : new Color(0.150f, 0.105f, 0.140f, 1f);
            turnChips[shown].color = isActive ? Color.Lerp(fill, GoldBright, 0.14f) : (acted ? Dim(fill, 0.7f) : fill);

            // Marco TurnoNormal (en espera) / TurnoActivo (quien juega); sin retratos definitivos queda la abreviatura.
            if (turnFrames[shown] != null)
            {
                Sprite frameSprite = isActive ? turnActiveSprite : turnNormalSprite;

                if (turnFrames[shown].sprite != frameSprite)
                    turnFrames[shown].sprite = frameSprite;

                turnFrames[shown].color = !isActive && !ally ? new Color(1f, 0.70f, 0.66f, acted ? 0.55f : 1f)
                    : new Color(1f, 1f, 1f, acted && !isActive ? 0.55f : 1f);
            }

            Outline chipOutline = turnChips[shown].GetComponent<Outline>();
            if (chipOutline != null)
                chipOutline.effectColor = turnFrames[shown] != null ? new Color(0f, 0f, 0f, 0f)
                    : (isActive ? GoldBright : (acted ? Dim(LineBlue, 0.6f) : LineBlue));
            turnChipTexts[shown].color = acted ? DimTextColor : (isActive ? Color.white : TextColor);
            shown++;
        }

        for (int i = shown; i < turnChips.Length; i++)
            if (turnChips[i].gameObject.activeSelf)
                turnChips[i].gameObject.SetActive(false);

        // Fila centrada en el area derecha del encabezado, mismo tamano y separacion uniforme.
        if (NewHud && shown != lastChipCount)
        {
            lastChipCount = shown;

            for (int i = 0; i < shown; i++)
            {
                float centerX = TurnAreaCenter + (i - (shown - 1) * 0.5f) * TurnPitch;
                Anchor((RectTransform)turnChips[i].transform, centerX - 31f, 26f, 62f, 62f, 1920f, HeaderHeight);
            }
        }
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

    // Indice del icono (orden de statusIcons) de un estado; -1 si no tiene icono.
    private static int StatusIconIndex(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Bleeding: return 0;
            case StatusEffectType.Poison: return 1;
            case StatusEffectType.Burn: return 2;
            case StatusEffectType.Weaken: return 3;
            case StatusEffectType.Vulnerability:
            case StatusEffectType.PriorityTarget: return 4;
            case StatusEffectType.Guard: return 5;
            case StatusEffectType.Mark: return 8;
            case StatusEffectType.DefenseBoost: return 9;
            case StatusEffectType.Regeneration: return 6;
            case StatusEffectType.Stun: return 7;
            default: return -1;
        }
    }

    // Fila de iconos sobre la barra: solo los estados activos de la unidad (lee los mismos datos que StatusTags;
    // no modifica ningun efecto). Se recalcula cada fotograma, asi que al caducar un estado su icono desaparece.
    private void UpdateStatusIcons(int slot, BattleUnit unit)
    {
        Transform row = tileImages[slot].transform.Find("Estados");

        if (row == null)
            return;

        bool[] present = new bool[StatusIconCount];
        string[] info = new string[StatusIconCount];
        StatusEffectController effects = unit.GetComponent<StatusEffectController>();

        if (effects != null)
        {
            foreach (StatusEffectInstance effect in effects.GetActiveEffects())
            {
                int index = StatusIconIndex(effect.Type);

                if (index >= 0)
                {
                    present[index] = true;
                    info[index] = (info[index] == null ? "" : info[index] + "\n") + StatusInfo(effect);
                }
            }
        }

        RegenerationEffect regeneration = unit.GetComponent<RegenerationEffect>();

        if (regeneration != null && regeneration.IsActive)
        {
            present[6] = true;
            info[6] = "<b>Regeneración</b>\n+" + regeneration.HealPerTurn + " HP por turno (" + regeneration.RemainingTurns + " rds)";
        }

        // Habilidad preparada (p. ej. Golpe de Meteorito): se ejecuta en el siguiente turno de la unidad.
        PreparedAbilityController prepared = unit.GetComponent<PreparedAbilityController>();

        if (prepared != null && prepared.HasPreparation)
        {
            present[10] = true;
            info[10] = "<b>Preparación</b>\n" + prepared.PreparedAbility.abilityName + " se ejecutará en su siguiente turno";
        }

        int hoveredIndex = -1;
        RectTransform hoveredRect = null;

        int count = 0;

        for (int i = 0; i < StatusIconCount; i++)
            if (present[i]) count++;

        float pitch = statusIconSize + statusIconSpacing;
        int shown = 0;

        for (int i = 0; i < StatusIconCount && i < row.childCount; i++)
        {
            Transform icon = row.GetChild(i);
            bool show = present[i] && statusIcons != null && i < statusIcons.Length && statusIcons[i] != null;

            if (icon.gameObject.activeSelf != show)
                icon.gameObject.SetActive(show);

            if (!show)
                continue;

            RectTransform rect = (RectTransform)icon;
            rect.sizeDelta = new Vector2(statusIconSize, statusIconSize);
            rect.anchoredPosition = new Vector2((shown - (count - 1) * 0.5f) * pitch, 0f);
            shown++;

            HudFrameStyle hover = icon.GetComponent<HudFrameStyle>();

            if (hover != null && hover.Hovered)
            {
                hoveredIndex = i;
                hoveredRect = rect;
            }
        }

        ((RectTransform)row).anchoredPosition = new Vector2(0f, statusIconGap);
        ((RectTransform)row).sizeDelta = new Vector2(0f, statusIconSize);

        // Descripcion del estado bajo el cursor (nombre, efecto y turnos restantes).
        if (statusTip == null)
            return;

        if (hoveredIndex >= 0 && info[hoveredIndex] != null)
        {
            statusTipText.text = info[hoveredIndex];
            statusTip.sizeDelta = new Vector2(statusTipText.preferredWidth + 44f, statusTipText.preferredHeight + 24f);
            RectTransform tile = tileImages[slot].rectTransform;
            statusTip.anchoredPosition = tile.anchoredPosition +
                new Vector2(hoveredRect.anchoredPosition.x, tile.sizeDelta.y * 0.5f + statusIconGap + statusIconSize + 6f);
            statusTip.SetAsLastSibling();
            statusTip.gameObject.SetActive(true);
            statusTipSlot = slot;
        }
        else if (statusTipSlot == slot)
        {
            statusTip.gameObject.SetActive(false);
            statusTipSlot = -1;
        }
    }

    // Texto de un estado para su descripcion (valores reales del efecto).
    private static string StatusInfo(StatusEffectInstance effect)
    {
        int p = effect.Potency;
        string turns = " (" + effect.RemainingTurns + " rds)";

        switch (effect.Type)
        {
            case StatusEffectType.Bleeding: return "<b>Sangrado</b>\n" + p + " de daño por turno" + turns;
            case StatusEffectType.Poison: return "<b>Veneno</b>\n" + p + " de daño por turno" + turns;
            case StatusEffectType.Burn: return "<b>Quemadura</b>\n" + p + " de daño por turno" + turns;
            case StatusEffectType.Weaken: return "<b>Debilitamiento</b>\n-" + p + "% de ataque" + turns;
            case StatusEffectType.Vulnerability: return "<b>Vulnerabilidad</b>\n-" + p + "% de defensa" + turns;
            case StatusEffectType.Mark: return "<b>Marca</b>\nEl siguiente ataque directo causa x2 de daño" + turns;
            case StatusEffectType.PriorityTarget: return "<b>Objetivo prioritario</b>\n+" + p + "% de daño directo recibido" + turns;
            case StatusEffectType.DefenseBoost: return "<b>Defensa elevada</b>\n+" + p + "% de defensa" + turns;
            case StatusEffectType.Guard: return "<b>Protección</b>\n-" + p + "% de daño directo" + turns;
            case StatusEffectType.Stun: return "<b>Aturdimiento</b>\nPierde su turno" + turns;
            default: return "<b>" + effect.Type + "</b>" + turns;
        }
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
        float cardY = (feet - 0.08f - cam.transform.position.y) * ppu - card.sizeDelta.y * 0.5f - (NewHud ? 36f : 0f);
        float minCardY = -canvasRect.rect.height * 0.5f + canvasRect.rect.height * (NewHud ? 0.315f : 0.297f) + 8f + card.sizeDelta.y * 0.5f;
        card.anchoredPosition = new Vector2(x, Mathf.Max(cardY, minCardY));

        if (targetZones[slot] != null)
            targetZones[slot].anchoredPosition = card.anchoredPosition + new Vector2(0f, 8f);

        // Al pasar el cursor sobre la barra se muestra la vida exacta (HP actual / maximo).
        HudFrameStyle hover = card.GetComponent<HudFrameStyle>();

        if (hover != null && hpTip != null && hover.Hovered && statusTipSlot != slot)
        {
            hpTipText.text = "HP " + unit.CurrentHealth + " / " + unit.MaxHealth;
            hpTip.sizeDelta = new Vector2(Mathf.Max(180f, hpTipText.preferredWidth + 56f), 54f);
            hpTip.anchoredPosition = card.anchoredPosition + new Vector2(0f, card.sizeDelta.y * 0.5f + 8f);
            hpTip.SetAsLastSibling();
            hpTip.gameObject.SetActive(true);
            hpTipSlot = slot;
        }
        else if (hpTip != null && hpTipSlot == slot)
        {
            hpTip.gameObject.SetActive(false);
            hpTipSlot = -1;
        }

        // Ovalo dorado suave bajo los pies del participante activo (protagonista o enemigo).
        RectTransform oval = groundOvals[slot];

        if (oval != null)
        {
            // HUD nuevo: el turno activo lo indica solo la linea dorada bajo la barra (sin estela ni triangulo).
            bool showOval = isActive && !NewHud;

            if (oval.gameObject.activeSelf != showOval)
                oval.gameObject.SetActive(showOval);

            if (showOval)
                oval.anchoredPosition = new Vector2(x, (feet - cam.transform.position.y) * ppu + 16f);
        }

        RectTransform marker = unitMarkers[slot];

        if (marker == null)
            return;

        bool show = !NewHud && (isTarget || isActive);

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

    // Descripcion de la habilidad para el panel de informacion nuevo (tipo, alcance y movimiento van en filas con icono).
    private string AbilityDescription(AbilityData ability)
    {
        if (!NewHud)
            return Describe(ability);

        string text = string.IsNullOrEmpty(ability.description) ? "" : ability.description;

        if (ability.requiresPreparation)
            text += (text == "" ? "" : "\n") + "Requiere preparación: impacta en el siguiente turno.";

        return text;
    }

    // "P1 – P4" / "P2" / "P1, P3" segun las posiciones marcadas.
    private static string RangeText(bool[] flags)
    {
        List<int> positions = new List<int>();

        for (int i = 0; flags != null && i < flags.Length; i++)
            if (flags[i]) positions.Add(i + 1);

        if (positions.Count == 0)
            return "-";

        bool contiguous = positions[positions.Count - 1] - positions[0] == positions.Count - 1;

        if (positions.Count == 1)
            return "P" + positions[0];

        if (contiguous)
            return "P" + positions[0] + " – P" + positions[positions.Count - 1];

        return PositionList(flags);
    }

    // Filas Tipo / Alcance / Movimiento de la habilidad mostrada (null = se ocultan).
    private void RefreshStatRows(AbilityData ability)
    {
        if (statRows == null || statRows[0] == null)
            return;

        if (detailText != null)
        {
            float minY = ability != null ? 0.50f : 0.08f;

            if (!Mathf.Approximately(detailText.rectTransform.anchorMin.y, minY))
                detailText.rectTransform.anchorMin = new Vector2(detailText.rectTransform.anchorMin.x, minY);
        }

        string[] lines = new string[3];

        if (ability != null)
        {
            string type = ability.abilityType == AbilityType.Attack ? "Ofensiva"
                : (ability.abilityType == AbilityType.Heal ? "Curación" : "Apoyo");
            lines[0] = "Tipo: " + type;

            if (cooldownText != null)
                SetText(cooldownText, ability.cooldown > 0 ? "Recarga: " + ability.cooldown : "Sin recarga");

            switch (ability.targetType)
            {
                case AbilityTargetType.Self: lines[1] = "Alcance: Propio"; break;
                case AbilityTargetType.AllAllies: lines[1] = "Alcance: Todo el equipo"; break;
                case AbilityTargetType.AllEnemies: lines[1] = "Alcance: Todos los enemigos"; break;
                default: lines[1] = "Alcance: " + RangeText(ability.targetPositions); break;
            }

            if (ability.userMovement != UserMovement.None)
                lines[2] = "Movimiento: " + (ability.userMovement == UserMovement.Backward ? "Retrocede 1" : "Avanza 1");
        }

        for (int i = 0; i < statRows.Length; i++)
        {
            bool show = lines[i] != null;

            if (statRows[i] != null && statRows[i].activeSelf != show)
                statRows[i].SetActive(show);

            if (show)
                SetText(statTexts[i], lines[i]);
        }
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

    // Carga los sprites de Assets/Sprites/UICombate+ (cada PNG esta recortado a su area visible en la importacion).
    private void EnsureHudSprites()
    {
#if UNITY_EDITOR
        const string dir = "Assets/Sprites/UICombate+/";
        Sprite Load(string file) { return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(dir + file + ".png"); }

        selectionSprite = Load("seleccion 1");

        string[] iconFiles = { "Sangrado", "veneno", "Quemadura", "Debilitamiento", "Vulnerabilidad", "Protección", "Regeneración", "Aturdimiento", "Marca", "Defensa elevada", "Preparación" };
        statusIcons = new Sprite[StatusIconCount];

        for (int i = 0; i < StatusIconCount; i++)
        {
            string iconPath = "Assets/Sprites/Estados/" + iconFiles[i] + ".png";
            statusIcons[i] = System.Linq.Enumerable.FirstOrDefault(
                System.Linq.Enumerable.OfType<Sprite>(UnityEditor.AssetDatabase.LoadAllAssetsAtPath(iconPath)));
        }

        targetFrameSprite = Load("SeleccionEnem");
        allyFrameSprite = Load("SeleccionAli");
        skullSprite = Load("indicador");
        encounterSprite = Load("UI Encuentro");
        turnPlateSprite = Load("Turno");
        turnNormalSprite = Load("TurnoNormal");
        turnActiveSprite = Load("TurnoActivo");
        playerInfoSprite = Load("InfJugador");
        consumablesFrameSprite = Load("MarcoConsumibles");
        abilitiesFrameSprite = Load("MarcoHabilidades");
        infoFrameSprite = Load("MarcoInfo");
        abilityNormalSprite = Load("HabilidadNormal");
        abilityHoverSprite = Load("HabilidadHover");
        moveSprite = Load("Mover");
        endTurnSprite = Load("TerminarTurno");
        typeIconSprite = Load("Tipo");
        rangeIconSprite = Load("Alcance");
        moveIconSprite = Load("Movimiento");
#endif
    }

    // Coloca un RectTransform por pixeles (origen abajo-izquierda) dentro de un padre de tamano pw x ph.
    private static RectTransform Anchor(RectTransform rect, float x, float y, float w, float h, float pw, float ph)
    {
        rect.anchorMin = new Vector2(x / pw, y / ph);
        rect.anchorMax = new Vector2((x + w) / pw, (y + h) / ph);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    // Imagen decorativa con sprite (sin deformarlo: el rectangulo respeta su proporcion).
    private RectTransform SpriteBox(string name, Transform parent, Sprite sprite, float x, float y, float w, float h, float pw, float ph)
    {
        RectTransform rect = Box(name, parent, Vector2.zero, Vector2.one, sprite != null ? Color.white : new Color(0, 0, 0, 0), sprite);
        rect.GetComponent<Image>().raycastTarget = false;
        return Anchor(rect, x, y, w, h, pw, ph);
    }

    // Boton con dos sprites (normal / resaltado). El estado lo fija SetButtonState; sin desplazamientos.
    private Button SpriteButton(string name, Transform parent, Sprite normal, Sprite hover,
        out Text label, out Image image, int size)
    {
        Button button = MakeButton(name, parent, Vector2.zero, Vector2.one, Color.white, out label, out image, size);
        image.sprite = normal;
        button.transition = Selectable.Transition.None;
        HudFrameStyle style = image.gameObject.AddComponent<HudFrameStyle>();
        style.normalSprite = normal;
        style.hoverSprite = hover != null ? hover : normal;
        return button;
    }

    // Panel con el marco comun (el de Habilidades) a 9 cortes: las esquinas y el borde dorado conservan su tamano
    // en los tres paneles inferiores, sea cual sea su anchura o altura.
    private const float PanelCornerPx = 39.6f;

    private RectTransform SlicedPanel(string name, Transform parent, float x, float y, float w, float h, float pw, float ph)
    {
        RectTransform rect = SpriteBox(name, parent, abilitiesFrameSprite, x, y, w, h, pw, ph);
        Image image = rect.GetComponent<Image>();

        if (abilitiesFrameSprite != null && abilitiesFrameSprite.border.x > 0f)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = abilitiesFrameSprite.border.x / PanelCornerPx;
        }

        return rect;
    }

    // Boton cuyo sprite ya incluye texto e icono: sin etiqueta, tinte al deshabilitar y hover muy discreto (1.02).
    private Button PictureButton(string name, Transform parent, Sprite sprite, float x, float y, float w, float h, out Image image)
    {
        Text label;
        Button button = MakeButton(name, parent, Vector2.zero, Vector2.one, Color.white, out label, out image, 20);
        Anchor((RectTransform)button.transform, x, y, w, h, 1920f, 1080f);
        image.sprite = sprite;
        image.preserveAspect = true;
        label.gameObject.SetActive(false);

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.76f, 1f);   // iluminacion dorada suave al pasar el cursor
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.52f, 0.52f, 0.56f, 1f);
        button.colors = colors;

        HoverScale hover = button.gameObject.AddComponent<HoverScale>();
        hover.HoverFactor = 1.02f;
        return button;
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
        EnsureHudSprites();
        EnsureConsumableIcons();
        const float pw = 1920f;
        const float ph = 1080f;

        // ---------------- Cabecera ----------------
        const float BarH = 156f;
        RectTransform top = Box("BarraSuperior", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Color(BackColor.r, BackColor.g, BackColor.b, 0.94f));
        top.pivot = new Vector2(0.5f, 1f);
        top.offsetMin = new Vector2(0f, -BarH);
        top.offsetMax = Vector2.zero;
        top.GetComponent<Image>().raycastTarget = false;
        RectTransform topLine = Box("LineaDorada", top, Vector2.zero, new Vector2(1, 0), gold);
        topLine.offsetMax = new Vector2(0, 3);
        topLine.GetComponent<Image>().raycastTarget = false;

        // Divisiones discretas entre encuentro, ronda/turno y orden de turnos.
        foreach (float dividerX in new[] { 700f, 1090f })
        {
            RectTransform vLine = Box("Division", top, Vector2.zero, Vector2.one, LineBlue);
            Anchor(vLine, dividerX - 1f, 26f, 2f, 104f, pw, BarH);
            vLine.GetComponent<Image>().raycastTarget = false;
        }

        // Encuentro: nombre narrativo de la mision y, debajo, region y numero de encuentro.
        // El sprite UI Encuentro ya trae el escudo con espadas y el borde: solo se colocan los textos, a la derecha del escudo.
        RectTransform encounter = SpriteBox("Encuentro", top, encounterSprite, 16f, 11f, 660f, 133f, pw, BarH);

        // El escudo de las espadas es el boton de pausa: una zona transparente sobre el escudo del propio sprite
        // (no hay iconos ni botones nuevos). Al pasar el cursor, el marco se ilumina en dorado (Outline).
        if (encounterSprite != null)
        {
            shieldGlow = encounter.gameObject.AddComponent<Outline>();
            shieldGlow.effectDistance = new Vector2(2f, -2f);
            shieldGlow.effectColor = new Color(GoldBright.r, GoldBright.g, GoldBright.b, 0f);
            shieldGlow.useGraphicAlpha = false;

            RectTransform shield = Box("Escudo", encounter, new Vector2(0f, 0f), new Vector2(0.155f, 1f), new Color(0, 0, 0, 0));
            shield.GetComponent<Image>().canvasRenderer.cullTransparentMesh = false;
            shieldHover = shield.gameObject.AddComponent<HudFrameStyle>();
            shieldButton = shield.gameObject.AddComponent<Button>();
            shieldButton.targetGraphic = shield.GetComponent<Image>();
            shieldButton.transition = Selectable.Transition.None;
        }
        regionText = Label("Region", encounter, Vector2.zero, Vector2.one, 34, TextAnchor.MiddleLeft, TextColor);
        Anchor(regionText.rectTransform, 660f * 0.225f, 133f * 0.20f, 660f * 0.625f, 133f * 0.70f, 660f, 133f);
        regionText.fontStyle = FontStyle.Bold;
        regionText.supportRichText = true;
        regionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        regionText.verticalOverflow = VerticalWrapMode.Truncate;

        // Ronda y turno activo.
        // Solo textos, centrados como un conjunto entre los dos separadores (sin placa ni iconos).
        roundText = Label("Ronda", top, Vector2.zero, Vector2.one, 46, TextAnchor.MiddleCenter, TextColor);
        Anchor(roundText.rectTransform, 710f, 78f, 370f, 54f, pw, BarH);
        roundText.fontStyle = FontStyle.Bold;
        roundText.horizontalOverflow = HorizontalWrapMode.Overflow;

        activeText = Label("TurnoActivo", top, Vector2.zero, Vector2.one, 30, TextAnchor.MiddleCenter, new Color(0.83f, 0.69f, 0.42f, 1f));
        Anchor(activeText.rectTransform, 710f, 30f, 370f, 46f, pw, BarH);
        activeText.fontStyle = FontStyle.Bold;
        Fit(activeText, 16, 30);   // una linea; los nombres largos reducen la letra en lugar de desbordar

        // Orden de turno: marco normal (en espera) y activo (el que juega).
        Text orderTitle = Label("TituloOrden", top, Vector2.zero, Vector2.one, 26, TextAnchor.MiddleCenter, GoldBright);
        Anchor(orderTitle.rectTransform, TurnAreaCenter - 300f, 102f, 600f, 40f, pw, BarH);
        orderTitle.fontStyle = FontStyle.Bold;
        orderTitle.text = "Orden de turno";

        for (int i = 0; i < turnChips.Length; i++)
        {
            // La posicion real de cada ficha la fija RefreshHeader (fila centrada segun los participantes vivos).
            RectTransform chip = Box("Turno_" + i, top, Vector2.zero, Vector2.one, SurfaceColor);
            Anchor(chip, TurnAreaCenter - 31f, 26f, 62f, 62f, pw, BarH);
            turnChips[i] = chip.GetComponent<Image>();
            turnChips[i].raycastTarget = false;
            Outline chipOutline = chip.gameObject.AddComponent<Outline>();
            chipOutline.effectDistance = new Vector2(1.5f, -1.5f);
            chipOutline.effectColor = new Color(0f, 0f, 0f, 0f);

            // Retrato provisional centrado en el marco (sin deformar).
            RectTransform portrait = Box("Retrato", chip, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.97f), Color.white);
            portrait.GetComponent<Image>().preserveAspect = true;
            portrait.GetComponent<Image>().raycastTarget = false;
            portrait.GetComponent<Image>().enabled = false;
            turnPortraits[i] = portrait.GetComponent<Image>();

            RectTransform frame = Box("Marco", chip, Vector2.zero, Vector2.one, Color.white, turnNormalSprite);
            frame.offsetMin = new Vector2(-8f, -8f);
            frame.offsetMax = new Vector2(8f, 8f);
            frame.GetComponent<Image>().raycastTarget = false;
            turnFrames[i] = frame.GetComponent<Image>();

            turnChipTexts[i] = Label("Texto", chip, Vector2.zero, Vector2.one, 20, TextAnchor.MiddleCenter, TextColor);
            turnChipTexts[i].fontStyle = FontStyle.Bold;
            turnChipTexts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            chip.gameObject.SetActive(false);
        }

        // Las Coronas no se muestran en el combate (la economia no cambia).
        resourcesText = null;

        // ---------------- Ovalos bajo los pies, tarjetas y marcadores ----------------
        RectTransform ovals = Box("Ovalos", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0));
        ovals.GetComponent<Image>().raycastTarget = false;
        Sprite ovalSprite = art != null ? (art.softCircle != null ? art.softCircle : art.circle) : null;

        for (int slot = 0; slot < SlotCount; slot++)
        {
            RectTransform oval = Box("Ovalo_" + slot, ovals, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Color(GoldBright.r, GoldBright.g, GoldBright.b, 0.62f), ovalSprite);
            oval.pivot = new Vector2(0.5f, 0.5f);
            oval.sizeDelta = new Vector2(200f, 44f);
            oval.GetComponent<Image>().raycastTarget = false;
            groundOvals[slot] = oval;
            oval.gameObject.SetActive(false);
        }

        RectTransform units = Box("Unidades", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0));
        units.GetComponent<Image>().raycastTarget = false;

        // Zonas invisibles sobre el sprite de cada unidad: solo estan activas para objetivos validos de la habilidad
        // elegida (apuntar con el cursor y hacer clic). Van detras de las barras, que conservan su propio clic.
        if (NewHud)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                RectTransform zone = Box("ZonaObjetivo_" + slot, units, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0, 0, 0, 0));
                zone.pivot = new Vector2(0.5f, 0f);
                zone.sizeDelta = new Vector2(210f, 300f);
                Image zoneImage = zone.GetComponent<Image>();
                zoneImage.raycastTarget = true;
                zoneImage.canvasRenderer.cullTransparentMesh = false;
                Button zoneButton = zone.gameObject.AddComponent<Button>();
                zoneButton.targetGraphic = zoneImage;
                zoneButton.transition = Selectable.Transition.None;
                zone.gameObject.AddComponent<HudFrameStyle>();
                targetZones[slot] = zone;
                targetZoneButtons[slot] = zoneButton;
                zone.gameObject.SetActive(false);
            }
        }

        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (slot == 0 && NewHud && allyFrameSprite != null)
            {
                // Indicadores verdes de aliados beneficiados (hasta 4 tramos). Van detras de las casillas para que la
                // barra y el indicador dorado queden encima. Sprite de 9 cortes: los extremos conservan su forma al estirarse.
                for (int i = 0; i < allyFrames.Length; i++)
                {
                    RectTransform frame = Box("AliadoBeneficiado_" + i, units, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white, allyFrameSprite);
                    Image frameImage = frame.GetComponent<Image>();
                    frameImage.type = Image.Type.Sliced;
                    frameImage.pixelsPerUnitMultiplier = allyFrameSprite.rect.width / targetFrameWidth;
                    frameImage.raycastTarget = false;
                    allyFrames[i] = frame;
                    frame.gameObject.SetActive(false);
                }
            }

            bool ally = slot < 4;
            // HUD nuevo: la casilla es solo una barra de vida pequena (nombre, HP y posicion van en el panel inferior).
            RectTransform tile = Box("Casilla_" + slot, units, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                NewHud ? new Color(0.03f, 0.04f, 0.06f, 0.92f) : Color.white, NewHud ? null : playerInfoSprite);
            tile.pivot = new Vector2(0.5f, 0.5f);
            tile.sizeDelta = NewHud ? new Vector2(150f, 16f) : new Vector2(206f, 100f);
            tile.anchoredPosition = new Vector2(ally ? -600f + slot * 200f : 200f + (slot - 4) * 200f, -330f);
            tileImages[slot] = tile.GetComponent<Image>();
            AddFlatBorder(tile.gameObject, new Color(0f, 0f, 0f, 0f), false, 2f);   // borde dorado solo del activo / objetivo
            tileBorders[slot] = null;

            Button b = tile.gameObject.AddComponent<Button>();
            b.targetGraphic = tileImages[slot];
            b.transition = Selectable.Transition.None;
            tileButtons[slot] = b;

            tileTexts[slot] = Label("Texto", tile, new Vector2(0.09f, 0.36f), new Vector2(0.60f, 0.92f), 19, TextAnchor.UpperLeft, TextColor);
            Fit(tileTexts[slot], 12, 19);
            RectTransform barBg = Box("BarraFondo", tile, new Vector2(0.10f, 0.13f), new Vector2(0.90f, 0.30f), new Color(0.04f, 0.05f, 0.07f, 1f));
            tileBars[slot] = Box("Vida", barBg, Vector2.zero, Vector2.one, ally ? HealthColor : HealthEnemyColor);
            AddFlatBorder(barBg.gameObject, CardLine, false);
            barBg.GetComponent<Image>().raycastTarget = false;
            tileBars[slot].GetComponent<Image>().raycastTarget = false;
            tileStatus[slot] = Label("Estado", tile, new Vector2(0.58f, 0.34f), new Vector2(0.92f, 0.90f), 14, TextAnchor.UpperRight, TextColor);
            tileStatus[slot].supportRichText = true;
            Fit(tileStatus[slot], 9, 14);

            if (NewHud)
            {
                tile.gameObject.AddComponent<HudFrameStyle>();
                tileTexts[slot].gameObject.SetActive(false);
                tileStatus[slot].gameObject.SetActive(false);
                barBg.anchorMin = new Vector2(0.02f, 0.16f);
                barBg.anchorMax = new Vector2(0.98f, 0.84f);

                // Linea dorada del turno activo bajo la barra (con un halo suave): complementa al triangulo.
                // Iconos de estados activos: fila centrada justo sobre la barra (solo se muestran los activos).
                RectTransform iconRow = Box("Estados", tile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Color(0, 0, 0, 0));
                iconRow.pivot = new Vector2(0.5f, 0f);
                iconRow.sizeDelta = new Vector2(0f, statusIconSize);
                iconRow.anchoredPosition = new Vector2(0f, statusIconGap);
                iconRow.GetComponent<Image>().raycastTarget = false;

                for (int k = 0; k < StatusIconCount; k++)
                {
                    RectTransform icon = Box("Estado_" + k, iconRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white,
                        statusIcons != null && k < statusIcons.Length ? statusIcons[k] : null);
                    icon.sizeDelta = new Vector2(statusIconSize, statusIconSize);
                    icon.GetComponent<Image>().raycastTarget = true;   // el cursor encima muestra la descripcion del estado
                    icon.GetComponent<Image>().preserveAspect = true;
                    icon.gameObject.AddComponent<HudFrameStyle>();
                    icon.gameObject.SetActive(false);
                }

                if (targetFrameSprite != null)
                {
                    // Marco rojo de objetivo valido: la linea pasa justo bajo la barra. Solo su ancho pulsa (AnimateSelection).
                    RectTransform frame = Box("ObjetivoValido", tile, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white, targetFrameSprite);
                    frame.sizeDelta = new Vector2(targetFrameWidth, targetFrameWidth * targetFrameSprite.rect.height / targetFrameSprite.rect.width);
                    frame.anchoredPosition = new Vector2(0f, targetFrameOffsetY);
                    frame.GetComponent<Image>().raycastTarget = false;
                    frame.GetComponent<Image>().preserveAspect = true;
                    targetFrames[slot] = frame;
                    frame.gameObject.SetActive(false);
                }

                if (selectionSprite != null)
                {
                    // Sprite dorado 'seleccion': corchetes a los lados de la barra y linea debajo. Es independiente de
                    // la barra (no la modifica); su ancho pulsa en Update (AnimateSelection).
                    RectTransform marker = Box("Seleccion", tile, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white, selectionSprite);
                    marker.sizeDelta = new Vector2(selectionWidth, selectionWidth * selectionSprite.rect.height / selectionSprite.rect.width);
                    marker.anchoredPosition = new Vector2(0f, selectionOffsetY);
                    marker.GetComponent<Image>().raycastTarget = false;
                    marker.GetComponent<Image>().preserveAspect = true;
                    activeLines[slot] = marker;
                    marker.gameObject.SetActive(false);
                    continue;
                }

                RectTransform activeLine = Box("LineaActiva", tile, new Vector2(0.04f, 0f), new Vector2(0.96f, 0f), new Color(0, 0, 0, 0));
                activeLine.pivot = new Vector2(0.5f, 1f);
                activeLine.sizeDelta = new Vector2(0f, 11f);
                activeLine.anchoredPosition = new Vector2(0f, -3f);
                activeLine.GetComponent<Image>().raycastTarget = false;
                RectTransform glow = Box("Brillo", activeLine, Vector2.zero, Vector2.one,
                    new Color(GoldBright.r, GoldBright.g, GoldBright.b, 0.40f));
                glow.GetComponent<Image>().raycastTarget = false;
                RectTransform line = Box("Linea", activeLine, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), GoldBright);
                line.sizeDelta = new Vector2(0f, 4f);
                line.GetComponent<Image>().raycastTarget = false;
                activeLines[slot] = activeLine;
                activeLine.gameObject.SetActive(false);
            }
        }

        if (NewHud)
        {
            // Etiqueta flotante con la vida exacta (se muestra al pasar el cursor sobre una barra).
            hpTip = Box("VidaTooltip", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.04f, 0.05f, 0.08f, 0.96f));
            hpTip.pivot = new Vector2(0.5f, 0f);
            hpTip.sizeDelta = new Vector2(180f, 54f);
            hpTip.GetComponent<Image>().raycastTarget = false;
            AddFlatBorder(hpTip.gameObject, AgedGold, false);
            hpTipText = Label("Texto", hpTip, Vector2.zero, Vector2.one, 28, TextAnchor.MiddleCenter, TextColor);
            hpTipText.fontStyle = FontStyle.Bold;
            hpTipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            hpTipText.verticalOverflow = VerticalWrapMode.Overflow;
            hpTipText.raycastTarget = false;
            hpTip.gameObject.SetActive(false);
            hpTipSlot = -1;

            // Descripcion de un estado (cursor sobre su icono): recuadro oscuro con borde dorado y el texto del efecto.
            statusTip = Box("EstadoTooltip", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.04f, 0.05f, 0.08f, 0.97f));
            statusTip.pivot = new Vector2(0.5f, 0f);
            statusTip.sizeDelta = new Vector2(300f, 60f);
            statusTip.GetComponent<Image>().raycastTarget = false;
            AddFlatBorder(statusTip.gameObject, AgedGold, false);
            statusTipText = Label("Texto", statusTip, Vector2.zero, Vector2.one, 24, TextAnchor.MiddleCenter, TextColor);
            statusTipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            statusTipText.verticalOverflow = VerticalWrapMode.Overflow;
            statusTipText.supportRichText = true;
            statusTipText.raycastTarget = false;
            statusTip.gameObject.SetActive(false);
            statusTipSlot = -1;

            // Calavera dorada: marca el objetivo seleccionado y se desliza hasta el nuevo objetivo.
            if (skullSprite != null)
            {
                skull = Box("Calavera", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white, skullSprite);
                skull.sizeDelta = new Vector2(skullWidth, skullWidth * skullSprite.rect.height / skullSprite.rect.width);
                skull.GetComponent<Image>().raycastTarget = false;
                skull.GetComponent<Image>().preserveAspect = true;
                skull.gameObject.SetActive(false);
            }
        }

        RectTransform markers = Box("Marcadores", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0));
        markers.GetComponent<Image>().raycastTarget = false;

        for (int slot = 0; slot < SlotCount; slot++)
        {
            RectTransform marker = Box("Marcador_" + slot, markers, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pointSprite != null ? Color.white : GoldColor,
                pointSprite != null ? pointSprite : (art != null ? art.triangle : null));
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = pointSprite != null ? new Vector2(44f, 49f) : new Vector2(42f, 34f);
            marker.localRotation = Quaternion.Euler(0f, 0f, pointSprite != null ? 0f : 180f);
            marker.GetComponent<Image>().preserveAspect = true;
            marker.GetComponent<Image>().raycastTarget = false;
            unitMarkers[slot] = marker;
            marker.gameObject.SetActive(false);
        }

        // ---------------- Mensaje contextual ----------------
        RectTransform toast = Box("Mensajes", root, new Vector2(0.275f, 0.772f), new Vector2(0.725f, 0.836f), new Color(0.075f, 0.098f, 0.141f, 1f));
        AddFlatBorder(toast.gameObject, AgedGold, false);

        messageText = Label("Mensaje", toast, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), 22, TextAnchor.MiddleCenter, TextColor);
        Fit(messageText, 13, 22);
        toastRoot = toast.gameObject;
        toastRoot.SetActive(false);

        // Registro interno (oculto): se conserva para diagnostico.
        RectTransform logRoot = Box("Registro", root, Vector2.zero, new Vector2(0.01f, 0.01f), new Color(0, 0, 0, 0));
        logText = Label("Texto", logRoot, Vector2.zero, Vector2.one, 12, TextAnchor.LowerLeft, DimTextColor);
        logRoot.gameObject.SetActive(false);

        prepText = Label("Preparacion", root, new Vector2(0.20f, 0.732f), new Vector2(0.80f, 0.770f), 16, TextAnchor.MiddleCenter, new Color(0.95f, 0.78f, 0.50f));
        bannerText = Label("Banner", root, new Vector2(0.2f, 0.60f), new Vector2(0.8f, 0.73f), 80, TextAnchor.MiddleCenter, Color.white);
        bannerText.fontStyle = FontStyle.Bold;

        // ---------------- Panel inferior izquierdo: consumibles ----------------
        RectTransform items = SlicedPanel("PanelConsumibles", root, 18f, 18f, 437f, 317f, pw, ph);
        Text itemsTitle = Label("Titulo", items, new Vector2(0.12f, 0.79f), new Vector2(0.92f, 0.95f), 30, TextAnchor.MiddleLeft, GoldBright);
        itemsTitle.fontStyle = FontStyle.Bold;
        itemsTitle.text = "Consumibles";

        for (int i = 0; i < 2; i++)
        {
            float y0 = i == 0 ? 0.46f : 0.14f;
            float y1 = i == 0 ? 0.74f : 0.42f;
            Text rowLabel; Image rowImage;
            itemButtons[i] = SkinButton(i == 0 ? "Pocion" : "Antidoto", items, new Vector2(0.09f, y0), new Vector2(0.91f, y1),
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
            itemTexts[i] = Label("Nombre", itemButtons[i].transform, new Vector2(0.27f, 0.08f), new Vector2(0.78f, 0.92f), 22, TextAnchor.MiddleLeft, TextColor);
            itemTexts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
            itemCounts[i] = Label("Cantidad", itemButtons[i].transform, new Vector2(0.78f, 0.08f), new Vector2(0.92f, 0.92f), 22, TextAnchor.MiddleRight, TextColor);
            itemCounts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // ---------------- Panel inferior central: personaje y habilidades ----------------
        RectTransform bottom = SlicedPanel("PanelPersonaje", root, 467f, 18f, 870f, 318f, pw, ph);
        infoText = Label("Info", bottom, new Vector2(0.04f, 0.80f), new Vector2(0.56f, 0.96f), 30, TextAnchor.MiddleLeft, GoldBright);
        infoText.fontStyle = FontStyle.Bold;
        infoText.horizontalOverflow = HorizontalWrapMode.Overflow;
        statsText = Label("Estadisticas", bottom, new Vector2(0.46f, 0.80f), new Vector2(0.96f, 0.96f), 24, TextAnchor.MiddleRight, TextColor);
        statsText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // Cuatro habilidades en una sola fila: HabilidadNormal y HabilidadHover (seleccion o cursor encima).
        for (int i = 0; i < AbilitySlots; i++)
        {
            abilityButtons[i] = SpriteButton("Habilidad_" + i, bottom, abilityNormalSprite, abilityHoverSprite,
                out abilityLabels[i], out abilityImages[i], 20);
            Anchor((RectTransform)abilityButtons[i].transform, 28f + i * 208f, 40f, 190f, 176f, 870f, 318f);
            abilityLabels[i].supportRichText = true;

            // Icono arriba (sin deformar), nombre y estado debajo.
            RectTransform iconRect = Box("Icono", abilityButtons[i].transform, new Vector2(0.24f, 0.38f), new Vector2(0.76f, 0.90f), Color.white);
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
            labelRect.anchorMin = new Vector2(0.06f, 0.07f);
            labelRect.anchorMax = new Vector2(0.94f, 0.37f);
            abilityLabels[i].alignment = TextAnchor.MiddleCenter;
            Fit(abilityLabels[i], 12, 21);
        }

        // Contenedor propio de movimiento: aparece unicamente en modo Mover y ocupa el lugar de las habilidades.
        RectTransform moveBox = Box("OpcionesMovimiento", bottom, new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.70f), new Color(0, 0, 0, 0));
        moveBox.GetComponent<Image>().raycastTarget = false;
        moveContainer = moveBox.gameObject;

        for (int i = 0; i < 2; i++)
        {
            // Tres cuadros: Retroceder (izquierda), Avanzar (derecha) y Volver (mas estrecho).
            // Los indices no cambian (0 = Avanzar, 1 = Retroceder); solo su posicion en pantalla.
            float x0 = i == 0 ? 0.375f : 0.01f;
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
        RectTransform info = SlicedPanel("PanelInformacion", root, 1367f, 90f, 530f, 246f, pw, ph);
        abilityTitleText = Label("TituloHabilidad", info, new Vector2(0.07f, 0.78f), new Vector2(0.93f, 0.95f), 30, TextAnchor.MiddleLeft, GoldBright);
        abilityTitleText.fontStyle = FontStyle.Bold;
        Fit(abilityTitleText, 18, 30);
        infoIcon = null;

        detailText = Label("Detalle", info, new Vector2(0.07f, 0.50f), new Vector2(0.93f, 0.78f), 23, TextAnchor.UpperLeft, TextColor);
        Fit(detailText, 13, 23);

        // La ayuda contextual sigue en los mensajes; el texto se conserva oculto.
        hintText = Label("Ayuda", info, new Vector2(0.07f, 0.02f), new Vector2(0.93f, 0.10f), 16, TextAnchor.MiddleLeft, TextColor);
        hintText.gameObject.SetActive(false);

        // Tipo, Alcance y Movimiento con sus iconos.
        Sprite[] statIcons = { typeIconSprite, rangeIconSprite, moveIconSprite };
        float[] rowY = { 0.345f, 0.20f, 0.055f };

        for (int i = 0; i < 3; i++)
        {
            RectTransform row = Box("Fila_" + i, info, new Vector2(0.07f, rowY[i]), new Vector2(0.93f, rowY[i] + 0.145f), new Color(0, 0, 0, 0));
            row.GetComponent<Image>().raycastTarget = false;
            RectTransform statIcon = Box("Icono", row, new Vector2(0f, 0.04f), new Vector2(0.075f, 0.96f), Color.white, statIcons[i]);
            statIcon.GetComponent<Image>().preserveAspect = true;
            statIcon.GetComponent<Image>().raycastTarget = false;
            statTexts[i] = Label("Texto", row, new Vector2(0.10f, 0f), new Vector2(1f, 1f), 21, TextAnchor.MiddleLeft, TextColor);
            Fit(statTexts[i], 13, 21);

            if (i == 0)
            {
                // Tipo a la izquierda y Recarga a la derecha de la misma fila.
                statTexts[0].rectTransform.anchorMax = new Vector2(0.60f, 1f);
                cooldownText = Label("Recarga", row, new Vector2(0.62f, 0f), new Vector2(1f, 1f), 21, TextAnchor.MiddleRight, GoldBright);
                Fit(cooldownText, 13, 21);
            }
            statRows[i] = row.gameObject;
            row.gameObject.SetActive(false);
        }

        // Mover y Terminar turno: los sprites ya incluyen texto e icono (sin etiquetas duplicadas).
        Image moveImg, endImg;
        // Los sprites nuevos ya incluyen icono y texto. Misma altura (64) y anchura (237) en ambos, sin deformar (preserveAspect).
        moveButton = PictureButton("Mover", root, moveSprite, 1383f, 13f, 237f, 64f, out moveImg);
        moveButtonImage = moveImg;
        moveButton.gameObject.AddComponent<MapHoverTarget>();

        endTurnButton = PictureButton("TerminarTurno", root, endTurnSprite, 1644f, 13f, 237f, 64f, out endImg);

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
