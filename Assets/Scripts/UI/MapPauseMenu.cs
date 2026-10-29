using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menu de pausa del mapa. Solo presenta y encamina: la salida al menu usa el flujo existente
// de WorldMapController (PrepareMenu -> EndSession -> cargar Menu) y el audio usa los mismos
// ajustes que las Opciones del menu principal (AudioListener.volume y GameAudioSettings).
// No usa Time.timeScale: el mapa no tiene fisica ni combate que detener.
//
// Opciones visibles: Regresar al juego, Ayuda, Opciones y Salir al escritorio. Paginas: Pausa,
// Ayuda, Opciones y una confirmacion. Abre y cierra con el mismo efecto que las Opciones del
// menu principal: fundido del fondo oscuro y de la ventana y escala 0.95 -> 1 (0.3 s).
// La jerarquia se guarda en Map.unity (BuildInEditor la genera) y no se crea en ejecucion.
public class MapPauseMenu : MonoBehaviour
{
    public enum PauseState { Closed, Pause, Help, Options, ConfirmMenu, ConfirmQuit }

    [Header("Jerarquia (generada en el editor)")]
    [SerializeField] private WorldMapController map;
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform windowRect;
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private RectTransform dividerRect;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private GameObject pausePage;
    [SerializeField] private GameObject helpPage;
    [SerializeField] private GameObject optionsPage;
    [SerializeField] private GameObject confirmPage;
    [SerializeField] private Button helpCloseButton;
    [SerializeField] private TMP_Text[] helpTitleTexts = new TMP_Text[4];
    [SerializeField] private TMP_Text[] helpBodyTexts = new TMP_Text[4];
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private TMP_Text confirmNoLabel;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;

    [Header("Animacion (igual que Opciones del menu principal)")]
    [SerializeField] private float animationDuration = 0.3f;

    [Header("Contenido editable")]
    // Ayuda: cuatro apartados (titulo + descripcion).
    [SerializeField] private string[] helpTitles = { "CONTROLES", "NAVEGACIÓN", "COMBATES", "OBJETIVO" };

    [TextArea(2, 5)]
    [SerializeField] private string[] helpBodies =
    {
        "Todo se maneja con el ratón. La tecla Esc abre y cierra el menú de pausa.",
        "Selecciona una ubicación del mapa para iniciar su encuentro. Las nuevas zonas se desbloquean al superar las anteriores.",
        "Los combates son por turnos. Selecciona una habilidad y después su objetivo. También puedes mover a los héroes, usar consumibles o terminar el turno.",
        "Supera los encuentros de las tres regiones, fortalece a tus héroes y derrota al Capitán de las Fuerzas Expedicionarias en Trono Sellado."
    };

    [SerializeField] private string confirmMenuMessage =
        "¿Salir al menú principal?\nSe perderá el progreso de la partida.";
    [SerializeField] private string confirmQuitMessage =
        "¿Salir al escritorio?\nSe perderá el progreso de la partida.";
    [SerializeField] private string editorQuitNotice =
        "Salir al escritorio solo funciona en una build.\nEn el Editor la partida continúa.";

    // Tamano de la ventana: compacta para la Pausa (4 botones) y la del menu principal para las
    // paginas con mas contenido. El titulo y la linea divisoria siguen al borde superior.
    private static readonly Vector2 PauseWindowSize = new Vector2(902f, 952f);   // las cuatro vistas comparten el mismo tamano
    private static readonly Vector2 FullWindowSize = new Vector2(902f, 952f);
    private static readonly Vector2 HelpWindowSize = new Vector2(902f, 952f);
    private const float HelpContentWidth = 700f; // ~60 px de margen entre el texto y el borde interior del marco
    private const float HelpBlockSpacing = 12f;  // separacion vertical uniforme entre las cuatro filas (mas aire sobre VOLVER)
    private const float HelpIconSize = 64f;      // icono de cada apartado (caja 64 x 64, proporcion original)
    private const float HelpIconGap = 18f;       // hueco entre el icono y el texto
    private const float HelpCloseSize = 48f;     // boton X (UI_Cerrar), igual en todos los paneles
    private const float PauseTitleLift = 0f;     // el titulo y su divisor van en el mismo lugar en las cuatro vistas

    private PauseState state = PauseState.Closed;
    private Coroutine currentAnimation;

    public PauseState State => state;
    public bool IsOpen => state != PauseState.Closed;

    private void Awake()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (windowRect != null)
            windowRect.localScale = Vector3.one * 0.95f;

        if (root != null)
            root.SetActive(false);
    }

    private void Start()
    {
        if (masterSlider != null)
        {
            masterSlider.onValueChanged.RemoveListener(SetMasterVolume);
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        for (int i = 0; i < helpTitleTexts.Length && i < helpTitles.Length; i++)
            if (helpTitleTexts[i] != null)
                helpTitleTexts[i].text = helpTitles[i];

        for (int i = 0; i < helpBodyTexts.Length && i < helpBodies.Length; i++)
            if (helpBodyTexts[i] != null)
                helpBodyTexts[i].text = helpBodies[i];
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            HandleEscape();
    }

    // ------------------------------------------------------------
    // Navegacion
    // ------------------------------------------------------------

    public bool Open()
    {
        if (IsOpen)
            return false;

        if (map != null && !map.CanOpenPause())
            return false;

        Show(PauseState.Pause);
        return true;
    }

    public void Resume()
    {
        Show(PauseState.Closed);
    }

    // Esc: abre la pausa; con una subseccion abierta vuelve primero a Pausa; en Pausa la cierra.
    public void HandleEscape()
    {
        if (!IsOpen)
            Open();
        else
            Back();
    }

    public void Back()
    {
        if (state == PauseState.Pause)
            Resume();
        else if (IsOpen)
            Show(PauseState.Pause);
    }

    public void OpenHelp() { if (state == PauseState.Pause) Show(PauseState.Help); }

    // Boton X de Ayuda: igual que VOLVER (vuelve a Pausa) pero solo actua si Ayuda esta abierta, asi
    // un doble clic nunca llega a cerrar tambien la Pausa.
    public void CloseHelp() { if (state == PauseState.Help) Show(PauseState.Pause); }
    public void OpenOptions() { if (state == PauseState.Pause) Show(PauseState.Options); }

    // Botones X de Opciones y Confirmar: vuelven a Pausa solo si su panel sigue abierto (un doble clic
    // no llega a cerrar tambien la Pausa). Cancelar la confirmacion nunca ejecuta la salida.
    public void CloseOptions() { if (state == PauseState.Options) Show(PauseState.Pause); }
    public void CancelConfirm() { if (state == PauseState.ConfirmMenu || state == PauseState.ConfirmQuit) Show(PauseState.Pause); }
    public void AskExitToMenu() { if (state == PauseState.Pause) Show(PauseState.ConfirmMenu); }
    public void AskQuitDesktop() { if (state == PauseState.Pause) Show(PauseState.ConfirmQuit); }

    public void Confirm()
    {
        if (state == PauseState.ConfirmMenu)
        {
            if (map != null && map.ExitToMenu())
                Show(PauseState.Closed);
        }
        else if (state == PauseState.ConfirmQuit)
        {
            QuitDesktop();
        }
    }

    // Application.Quit() solo cierra una build; en el Editor no detiene Play Mode.
    private void QuitDesktop()
    {
#if UNITY_EDITOR
        Debug.Log("Salir al escritorio: solo funciona en una build (Application.Quit se ignora en el Editor).");
        if (confirmText != null)
            confirmText.text = editorQuitNotice;

        if (confirmYesButton != null)
            confirmYesButton.gameObject.SetActive(false);

        if (confirmNoLabel != null)
            confirmNoLabel.text = "VOLVER";
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------
    // Presentacion
    // ------------------------------------------------------------

    private void Show(PauseState next)
    {
        bool wasOpen = IsOpen;
        state = next;

        if (next == PauseState.Closed)
        {
            if (wasOpen)
                StartAnimation(false);

            return;
        }

        SetActive(pausePage, next == PauseState.Pause);
        SetActive(helpPage, next == PauseState.Help);
        SetActive(optionsPage, next == PauseState.Options);
        SetActive(confirmPage, next == PauseState.ConfirmMenu || next == PauseState.ConfirmQuit);
        ApplyWindowLayout(next);

        if (titleText != null)
        {
            switch (next)
            {
                case PauseState.Help: titleText.text = "AYUDA"; break;
                case PauseState.Options: titleText.text = "OPCIONES"; break;
                case PauseState.ConfirmMenu:
                case PauseState.ConfirmQuit: titleText.text = "CONFIRMAR"; break;
                default: titleText.text = "PAUSA"; break;
            }
        }

        if (next == PauseState.Options)
        {
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(GameAudioSettings.MasterVolume);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameAudioSettings.MusicVolume);
        }
        else if (next == PauseState.ConfirmMenu || next == PauseState.ConfirmQuit)
        {
            if (confirmText != null)
                confirmText.text = next == PauseState.ConfirmMenu ? confirmMenuMessage : confirmQuitMessage;

            if (confirmYesButton != null)
                confirmYesButton.gameObject.SetActive(true);

            if (confirmNoLabel != null)
                confirmNoLabel.text = "CANCELAR";
        }

        // Entrada: solo al abrir desde cerrado (cambiar de pagina no vuelve a animar).
        if (!wasOpen)
        {
            if (root != null && !root.activeSelf)
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = 0f;

                if (windowRect != null)
                    windowRect.localScale = Vector3.one * 0.95f;

                root.SetActive(true);
            }

            StartAnimation(true);
        }
    }

    // La Pausa usa una ventana compacta; Ayuda, Opciones y la confirmacion la del menu principal.
    private void ApplyWindowLayout(PauseState next)
    {
        bool compact = next == PauseState.Pause;
        Vector2 size = compact ? PauseWindowSize : (next == PauseState.Help ? HelpWindowSize : FullWindowSize);

        if (windowRect != null)
            windowRect.sizeDelta = size;

        // Distancias al borde superior: 186 / 241 en las paginas completas (290 y 235 con 952 de
        // alto) y 35 px menos en la Pausa compacta (titulo y divisor mas arriba).
        float lift = compact ? PauseTitleLift : 0f;

        if (titleRect != null)
            titleRect.anchoredPosition = new Vector2(0f, size.y * 0.5f - 186f + lift);

        if (dividerRect != null)
            dividerRect.anchoredPosition = new Vector2(0f, size.y * 0.5f - 241f + lift);
    }

    private void StartAnimation(bool opening)
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        if (!isActiveAndEnabled || canvasGroup == null || windowRect == null)
        {
            // Sin animacion posible: estado final directo.
            if (root != null)
                root.SetActive(opening);

            currentAnimation = null;
            return;
        }

        currentAnimation = StartCoroutine(Animate(opening));
    }

    // Misma curva que MainMenuController.AnimateOptions: smoothstep, tiempo no escalado.
    private IEnumerator Animate(bool opening)
    {
        float startAlpha = canvasGroup.alpha;
        float targetAlpha = opening ? 1f : 0f;
        Vector3 startScale = windowRect.localScale;
        Vector3 targetScale = Vector3.one * (opening ? 1f : 0.95f);

        // Durante la animacion el fondo ya bloquea los clics sobre el mapa.
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        float duration = Mathf.Max(animationDuration, 0.01f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            windowRect.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        windowRect.localScale = targetScale;

        if (opening)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (root != null)
                root.SetActive(false);
        }

        currentAnimation = null;
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }

    public void SetMasterVolume(float volume)
    {
        GameAudioSettings.MasterVolume = volume;
    }

    public void SetMusicVolume(float volume)
    {
        GameAudioSettings.MusicVolume = volume;
    }

    private void OnDestroy()
    {
        if (masterSlider != null)
            masterSlider.onValueChanged.RemoveListener(SetMasterVolume);

        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
    }

#if UNITY_EDITOR
    // ------------------------------------------------------------
    // Generacion de la jerarquia (solo editor; se guarda en la escena)
    // ------------------------------------------------------------

    [System.Serializable]
    public class BuildSprites
    {
        public Sprite window;       // opciones 1
        public Sprite buttonNormal; // menuNormal
        public Sprite buttonHover;  // menuHover
        public Sprite buttonDisabled; // menuDisable
        public Sprite sliderBackground;
        public Sprite sliderFill;
        public Sprite sliderKnob;
        public TMP_FontAsset font;      // Georgia Bold (titulos, botones)
        public TMP_FontAsset bodyFont;  // Georgia Regular (descripciones de Ayuda)
        public Sprite[] helpIcons;      // raton, mapa, espadas, bandera (en ese orden)
        public Sprite closeIcon;        // UI_Cerrar
    }

    private static readonly Color Dark = new Color32(0x10, 0x15, 0x1E, 0xFF);
    private static readonly Color Gold = new Color32(0xB7, 0x95, 0x5B, 0xFF);
    private static readonly Color GoldBright = new Color32(0xD4, 0xAF, 0x6A, 0xFF);
    private static readonly Color Ivory = new Color32(0xE8, 0xDC, 0xC6, 0xFF);

    // Mismos valores que los botones del menu principal (menuNormal, multiplicador 4.49).
    private const float ButtonMultiplier = 4.49f;
    private const float ButtonWidth = 464f;
    private const float ButtonHeight = 156f;
    private const float ButtonPitch = 97f;       // 71 de alto visible + 26 de hueco
    private const float PausePitch = 127f;       // Pausa: 71 de alto visible + 56 de hueco
    private const float PauseListY = -105f;     // bloque de botones centrado entre el divisor y el borde inferior
    private const float VeilOpacity = 0.85f;     // opacidad del fondo oscuro (#10151E); el menu principal usa 0.96

    private BuildSprites sp;

    public void BuildInEditor(Transform canvas, WorldMapController controller, BuildSprites sprites)
    {
        sp = sprites;
        map = controller;

        // Elimina una version anterior.
        Transform old = canvas.Find("MenuPausa");

        if (old != null)
            DestroyImmediate(old.gameObject);

        // Contenedor del componente + raiz del panel (velo a pantalla completa, bloquea clics).
        RectTransform holder = Rect("MenuPausa", canvas, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        holder.offsetMin = holder.offsetMax = Vector2.zero;
        MapPauseMenu menu = holder.gameObject.AddComponent<MapPauseMenu>();
        menu.map = controller;

        RectTransform veil = Rect("Velo", holder, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        veil.offsetMin = veil.offsetMax = Vector2.zero;
        // #10151E al 85 %: con 55 % el mapa seguia viendose casi igual de vivo en el Game View.
        Img(veil, null, new Color(Dark.r, Dark.g, Dark.b, VeilOpacity), false, 1f, true);
        menu.root = veil.gameObject;
        menu.canvasGroup = veil.gameObject.AddComponent<CanvasGroup>();

        RectTransform window = Rect("Ventana", veil, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        window.sizeDelta = PauseWindowSize;
        Img(window, sp.window, Color.white, true, 4.4f, true);
        menu.windowRect = window;

        RectTransform title = Rect("Titulo", window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        title.anchoredPosition = new Vector2(0f, PauseWindowSize.y * 0.5f - 186f + PauseTitleLift);
        title.sizeDelta = new Vector2(600f, 80f);
        menu.titleText = Txt(title, "PAUSA", 46f, Ivory, TextAlignmentOptions.Center, 6f);
        menu.titleRect = title;

        RectTransform divider = Rect("TitleDivider", window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        divider.anchoredPosition = new Vector2(0f, PauseWindowSize.y * 0.5f - 241f + PauseTitleLift);
        divider.sizeDelta = new Vector2(500f, 2f);
        Img(divider, null, new Color(Gold.r, Gold.g, Gold.b, 0.70f), false, 1f, false);
        menu.dividerRect = divider;

        // --- Pausa: cuatro botones, uniformes y centrados verticalmente ---
        RectTransform pause = Page("PaginaPausa", window);
        menu.pausePage = pause.gameObject;
        RectTransform list = Rect("Botones", pause, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        // Bloque visible de 362 px (4 x 71 + 3 x 26): arriba en y ~105 (20 px mas alto que antes),
        // ~49 px bajo la linea divisoria (y 154) y ~72 px sobre el borde inferior visible (~y -329).
        list.anchoredPosition = new Vector2(0f, PauseListY);
        list.sizeDelta = new Vector2(ButtonWidth, 4f * ButtonHeight - 3f * (ButtonHeight - PausePitch));
        VerticalLayoutGroup stack = list.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.spacing = PausePitch - ButtonHeight;
        stack.childAlignment = TextAnchor.MiddleCenter;
        stack.childControlWidth = stack.childControlHeight = true;
        stack.childForceExpandWidth = stack.childForceExpandHeight = false;

        Button resume = MenuButton("BotonRegresar", "REGRESAR AL JUEGO", list, true);
        Button help = MenuButton("BotonAyuda", "AYUDA", list, true);
        Button options = MenuButton("BotonOpciones", "OPCIONES", list, true);
        Button toMenu = MenuButton("BotonVolverMenu", "MENÚ PRINCIPAL", list, true);
        Wire(resume, menu, nameof(Resume));
        Wire(help, menu, nameof(OpenHelp));
        Wire(options, menu, nameof(OpenOptions));
        Wire(toMenu, menu, nameof(AskExitToMenu));

        // --- Ayuda ---
        RectTransform helpRect = Page("PaginaAyuda", window);
        menu.helpPage = helpRect.gameObject;
        BuildHelpContent(helpRect, menu);
        BackButton(helpRect, menu, -(HelpWindowSize.y * 0.5f - 191f));

        // --- Opciones (mismas posiciones que el menu principal) ---
        RectTransform optionsRect = Page("PaginaOpciones", window);
        menu.optionsPage = optionsRect.gameObject;
        Label(optionsRect, "VOLUMEN GENERAL", 135f);
        menu.masterSlider = MakeSlider(optionsRect, "SliderVolumenGeneral", 75f);
        Label(optionsRect, "MÚSICA", -25f);
        menu.musicSlider = MakeSlider(optionsRect, "SliderMusica", -85f);
        BackButton(optionsRect, menu);

        // --- Confirmacion (salir al escritorio) ---
        RectTransform confirm = Page("PaginaConfirmar", window);
        menu.confirmPage = confirm.gameObject;
        RectTransform message = Rect("Mensaje", confirm, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        message.anchoredPosition = new Vector2(0f, 105f);
        message.sizeDelta = new Vector2(660f, 200f);
        menu.confirmText = Txt(message, menu.confirmMenuMessage, 32f, Ivory, TextAlignmentOptions.Center, 1f);
        menu.confirmText.textWrappingMode = TextWrappingModes.Normal;
        menu.confirmText.lineSpacing = 8f;

        Button yes = MenuButton("BotonConfirmar", "CONFIRMAR", confirm, false);
        PlaceButton(yes, new Vector2(0f, -90f));
        Wire(yes, menu, nameof(Confirm));
        menu.confirmYesButton = yes;

        Button no = MenuButton("BotonCancelar", "CANCELAR", confirm, false);
        PlaceButton(no, new Vector2(0f, -210f));
        Wire(no, menu, nameof(Back));
        menu.confirmNoLabel = no.GetComponentInChildren<TMP_Text>();

        // Estado guardado: solo la pagina de Pausa activa y todo el panel oculto.
        menu.helpPage.SetActive(false);
        menu.optionsPage.SetActive(false);
        menu.confirmPage.SetActive(false);
        menu.canvasGroup.alpha = 0f;
        window.localScale = Vector3.one * 0.95f;
        veil.gameObject.SetActive(false);
        UnityEditor.EditorUtility.SetDirty(menu);
    }

    // Contenido de la pagina de Ayuda: cuatro filas (icono 64x64 a la izquierda; titulo dorado en
    // negrita y descripcion marfil en peso normal a la derecha), separadas por un VerticalLayoutGroup,
    // y el boton X de cerrar en la esquina superior derecha del panel.
    private void BuildHelpContent(RectTransform helpRect, MapPauseMenu menu)
    {
        float helpTop = HelpWindowSize.y * 0.5f - 241f - 28f;                           // 28 px bajo el divisor
        float backY = -(HelpWindowSize.y * 0.5f - 191f);                                 // VOLVER, abajo
        float helpBottom = backY + ButtonHeight * 0.5f - 185f / ButtonMultiplier + 28f;  // 28 px sobre VOLVER
        RectTransform blocks = Rect("Contenido", helpRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        blocks.anchoredPosition = new Vector2(0f, (helpTop + helpBottom) * 0.5f);
        blocks.sizeDelta = new Vector2(HelpContentWidth, helpTop - helpBottom);
        VerticalLayoutGroup stackBlocks = blocks.gameObject.AddComponent<VerticalLayoutGroup>();
        stackBlocks.spacing = HelpBlockSpacing;
        stackBlocks.childAlignment = TextAnchor.UpperLeft;
        stackBlocks.childControlWidth = stackBlocks.childControlHeight = true;
        stackBlocks.childForceExpandWidth = true;
        stackBlocks.childForceExpandHeight = false;

        string[] helpNames = { "Controles", "Navegacion", "Combates", "Objetivo" };

        for (int i = 0; i < helpNames.Length; i++)
        {
            RectTransform row = Rect("Bloque_" + helpNames[i], blocks, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = HelpIconGap;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            // Icono: caja fija de 64 x 64 con la proporcion original (sin deformar ni suavizar).
            RectTransform icon = Rect("Icono", row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            Sprite iconSprite = sp.helpIcons != null && i < sp.helpIcons.Length ? sp.helpIcons[i] : null;
            Image iconImage = Img(icon, iconSprite, Color.white, false, 1f, false);
            iconImage.preserveAspect = true;
            LayoutElement iconSize = icon.gameObject.AddComponent<LayoutElement>();
            iconSize.minWidth = iconSize.preferredWidth = HelpIconSize;
            iconSize.minHeight = iconSize.preferredHeight = HelpIconSize;
            iconSize.flexibleWidth = iconSize.flexibleHeight = 0f;

            RectTransform column = Rect("Columna", row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            VerticalLayoutGroup columnLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 6f;
            columnLayout.childAlignment = TextAnchor.UpperLeft;
            columnLayout.childControlWidth = columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;
            LayoutElement columnSize = column.gameObject.AddComponent<LayoutElement>();
            columnSize.flexibleWidth = 1f;

            RectTransform blockTitle = Rect("Titulo", column, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            TMP_Text titleLabel = Txt(blockTitle, menu.helpTitles[i], 24f, GoldBright, TextAlignmentOptions.TopLeft, 3f);
            titleLabel.textWrappingMode = TextWrappingModes.Normal;
            menu.helpTitleTexts[i] = titleLabel;

            RectTransform blockBody = Rect("Texto", column, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            TMP_Text bodyLabel = Txt(blockBody, menu.helpBodies[i], 20f, Ivory, TextAlignmentOptions.TopLeft, 0.5f);
            if (sp.bodyFont != null)
                bodyLabel.font = sp.bodyFont;

            bodyLabel.textWrappingMode = TextWrappingModes.Normal;
            bodyLabel.lineSpacing = 14f;
            menu.helpBodyTexts[i] = bodyLabel;
        }

        // Boton X (UI_Cerrar): sobresale ~14 px de la esquina superior derecha del marco (esquina
        // visible aproximada: x 422, y 452 respecto al centro de la ventana de 902 x 952).
        Button helpClose = BuildCloseButton(helpRect, HelpWindowSize, menu, nameof(CloseHelp));

        if (helpClose != null)
            menu.helpCloseButton = helpClose;
    }

    // Boton X (UI_Cerrar) de 48 x 48 en la esquina superior derecha del marco de una pagina; el
    // marco visible deja ~29 px a la derecha y ~24 px arriba, asi que sobresale ~14 px.
    private Button BuildCloseButton(RectTransform page, Vector2 windowSize, MapPauseMenu menu, string method)
    {
        if (sp == null || sp.closeIcon == null)
            return null;

        RectTransform close = Rect("BotonCerrar", page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        close.anchoredPosition = new Vector2(windowSize.x * 0.5f - 29f - 10f, windowSize.y * 0.5f - 24f - 10f);
        close.sizeDelta = new Vector2(HelpCloseSize, HelpCloseSize);
        Image closeImage = Img(close, sp.closeIcon, Color.white, false, 1f, true);
        closeImage.preserveAspect = true;
        Button closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = closeButton.colors;
        // Un tinte no puede superar el color original: en reposo la X va al 88 % y en hover pasa a
        // blanco puro (algo mas brillante), sin sprites nuevos.
        colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        closeButton.colors = colors;
        Navigation navigation = closeButton.navigation;
        navigation.mode = Navigation.Mode.None;
        closeButton.navigation = navigation;
        Wire(closeButton, menu, method);
        return closeButton;
    }

    // Anade (o rehace) solo el boton X de Pausa, Opciones y Confirmar sobre el panel ya existente.
    public void AddCloseButtonsInEditor(BuildSprites sprites)
    {
        sp = sprites;
        GameObject[] pages = { pausePage, optionsPage, confirmPage };
        Vector2[] sizes = { PauseWindowSize, FullWindowSize, FullWindowSize };
        string[] methods = { nameof(Resume), nameof(CloseOptions), nameof(CancelConfirm) };

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] == null)
                continue;

            RectTransform page = (RectTransform)pages[i].transform;
            Transform old = page.Find("BotonCerrar");

            if (old != null)
                DestroyImmediate(old.gameObject);

            BuildCloseButton(page, sizes[i], this, methods[i]);
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }

    // Rehace solo el contenido de la pagina de Ayuda (iconos + boton X) sobre un panel ya existente,
    // sin tocar el resto del menu de pausa (botones, sliders, confirmacion, velo...).
    public void RebuildHelpPageInEditor(BuildSprites sprites)
    {
        sp = sprites;
        RectTransform page = helpPage != null ? (RectTransform)helpPage.transform : null;

        if (page == null)
            return;

        Transform oldContent = page.Find("Contenido");
        Transform oldClose = page.Find("BotonCerrar");

        if (oldContent != null) DestroyImmediate(oldContent.gameObject);
        if (oldClose != null) DestroyImmediate(oldClose.gameObject);

        if (helpTitleTexts == null || helpTitleTexts.Length != 4) helpTitleTexts = new TMP_Text[4];
        if (helpBodyTexts == null || helpBodyTexts.Length != 4) helpBodyTexts = new TMP_Text[4];

        BuildHelpContent(page, this);

        // El contenido queda debajo del boton VOLVER en la jerarquia solo si se crea antes; el orden
        // visual no se solapa (VOLVER esta fuera del area del contenido).
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private RectTransform Page(string name, RectTransform window)
    {
        RectTransform page = Rect(name, window, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        page.offsetMin = page.offsetMax = Vector2.zero;
        return page;
    }

    private void BackButton(RectTransform page, MapPauseMenu menu, float y = -285f)
    {
        Button back = MenuButton("BotonVolver", "VOLVER", page, false);
        PlaceButton(back, new Vector2(0f, y));
        Wire(back, menu, nameof(Back));
    }

    private static void PlaceButton(Button button, Vector2 position)
    {
        RectTransform rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
    }

    private void Label(RectTransform page, string text, float y)
    {
        RectTransform rt = Rect("Etiqueta_" + text, page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(500f, 44f);
        Txt(rt, text, 30f, Ivory, TextAlignmentOptions.MidlineLeft, 2f);
    }

    private Slider MakeSlider(RectTransform page, string name, float y)
    {
        RectTransform rt = Rect(name, page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(500f, 35f);

        RectTransform background = Rect("Background", rt, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), new Vector2(0.5f, 0.5f));
        background.offsetMin = background.offsetMax = Vector2.zero;
        Img(background, sp.sliderBackground, Dark, true, 1f, true);

        RectTransform fillArea = Rect("Fill Area", rt, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), new Vector2(0.5f, 0.5f));
        fillArea.anchoredPosition = new Vector2(-5f, 0f);
        fillArea.sizeDelta = new Vector2(-20f, 0f);
        RectTransform fill = Rect("Fill", fillArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        fill.sizeDelta = new Vector2(10f, 0f);
        Img(fill, sp.sliderFill, Gold, true, 1f, true);

        RectTransform handleArea = Rect("Handle Slide Area", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        handleArea.anchoredPosition = Vector2.zero;
        handleArea.sizeDelta = new Vector2(-20f, 0f);
        RectTransform handle = Rect("Handle", handleArea, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f));
        handle.sizeDelta = new Vector2(20f, 0f);
        Image knob = Img(handle, sp.sliderKnob, GoldBright, false, 1f, true);

        Slider slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = knob;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    // Boton identico a los del menu principal: marco menuNormal 9-slice, texto marfil, hover dorado.
    private Button MenuButton(string name, string label, Transform parent, bool inLayout)
    {
        RectTransform rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        Image image = Img(rt, sp.buttonNormal, Color.white, true, ButtonMultiplier, true);

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = sp.buttonHover,
            pressedSprite = sp.buttonHover,
            selectedSprite = sp.buttonHover,
            disabledSprite = sp.buttonDisabled
        };
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        if (inLayout)
        {
            LayoutElement size = rt.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = ButtonWidth;
            size.preferredHeight = ButtonHeight;
        }

        RectTransform text = Rect("Text (TMP)", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        text.offsetMin = text.offsetMax = Vector2.zero;
        TMP_Text tmp = Txt(text, label, 26f, Ivory, TextAlignmentOptions.Center, 2f);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 26f;
        text.offsetMin = new Vector2(34f, 0f);
        text.offsetMax = new Vector2(-34f, 0f);

        rt.gameObject.AddComponent<MenuButtonLabel>();
        return button;
    }

    private static void Wire(Button button, MapPauseMenu target, string method)
    {
        UnityEditor.Events.UnityEventTools.AddPersistentListener(
            button.onClick,
            (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, method));
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        return rt;
    }

    private static Image Img(RectTransform rt, Sprite sprite, Color color, bool sliced, float multiplier, bool raycast)
    {
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = multiplier;
        image.raycastTarget = raycast;
        return image;
    }

    private TMP_Text Txt(RectTransform rt, string text, float size, Color color, TextAlignmentOptions alignment, float spacing)
    {
        TextMeshProUGUI label = rt.gameObject.AddComponent<TextMeshProUGUI>();

        if (sp != null && sp.font != null)
            label.font = sp.font;

        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.characterSpacing = spacing;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }
#endif
}
