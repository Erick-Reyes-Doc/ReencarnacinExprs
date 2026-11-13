using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Mapa del mundo provisional (Map.unity). Solo presenta y navega: las reglas
// viven en MapProgression, GameSession, ConsumableService y EncounterData.
//
// Capas separadas para poder sustituir el arte sin tocar la logica:
//   Fondo        -> imagen general (MapBackground)
//   MapArea      -> lienzo fijo 1920x1080 (nodos y lineas conservan su
//                   posicion relativa en cualquier resolucion)
//     Terreno    -> regiones y relieve provisional
//     Conexiones -> camino entre nodos
//     Nodos      -> un boton por encuentro
//   BarraSuperior y PanelInfo -> interfaz, fuera del lienzo del mapa.
public partial class WorldMapController : MonoBehaviour
{
    private const string BossId = "BOSS";

    [Serializable]
    public class NodeView
    {
        public string id;
        public string zone;
        public Vector2 position;          // normalizada dentro de MapArea
        public RectTransform rect;
        public Button button;
        public Image image;
        public Image ring;
        public Text label;
        public Text stateLabel;

        // Presentacion (solo visual): marco, insignia de estado e icono.
        public Image frame;
        public Image badge;
        public Image badgeIcon;
        public Image icon;
    }

    [Serializable]
    public class LinkView
    {
        public string fromId;
        public string toId;
        public Image line;
    }

    [Serializable]
    public class RegionView
    {
        public string zone;
        public Image overlay;
        public Text label;
    }

    [Header("Referencias")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Sprite circleSprite;
    [SerializeField] private MapShopController shop;

    [Tooltip("Encuentros del mapa (los mismos del cargador de combate).")]
    [SerializeField] private List<EncounterData> encounters = new List<EncounterData>();

    [Header("Escenas (deben estar en Build Settings)")]
    [SerializeField] private string combatSceneName = "CombateIntegracion";
    [SerializeField] private string menuSceneName = "Menu";

    [Header("Vistas generadas")]
    [SerializeField] private List<NodeView> nodes = new List<NodeView>();
    [SerializeField] private List<LinkView> links = new List<LinkView>();
    [SerializeField] private List<RegionView> regions = new List<RegionView>();

    [SerializeField] private Text coinsText;
    [SerializeField] private Text potionsText;
    [SerializeField] private Text antidotesText;
    [SerializeField] private Text progressText;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button inventoryButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Text menuButtonLabel;

    [SerializeField] private Text infoTitle;
    [SerializeField] private Text infoSubtitle;
    [SerializeField] private Text infoRewards;
    [SerializeField] private Text infoEnemies;
    [SerializeField] private Text infoNote;
    [SerializeField] private Button startButton;
    [SerializeField] private Text startButtonLabel;

    [Header("Evento: La máscara rota (texto provisional, editable)")]
    [SerializeField] private string eventTitle = "La máscara rota";
    [TextArea(4, 10)]
    [SerializeField] private string eventNarrative =
        "[TEXTO PROVISIONAL]\n\nLa máscara de Noelle se resquebraja y cae. " +
        "Lo que había detrás queda por fin libre.";
    [SerializeField] private GameObject eventPanel;
    [SerializeField] private Text eventTitleText;
    [SerializeField] private Text eventBodyText;
    [SerializeField] private Button eventConfirmButton;
    [SerializeField] private Button eventCloseButton;

    private static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.14f, 0.94f);
    private static readonly Color ButtonColor = new Color(0.19f, 0.23f, 0.32f, 1f);
    private static readonly Color ButtonOff = new Color(0.12f, 0.13f, 0.17f, 1f);
    private static readonly Color TextColor = new Color(0.90f, 0.92f, 0.96f, 1f);
    private static readonly Color DimText = new Color(0.55f, 0.58f, 0.66f, 1f);
    private static readonly Color GoldColor = new Color(0.95f, 0.85f, 0.5f, 1f);

    private static readonly Color LockedColor = new Color(0.30f, 0.31f, 0.36f, 1f);
    private static readonly Color AvailableColor = new Color(0.30f, 0.55f, 0.85f, 1f);
    private static readonly Color RecommendedColor = new Color(0.95f, 0.75f, 0.25f, 1f);
    private static readonly Color CompletedColor = new Color(0.30f, 0.70f, 0.42f, 1f);
    private static readonly Color EventColor = new Color(0.70f, 0.42f, 0.90f, 1f);

    private Font font;
    private string selectedId;
    private bool loading;
    private bool eventOpen;
    private bool confirmMenu;
    private float confirmUntil;

    public string SelectedId => selectedId;
    public IReadOnlyList<NodeView> Nodes => nodes;

    // ------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------

    private void Start()
    {
        if (canvas == null)
            BuildUI();

        // El mapa nunca es un combate; garantiza que la tienda este disponible.
        GameSession.EnsureSession();
        GameSession.SetInCombat(false);

        WireButtons();

        if (selectedId == null)
            selectedId = MapProgression.NextRecommended();

        Refresh();
        StartBossRevealIfPending();
    }

    [Header("Depuracion TEMPORAL")]
    [Tooltip("F9 = marca P1-P4 y B1 como completados (requiere haber ganado un combate real). Desactivar tras la prueba.")]
    [SerializeField] private bool debugShortcuts = false;

    private void Update()
    {
        if (confirmMenu && Time.unscaledTime > confirmUntil)
            confirmMenu = false;

        if (DebugEnabled && UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
            DebugSkipToBosque1();

        Refresh();
    }

    // Los atajos solo funcionan en el Editor o en compilaciones de
    // desarrollo, aunque el campo quede activo en el Inspector.
    public bool DebugEnabled => debugShortcuts && Debug.isDebugBuild;

    // Solo depuracion: salta a "B1 completado" para probar el evento de
    // Noelle sin jugar cinco combates. No entrega Coronas ni XP.
    public bool DebugSkipToBosque1()
    {
        if (!DebugEnabled)
            return false;

        if (!GameSession.IsActive || !GameSession.HasPartySnapshot ||
            GameSession.IsNoelleLiberated)
        {
            Debug.LogWarning("Depuracion F9: gana primero un combate real (para guardar al equipo) y hazlo antes del evento.");
            return false;
        }

        foreach (string id in new[] { "P1", "P2", "P3", "P4", "B1" })
            GameSession.MarkEncounterCompleted(id);

        selectedId = MapProgression.NextRecommended();
        Debug.Log("Depuracion F9: P1-P4 y B1 marcados como completados.");
        return true;
    }

    private void WireButtons()
    {
        foreach (NodeView node in nodes)
        {
            string id = node.id;
            node.button.onClick.RemoveAllListeners();
            node.button.onClick.AddListener(() => Select(id));
        }

        startButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(() => StartSelected());

        eventConfirmButton.onClick.RemoveAllListeners();
        eventConfirmButton.onClick.AddListener(() => ConfirmEvent());

        eventCloseButton.onClick.RemoveAllListeners();
        eventCloseButton.onClick.AddListener(CloseEvent);

        shopButton.onClick.RemoveAllListeners();
        shopButton.onClick.AddListener(() => OpenPanel(true));

        inventoryButton.onClick.RemoveAllListeners();
        inventoryButton.onClick.AddListener(() => OpenPanel(false));

        menuButton.onClick.RemoveAllListeners();
        // Desde el Paso 90 el boton de menu abre la pausa; OnMenuClicked queda como acceso directo antiguo.
        menuButton.onClick.AddListener(() => OpenPause());

        WireHeroCards();
        WireLocationButtons();
    }

    // ------------------------------------------------------------
    // Acciones
    // ------------------------------------------------------------

    public void Select(string id)
    {
        if (InputLocked)
            return;

        if (loading || eventOpen || PauseOpen || CharactersOpen || (shop != null && shop.IsOpen))
            return;

        if (IsKnownId(id))
            selectedId = id;
    }

    // ------------------------------------------------------------
    // Evento narrativo: La mascara rota
    // ------------------------------------------------------------

    public bool IsEventOpen => eventOpen;

    // El evento esta disponible (B1 completado) y aun sin completar.
    public bool CanOpenEvent()
    {
        return !loading && !eventOpen && !PauseOpen && !CharactersOpen && (shop == null || !shop.IsOpen) &&
               MapProgression.CanStart(GameSession.NoelleEventId);
    }

    public bool OpenEvent()
    {
        if (!CanOpenEvent())
            return false;

        confirmMenu = false;
        eventOpen = true;
        return true;
    }

    public void CloseEvent()
    {
        eventOpen = false;
    }

    // Confirma el evento: marca la etapa liberada de Noelle en la sesion
    // (se aplica al loadout al cargar el combate) y desbloquea B2. No da
    // XP ni Coronas. Solo una vez por partida.
    public bool ConfirmEvent()
    {
        if (!eventOpen || !MapProgression.CanStart(GameSession.NoelleEventId))
            return false;

        bool done = GameSession.TryLiberateNoelle();
        eventOpen = false;

        if (done)
            selectedId = MapProgression.NextRecommended();
        else
            Debug.LogWarning("Evento de Noelle: no se pudo completar (sin datos del equipo).");

        return done;
    }

    private void OpenPanel(bool asShop)
    {
        if (loading || eventOpen || PauseOpen || shop == null)
            return;

        // Con una ficha de personaje abierta, los botones Inventario y Tienda del HUD no hacen nada (el inventario se abre
        // desde las casillas de reliquias de la ficha). Solo cierran el inventario suelto abierto desde el mapa.
        if (CharactersOpen)
        {
            if (characters.InventoryOnly)
            {
                if (!asShop)
                {
                    characters.ToggleInventory();   // Inventario sobre Inventario: cierra
                }
                else
                {
                    characters.Close();             // Tienda desde el inventario: cierra este y abre la tienda
                    confirmMenu = false;
                    shop.Toggle(true);
                }
            }

            return;
        }

        // Sin ficha abierta, el boton Inventario abre el inventario nuevo (reliquias y consumibles), centrado.
        if (!asShop && characters != null)
        {
            confirmMenu = false;

            if (shop.IsOpen)
                shop.Close();

            if (characters.OpenInventory())
                return;
        }

        confirmMenu = false;
        shop.Toggle(asShop);   // abre, cambia entre tienda e inventario o cierra si ya estaba esa vista
    }

    // Comprueba y deja listo el inicio del encuentro seleccionado
    // (guarda el encuentro pendiente). No carga la escena.
    public bool PrepareStart()
    {
        if (InputLocked || loading || eventOpen || PauseOpen || CharactersOpen || (shop != null && shop.IsOpen))
            return false;

        // Los eventos no son combates: no hay encuentro que cargar.
        if (MapProgression.IsEvent(selectedId))
            return false;

        if (string.IsNullOrEmpty(selectedId) || !MapProgression.CanStart(selectedId))
        {
            Debug.Log("Mapa: el encuentro no esta disponible.");
            return false;
        }

        EncounterData data = FindEncounter(selectedId);

        if (data == null || !data.Validate(out string error))
        {
            Debug.LogWarning("Mapa: encuentro invalido (" + selectedId + ").");
            return false;
        }

        if (!GameSession.IsSceneInBuild(combatSceneName))
        {
            Debug.LogError("Mapa: la escena '" + combatSceneName +
                           "' no esta en Build Settings.");
            return false;
        }

        return GameSession.SetPendingEncounter(selectedId);
    }

    private const string ValdoriaSceneName = "ValdoriaDialogo";
    private const string NoelleSceneName = "NoelleDialogo";

    // Evento narrativo con escena propia: disponible segun la cadena de MapProgression y solo una vez. Funde a negro con el
    // fundido del mapa y carga la escena.
    private bool StartDialogueEvent(string eventId, string sceneName)
    {
        if (InputLocked || loading || eventOpen || PauseOpen || CharactersOpen || (shop != null && shop.IsOpen))
            return false;

        if (!MapProgression.CanStart(eventId))
            return false;

        if (!GameSession.IsSceneInBuild(sceneName))
        {
            Debug.LogError("Evento " + eventId + ": la escena '" + sceneName + "' no esta en Build Settings.");
            return false;
        }

        confirmMenu = false;
        loading = true;
        SceneFadeIn fade = FindAnyObjectByType<SceneFadeIn>(FindObjectsInactive.Include);

        if (fade != null)
            fade.FadeOutAndLoad(sceneName, 1f);
        else
            SceneManager.LoadScene(sceneName);

        return true;
    }

    // Una sola carga aunque se haga doble clic.
    public bool StartSelected()
    {
        // Eventos con escena de dialogo propia (fundido a negro y carga): Valdoria y La mascara rota.
        if (selectedId == GameSession.ValdoriaEventId)
            return StartDialogueEvent(GameSession.ValdoriaEventId, ValdoriaSceneName);

        if (selectedId == GameSession.NoelleEventId)
            return StartDialogueEvent(GameSession.NoelleEventId, NoelleSceneName);

        // En un nodo de evento el boton abre la escena narrativa.
        if (MapProgression.IsEvent(selectedId))
            return OpenEvent();

        if (!PrepareStart())
            return false;

        loading = true;

        // Fundido rapido a negro con la carga del combate en segundo plano (antes el mapa se congelaba al cargar).
        SceneFadeIn fade = FindAnyObjectByType<SceneFadeIn>(FindObjectsInactive.Include);

        if (fade != null)
            fade.FadeOutAndLoad(combatSceneName, 0.4f);
        else
            SceneManager.LoadScene(combatSceneName);

        return true;
    }

    // Pide confirmacion antes de terminar la sesion.
    public void OnMenuClicked()
    {
        if (loading || eventOpen)
            return;

        if (!confirmMenu)
        {
            confirmMenu = true;
            confirmUntil = Time.unscaledTime + 5f;
            return;
        }

        if (!PrepareMenu())
            return;

        loading = true;
        SceneManager.LoadScene(menuSceneName);
    }

    // Descarta la sesion si el menu es valido. No carga la escena.
    public bool PrepareMenu()
    {
        if (!GameSession.IsSceneInBuild(menuSceneName))
        {
            Debug.LogError("Mapa: la escena '" + menuSceneName +
                           "' no esta en Build Settings.");
            return false;
        }

        GameSession.EndSession();
        return true;
    }

    // ------------------------------------------------------------
    // Consulta
    // ------------------------------------------------------------

    // Encuentro, evento o jefe conocido por el mapa (con o sin nodos dibujados).
    private static bool IsKnownId(string id)
    {
        return !string.IsNullOrEmpty(id) &&
               (MapProgression.IndexOf(id) >= 0 || id == BossId);
    }

    private NodeView FindNode(string id)
    {
        foreach (NodeView node in nodes)
            if (node.id == id) return node;

        return null;
    }

    private EncounterData FindEncounter(string id)
    {
        foreach (EncounterData data in encounters)
            if (data != null && data.encounterId == id) return data;

        return null;
    }

    public static string ZoneName(string zone)
    {
        return zone == MapProgression.Corruption ? "Corrupción" : zone;
    }

    public string StateText(string id)
    {
        switch (MapProgression.GetState(id))
        {
            case MapProgression.NodeState.Completed:
                return "Completado";
            case MapProgression.NodeState.Available:
                return id == MapProgression.NextRecommended()
                    ? "Siguiente recomendado" : "Disponible";
            default:
                return "Bloqueado";
        }
    }

    private string EnemySummary(EncounterData data)
    {
        List<string> names = new List<string>();
        Dictionary<string, int> counts = new Dictionary<string, int>();

        foreach (EncounterData.EnemySlot slot in data.enemies)
        {
            if (slot == null || slot.prefab == null)
                continue;

            string n = slot.prefab.name;

            if (!counts.ContainsKey(n))
            {
                counts[n] = 0;
                names.Add(n);
            }

            counts[n]++;
        }

        List<string> parts = new List<string>();

        foreach (string n in names)
            parts.Add(counts[n] + "x " + n);

        return string.Join(", ", parts);
    }

    // ------------------------------------------------------------
    // Presentacion
    // ------------------------------------------------------------

    public void Refresh()
    {
        if (canvas == null)
            return;

        SetText(coinsText, "Coronas: " + GameSession.Coins);
        SetText(potionsText, "Pociones: " + GameSession.Potions + "/" + GameSession.MaxPerItem);
        SetText(antidotesText, "Antídotos: " + GameSession.Antidotes + "/" + GameSession.MaxPerItem);

        MapProgression.ZoneProgress(MapProgression.Meadow, out int pd, out int pt);
        MapProgression.ZoneProgress(MapProgression.Forest, out int bd, out int bt);
        MapProgression.ZoneProgress(MapProgression.Corruption, out int cd, out int ct);
        SetText(progressText,
            "Pradera " + pd + "/" + pt + "   Bosque " + bd + "/" + bt +
            "   Corrupción " + cd + "/" + ct +
            (MapProgression.AllCompleted() ? "   Jefe 1/1" : ""));

        // Confirmacion intencional: el primer clic avisa y el segundo
        // termina la partida (se pierde todo el progreso).
        SetText(menuButtonLabel, confirmMenu ? "¿Perder todo? Pulsa otra vez" : "Volver al menú");

        if (menuButtonLabel != null)
            menuButton.targetGraphic.color = confirmMenu
                ? new Color(0.55f, 0.18f, 0.18f, 1f)
                : ButtonColor;
        else
            menuButton.targetGraphic.color = confirmMenu
                ? new Color(1f, 0.62f, 0.58f, 1f)   // HUD del Paso 89: el aviso lo muestra hudMenuHint
                : Color.white;

        RefreshHud();

        string recommended = MapProgression.NextRecommended();

        foreach (RegionView region in regions)
        {
            bool unlocked = MapProgression.IsZoneUnlocked(region.zone);
            region.overlay.color = new Color(0.02f, 0.03f, 0.06f, unlocked ? 0f : 0.55f);

            MapProgression.ZoneProgress(region.zone, out int done, out int total);
            SetText(region.label, ZoneName(region.zone).ToUpper() + "  " + done + "/" + total +
                                  (unlocked ? "" : "  (bloqueada)"));
        }

        foreach (NodeView node in nodes)
        {
            MapProgression.NodeState state = MapProgression.GetState(node.id);

            ApplyNodeVisual(node, state, recommended);
            node.ring.gameObject.SetActive(node.id == selectedId || node.id == recommended);
            node.ring.color = node.id == selectedId
                ? Color.white
                : new Color(RecommendedColor.r, RecommendedColor.g, RecommendedColor.b, 0.7f);

            float pulse = node.id == recommended && Application.isPlaying
                ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 3.5f) : 1f;
            node.rect.localScale = Vector3.one * (node.id == selectedId ? 1.15f : 1f) * pulse;

            SetText(node.stateLabel, StateText(node.id));
            node.stateLabel.color = state == MapProgression.NodeState.Locked ? DimText : TextColor;
        }

        foreach (LinkView link in links)
            ApplyLinkVisual(link, recommended);

        RefreshLocations(recommended);
        RefreshInfo();
    }

    private void RefreshInfo()
    {
        bool hasSelection = IsKnownId(selectedId);
        bool canStart = false;

        if (!hasSelection)
        {
            SetText(infoTitle, "Selecciona un nodo del mapa");
            SetText(infoSubtitle, "");
            SetText(infoRewards, "");
            SetText(infoEnemies, "");
            SetText(infoNote, "");
        }
        else if (MapProgression.IsEvent(selectedId))
        {
            MapProgression.NodeState state = MapProgression.GetState(selectedId);
            canStart = MapProgression.CanStart(selectedId);

            SetText(infoTitle, eventTitle);
            SetText(infoSubtitle, "Zona: Bosque     Estado: " + StateText(selectedId));
            SetText(infoRewards, "Sin XP ni Coronas");
            SetText(infoEnemies, "Evento narrativo obligatorio");
            SetText(infoNote,
                state == MapProgression.NodeState.Locked
                    ? "Completa Bosque 1 para desbloquearlo."
                    : state == MapProgression.NodeState.Completed
                        ? "Evento superado: no se puede repetir."
                        : "Debes completarlo para continuar con Bosque 2.");
        }
        else
        {
            EncounterData data = FindEncounter(selectedId);
            MapProgression.NodeState state = MapProgression.GetState(selectedId);
            canStart = MapProgression.CanStart(selectedId) && data != null;

            SetText(infoTitle, data != null ? data.displayName : selectedId);
            SetText(infoSubtitle, "Zona: " + ZoneName(MapProgression.ZoneOf(selectedId)) +
                                  "     Estado: " + StateText(selectedId));

            if (data != null)
            {
                bool done = state == MapProgression.NodeState.Completed;
                SetText(infoRewards, "XP: " + data.experienceReward +
                                     "     Coronas: " + data.coinReward +
                                     (done ? "  (ya obtenidas)" : ""));
                SetText(infoEnemies, "Enemigos: " + EnemySummary(data));
            }

            bool isBoss = selectedId == MapProgression.BossId;

            SetText(infoNote,
                state == MapProgression.NodeState.Locked
                    ? (isBoss ? "Completa Corrupción 2 para llegar al jefe."
                              : "Completa el encuentro anterior para desbloquearlo.")
                    : state == MapProgression.NodeState.Completed
                        ? (isBoss ? "Jefe derrotado: demo completada."
                                  : "Encuentro superado: no se puede repetir.")
                        : "");
        }

        startButton.interactable = canStart && !loading;

        // Con la ventana de misiones el boton lleva su propio estilo (sprite + velo de estado).
        if (startButtonLabel != null)
        {
            startButton.targetGraphic.color = canStart ? ButtonColor : ButtonOff;
            SetText(startButtonLabel,
                MapProgression.IsEvent(selectedId) ? "Abrir evento" : "Iniciar combate");
        }

        if (eventPanel != null)
        {
            if (eventPanel.activeSelf != eventOpen)
                eventPanel.SetActive(eventOpen);

            SetText(eventTitleText, eventTitle);
            SetText(eventBodyText, eventNarrative);
        }
        if (startButtonLabel != null)
            startButtonLabel.color = canStart ? TextColor : DimText;

        UpdateInfoAccent();
    }

    private static void SetText(Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

}
