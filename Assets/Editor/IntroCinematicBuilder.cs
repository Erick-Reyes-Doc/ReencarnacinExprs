using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Construye la escena IntroCinematic.unity (Canvas, capas de imagen, logo, fundido, botones y pausa) y asigna los
// recursos reales de Assets/Sprites/Backgrounds/Intro y Assets/Audio/Intro. Tambien apunta Nueva partida a esta escena.
// Se puede volver a ejecutar sin problema: reconstruye la escena entera.
public static class IntroCinematicBuilder
{
    const string ScenePath = "Assets/Scenes/IntroCinematic.unity";
    const string MenuPath = "Assets/Scenes/Menu.unity";
    const string ImagesFolder = "Assets/Sprites/Backgrounds/Intro";
    const string VoiceFolder = "Assets/Audio/Intro/IntroVoz";
    const string SfxFolder = "Assets/Audio/Intro/Efectos";
    const string MusicPath = "Assets/Audio/Intro/Lasciate ogne speranza.wav";
    const string LogoPath = "Assets/Sprites/Logo/LOGO 1 transparente.png";

    static readonly Color Gold = new Color(0.831f, 0.686f, 0.416f, 1f);
    static readonly Color Navy = new Color(0.063f, 0.082f, 0.118f, 1f);
    static readonly Color Ivory = new Color(0.93f, 0.90f, 0.82f, 1f);

    // Duraciones y retrasos de voz propuestos (escenas 1 a 11).
    static readonly float[] Durations = { 12, 10, 14, 10, 10, 14, 18, 15, 27, 22, 8 };
    static readonly float[] VoiceDelays = { 0.8f, 0.6f, 0.7f, 0f, 0.2f, 1.2f, 2f, 1f, 1.5f, 0.8f, 0f };
    // ------------------------------------------------------------------------------------------------
    // SUBTITULOS (unico lugar con textos y tiempos). start/end = segundos desde el inicio del audio de la escena
    // (VO_01 a VO_10); el tiempo real es inicio de escena + retraso de la voz + start. Los cortes salen de las pausas
    // medidas en cada narracion. Para corregir un subtitulo basta cambiar su texto o sus segundos aqui (o en el
    // Inspector de IntroManager) y reconstruir la escena.
    // ------------------------------------------------------------------------------------------------
    struct SubtitleLine
    {
        public int shot;
        public float start, end;
        public string text;
        public SubtitleLine(int shot, float start, float end, string text) { this.shot = shot; this.start = start; this.end = end; this.text = text; }
    }

    // Quien habla en cada narracion (escenas 1 a 10): NARRADOR o DIOS (voz misteriosa). Editable.
    static readonly string[] Speakers = { "NARRADOR", "NARRADOR", "NARRADOR", "NARRADOR", "NARRADOR", "DIOS", "NARRADOR", "DIOS", "NARRADOR", "DIOS" };
    static readonly SubtitleLine[] SubtitleTable =
    {
        // 1 El ultimo turno
        new SubtitleLine(1, 0.06f, 5.20f, "A las cuatro de la madrugada, Martín llevaba treinta y cuatro horas trabajando."),
        new SubtitleLine(1, 5.80f, 8.46f, "Lo único que deseaba era volver a casa..."),
        new SubtitleLine(1, 9.00f, 9.96f, "y descansar."),
        // 2 Salir a casa
        new SubtitleLine(2, 0.06f, 4.56f, "Sin imaginar lo que le esperaba, salió de su oficina bajo la lluvia."),
        new SubtitleLine(2, 5.16f, 8.54f, "Había sobrevivido a otra jornada interminable."),
        // 3 Tres amigas
        new SubtitleLine(3, 0.08f, 5.42f, "Mientras tanto, Elaina, Noelle y Evanora regresaban juntas a casa."),
        new SubtitleLine(3, 6.02f, 11.80f, "Eran tres amigas que jamás imaginaron lo que aquella noche les tenía preparado."),
        // 4 La interseccion
        new SubtitleLine(4, 0.08f, 2.08f, "Ellas no conocían a Martín."),
        new SubtitleLine(4, 2.60f, 5.04f, "Sus caminos nunca debieron cruzarse..."),
        new SubtitleLine(4, 5.68f, 8.34f, "pero el destino tenía otros planes."),
        // 5 Un solo instante
        new SubtitleLine(5, 0.04f, 1.96f, "Un vehículo perdió el control."),
        new SubtitleLine(5, 2.42f, 4.36f, "Y, en un solo instante..."),
        new SubtitleLine(5, 5.02f, 7.46f, "cuatro vidas llegaron a su fin."),
        // 6 Segunda oportunidad (audio VO_06): habla Dios
        new SubtitleLine(6, 0.04f, 2.66f, "Sus vidas en aquel mundo han terminado."),
        new SubtitleLine(6, 3.33f, 5.62f, "Pero todavía existe una oportunidad."),
        new SubtitleLine(6, 6.19f, 8.20f, "Un nuevo mundo los necesita..."),
        new SubtitleLine(6, 8.80f, 11.96f, "y ustedes pueden cambiar su destino."),
        // 7 Cuatro almas (audio VO_07): habla el narrador
        new SubtitleLine(7, 0.04f, 3.58f, "Sin embargo, aquella tragedia no sería su final."),
        new SubtitleLine(7, 4.32f, 7.60f, "Cuatro almas quedaron suspendidas entre la muerte..."),
        new SubtitleLine(7, 8.24f, 10.90f, "y un destino que todavía desconocían."),
        // 8 Un mundo en peligro
        new SubtitleLine(8, 0.04f, 1.65f, "Su nombre es Aethel."),
        new SubtitleLine(8, 2.28f, 6.41f, "Un mundo donde las fuerzas del Rey Demonio avanzan sin descanso."),
        new SubtitleLine(8, 7.02f, 9.16f, "La corrupción consume sus tierras"),
        new SubtitleLine(8, 9.43f, 12.12f, "y amenaza la voluntad de sus habitantes."),
        // 9 Nuevo amanecer
        new SubtitleLine(9, 0.04f, 1.86f, "Así despertaron en Aethel."),
        new SubtitleLine(9, 2.44f, 5.12f, "Elaina había adquirido un cuerpo élfico."),
        new SubtitleLine(9, 5.64f, 10.88f, "Evanora había perdido sus recuerdos y ocultaba su rostro tras una máscara."),
        new SubtitleLine(9, 11.44f, 14.54f, "Noelle descubría un mundo lleno de magia..."),
        new SubtitleLine(9, 15.12f, 19.70f, "y Martín, ahora llamado Valerian, solo deseaba descansar."),
        new SubtitleLine(9, 20.22f, 23.66f, "Convertirse en héroe era lo último que tenía en mente."),
        // 10 Primera mision
        new SubtitleLine(10, 0.06f, 3.02f, "Las Llanuras Centrales están siendo devastadas."),
        new SubtitleLine(10, 3.52f, 7.12f, "Si caen, la corrupción continuará extendiéndose."),
        new SubtitleLine(10, 7.68f, 12.56f, "Los guiaré, pero serán ustedes quienes decidan el destino de este mundo."),
        new SubtitleLine(10, 13.12f, 15.95f, "Su primera misión será proteger estas tierras"),
        new SubtitleLine(10, 16.10f, 19.56f, "y detener el avance de las fuerzas del Rey Demonio.")
    };


    [MenuItem("Reencarnacion/Construir escena de introduccion")]
    public static void BuildAll()
    {
        string report = Build();
        Debug.Log(report);
    }

    public static string Build()
    {
        if (EditorApplication.isPlaying)
            return "Sal de Play antes de construir la escena.";

        string missing = CheckAssets();

        if (missing != "")
            return "Faltan recursos:\n" + missing;

        ConfigureMusicImport();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Se conserva la camara existente; el resto de la escena se reconstruye.
        GameObject cameraObject = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<Camera>() != null && cameraObject == null)
                cameraObject = root;
            else
                Object.DestroyImmediate(root);
        }

        if (cameraObject != null)
        {
            Camera cam = cameraObject.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            if (cameraObject.GetComponent<AudioListener>() == null)
                cameraObject.AddComponent<AudioListener>();
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Georgia_OS_SDF.asset");
        TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/GeorgiaBold_OS_SDF.asset");

        GameObject managerObject = new GameObject("IntroManager");
        IntroCinematicController controller = managerObject.AddComponent<IntroCinematicController>();

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        // Fondo: dos capas para el fundido cruzado; cada imagen conserva su proporcion (Envelope: rellena sin deformar).
        RectTransform background = Stretch("Fondo", canvasObject.transform);
        background.gameObject.AddComponent<RectMask2D>();
        Image[] layers = new Image[2];
        AspectRatioFitter[] fitters = new AspectRatioFitter[2];

        for (int i = 0; i < 2; i++)
        {
            GameObject go = new GameObject("Imagen_" + (i == 0 ? "A" : "B"), typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            go.transform.SetParent(background, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            layers[i] = go.GetComponent<Image>();
            layers[i].raycastTarget = false;
            layers[i].preserveAspect = false;
            layers[i].enabled = false;
            fitters[i] = go.GetComponent<AspectRatioFitter>();
            fitters[i].aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitters[i].aspectRatio = 16f / 9f;
        }

        // Logo: elemento independiente con transparencia; conserva sus proporciones.
        Sprite logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        GameObject logoObject = new GameObject("Logo", typeof(RectTransform), typeof(Image));
        logoObject.transform.SetParent(canvasObject.transform, false);
        RectTransform logoRect = logoObject.GetComponent<RectTransform>();
        logoRect.anchorMin = logoRect.anchorMax = new Vector2(0.5f, 0.5f);
        logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = Vector2.zero;
        Image logoImage = logoObject.GetComponent<Image>();
        logoImage.sprite = logoSprite;
        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;
        logoObject.SetActive(false);

        // Subtitulos: elemento independiente de las imagenes, abajo al centro, por encima del fondo y el logo y por
        // debajo del fundido negro, los controles y la pausa.
        Color subtitleIvory = new Color(0.910f, 0.863f, 0.776f, 1f);   // #E8DCC6
        Color subtitleGold = new Color(0.718f, 0.584f, 0.357f, 1f);    // #B7955B
        RectTransform subtitleRoot = Stretch("Subtitulos", canvasObject.transform);
        CanvasGroup subtitleGroup = subtitleRoot.gameObject.AddComponent<CanvasGroup>();
        subtitleGroup.alpha = 0f;
        subtitleGroup.blocksRaycasts = false;
        subtitleGroup.interactable = false;

        GameObject boxObject = new GameObject("Caja", typeof(RectTransform), typeof(Image));
        boxObject.transform.SetParent(subtitleRoot, false);
        RectTransform subtitleBox = boxObject.GetComponent<RectTransform>();
        subtitleBox.anchorMin = subtitleBox.anchorMax = new Vector2(0.5f, 0f);
        subtitleBox.pivot = new Vector2(0.5f, 0f);
        subtitleBox.anchoredPosition = new Vector2(0f, 64f);
        Image boxImage = boxObject.GetComponent<Image>();
        boxImage.color = new Color(Navy.r, Navy.g, Navy.b, 0.82f);
        boxImage.raycastTarget = false;

        // Filete dorado fino (4 lineas): un contorno con Outline mezclaria el dorado con el azul translucido.
        for (int side = 0; side < 4; side++)
        {
            GameObject lineObject = new GameObject("Filete_" + side, typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(subtitleBox, false);
            Image lineImage = lineObject.GetComponent<Image>();
            lineImage.color = new Color(subtitleGold.r, subtitleGold.g, subtitleGold.b, 0.65f);
            lineImage.raycastTarget = false;
            RectTransform lr = lineObject.GetComponent<RectTransform>();

            if (side < 2)   // arriba y abajo
            {
                lr.anchorMin = new Vector2(0f, side == 0 ? 1f : 0f);
                lr.anchorMax = new Vector2(1f, side == 0 ? 1f : 0f);
                lr.pivot = new Vector2(0.5f, side == 0 ? 1f : 0f);
                lr.sizeDelta = new Vector2(0f, 2f);
            }
            else            // izquierda y derecha
            {
                lr.anchorMin = new Vector2(side == 2 ? 0f : 1f, 0f);
                lr.anchorMax = new Vector2(side == 2 ? 0f : 1f, 1f);
                lr.pivot = new Vector2(side == 2 ? 0f : 1f, 0.5f);
                lr.sizeDelta = new Vector2(2f, 0f);
            }

            lr.anchoredPosition = Vector2.zero;
        }

        TMP_Text subtitleText = Label("Texto", subtitleBox, "", 40f, subtitleIvory, font, TextAlignmentOptions.Center);
        subtitleText.textWrappingMode = TextWrappingModes.Normal;
        subtitleText.overflowMode = TextOverflowModes.Overflow;
        subtitleText.lineSpacing = 6f;
        subtitleText.rectTransform.anchorMin = Vector2.zero;
        subtitleText.rectTransform.anchorMax = Vector2.one;
        subtitleText.rectTransform.offsetMin = new Vector2(44f, 20f);
        subtitleText.rectTransform.offsetMax = new Vector2(-44f, -20f);
        TMP_Text subtitleSpeaker = Label("Hablante", subtitleBox, "", 22f, subtitleGold, bold, TextAlignmentOptions.Center);
        subtitleSpeaker.characterSpacing = 6f;
        subtitleSpeaker.rectTransform.anchorMin = new Vector2(0f, 1f);
        subtitleSpeaker.rectTransform.anchorMax = new Vector2(1f, 1f);
        subtitleSpeaker.rectTransform.pivot = new Vector2(0.5f, 1f);
        subtitleSpeaker.rectTransform.sizeDelta = new Vector2(0f, 30f);
        subtitleSpeaker.rectTransform.anchoredPosition = new Vector2(0f, -16f);

        RectTransform blackRect = Stretch("FundidoNegro", canvasObject.transform);
        Image black = blackRect.gameObject.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;

        // Panel de pausa (oculto).
        RectTransform pausePanel = Stretch("PanelPausa", canvasObject.transform);
        Image pauseBg = pausePanel.gameObject.AddComponent<Image>();
        pauseBg.color = new Color(Navy.r, Navy.g, Navy.b, 0.72f);
        TMP_Text pauseTitle = Label("Titulo", pausePanel, "PAUSA", 84f, Gold, bold, TextAlignmentOptions.Center);
        pauseTitle.rectTransform.anchorMin = pauseTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        pauseTitle.rectTransform.sizeDelta = new Vector2(800f, 120f);
        pauseTitle.rectTransform.anchoredPosition = new Vector2(0f, 190f);
        Button resume = MakeButton("BotonReanudar", pausePanel, "REANUDAR", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 64f), new Vector2(464f, 156f), 26f, bold, out _);
        Button pauseSkip = MakeButton("BotonOmitirPausa", pausePanel, "OMITIR INTRODUCCIÓN", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -63f), new Vector2(464f, 156f), 26f, bold, out _);
        pausePanel.gameObject.SetActive(false);

        // Controles siempre visibles arriba a la derecha.
        RectTransform controls = Stretch("Controles", canvasObject.transform);
        TMP_Text pauseLabel;
        Button pause = MakeButton("BotonPausa", controls, "PAUSA", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-374f, -2f), new Vector2(348f, 117f), 19.5f, bold, out pauseLabel);
        Button skip = MakeButton("BotonOmitir", controls, "OMITIR INTRODUCCIÓN", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -2f), new Vector2(348f, 117f), 19.5f, bold, out _);

        // ---- Datos de las escenas ----
        controller.shots = new List<IntroCinematicController.IntroShot>();
        string[] shotNames =
        {
            "1 El ultimo turno", "2 Salir a casa", "3 Tres amigas", "4 La interseccion", "5 Un solo instante", "6 Cuatro almas",
            "7 Segunda oportunidad", "8 Un mundo en peligro", "9 Nuevo amanecer", "10 Primera mision", "11 Logo"
        };

        for (int i = 0; i < 11; i++)
        {
            controller.shots.Add(new IntroCinematicController.IntroShot
            {
                name = shotNames[i],
                image = FindSprite(i + 1),
                duration = Durations[i],
                fadeIn = (i == 0 || i == 5) ? 0f : 0.5f, // la escena 6 sale del negro del choque
                voice = i < 10 ? FindAudio(VoiceFolder, "VO_" + (i + 1).ToString("00") + "_") : null,
                voiceDelay = VoiceDelays[i],
                voiceVolume = 1f
            });
        }

        controller.effects = new List<IntroCinematicController.IntroEffect>
        {
            Fx("01 Lluvia nocturna", "SFX_01_", 2, 0f, true, 5, 0.8f, true),
            Fx("02 Interior automovil", "SFX_02_", 3, 0f, true, 4, 0.85f, true),
            Fx("03 Accidente (empieza en la escena 5; el golpe cae a los 9 s)", "SFX_03_", 5, 6.65f, false, 5, 1f, false),
            Fx("04 Transicion de almas", "SFX_04_", 6, 0f, false, 6, 1f, true),
            Fx("05 Presencia divina", "SFX_05_", 7, 0f, false, 7, 1f, true),
            Fx("06 Invasion (escena 8)", "SFX_06_", 8, 0f, true, 8, 0.85f, true),
            Fx("07 Amanecer de Aethel", "SFX_07_", 9, 0f, true, 9, 0.85f, true),
            Fx("06 Invasion (escena 10)", "SFX_06_", 10, 0f, true, 10, 0.85f, true),
            Fx("08 Logo (el pico coincide con la aparicion)", "SFX_08_", 11, -1.8f, false, 11, 1f, false)
        };

        controller.music = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
        controller.logoImage = logoImage;
        controller.layers = layers;
        controller.fitters = fitters;
        controller.blackOverlay = black;
        controller.skipButton = skip;
        controller.pauseButton = pause;
        controller.pauseButtonLabel = pauseLabel;
        controller.pausePanel = pausePanel.gameObject;
        controller.resumeButton = resume;
        controller.subtitleGroup = subtitleGroup;
        controller.subtitleBox = subtitleBox;
        controller.subtitleText = subtitleText;
        controller.subtitleSpeaker = subtitleSpeaker;
        controller.subtitles = new List<IntroCinematicController.SubtitleCue>();

        foreach (SubtitleLine line in SubtitleTable)
            controller.subtitles.Add(new IntroCinematicController.SubtitleCue { shot = line.shot, start = line.start, end = line.end, text = line.text, speaker = Speakers[line.shot - 1] });

        controller.pauseSkipButton = pauseSkip;
        controller.nextScene = "Map";
        EditorUtility.SetDirty(controller);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        string menu = ConfigureMenu();
        return "Escena construida: " + controller.shots.Count + " escenas, " + controller.effects.Count + " efectos, musica " +
               (controller.music != null ? controller.music.length.ToString("0.0") + " s" : "FALTA") + ". " + menu;
    }

    // Nueva partida abre la introduccion (la sesion ya la inicia el menu antes de cargar la escena).
    static string ConfigureMenu()
    {
        var scene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
        MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);

        if (menu == null)
            return "No se encontro MainMenuController en Menu.unity.";

        SerializedObject so = new SerializedObject(menu);
        so.FindProperty("newGameScene").stringValue = "IntroCinematic";
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Menu: Nueva partida -> IntroCinematic.";
    }

    static void ConfigureMusicImport()
    {
        // La musica (160 s, 42 MB) se lee en streaming para no descomprimirla entera en memoria.
        AudioImporter importer = AssetImporter.GetAtPath(MusicPath) as AudioImporter;

        if (importer == null)
            return;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        if (settings.loadType == AudioClipLoadType.Streaming)
            return;

        settings.loadType = AudioClipLoadType.Streaming;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
    }

    static string CheckAssets()
    {
        string missing = "";

        for (int i = 1; i <= 11; i++)
            if (FindSprite(i) == null)
                missing += "- imagen de la escena " + i + "\n";

        for (int i = 1; i <= 10; i++)
            if (FindAudio(VoiceFolder, "VO_" + i.ToString("00") + "_") == null)
                missing += "- voz VO_" + i.ToString("00") + "\n";

        for (int i = 1; i <= 8; i++)
            if (FindAudio(SfxFolder, "SFX_" + i.ToString("00") + "_") == null)
                missing += "- efecto SFX_" + i.ToString("00") + "\n";

        if (AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath) == null)
            missing += "- musica\n";

        if (AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath) == null)
            missing += "- logo\n";

        return missing;
    }

    static Sprite FindSprite(int number)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { ImagesFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Match m = Regex.Match(System.IO.Path.GetFileNameWithoutExtension(path), @"(\d+)\s*$");

            if (m.Success && int.Parse(m.Groups[1].Value) == number)
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        return null;
    }

    static AudioClip FindAudio(string folder, string prefix)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (System.IO.Path.GetFileName(path).StartsWith(prefix))
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        return null;
    }

    static IntroCinematicController.IntroEffect Fx(string label, string prefix, int startShot, float offset, bool loop, int endShot, float volume, bool ambient)
    {
        return new IntroCinematicController.IntroEffect
        {
            name = label,
            clip = FindAudio(SfxFolder, prefix),
            startShot = startShot,
            offset = offset,
            loop = loop,
            endShot = endShot,
            volume = volume,
            ambient = ambient,
            fadeIn = 0.5f,
            fadeOut = 1.2f
        };
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static TMP_Text Label(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();

        if (font != null)
            label.font = font;

        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    // Mismos valores que los botones del menu de pausa del mapa (MapPauseMenu.MenuButton): menuNormal 9-slice con
    // multiplicador 4,49 a 464 x 156, texto marfil #E8DCC6 de 26 pt con espaciado 2 (auto 18-26) y margen de 34 px,
    // cambio de sprite menuHover / menuDisable y MenuButtonLabel. Los botones mas pequenos escalan todo con
    // size.y / 156 para conservar las proporciones.
    static Button MakeButton(string name, Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, float fontSize, TMP_FontAsset font, out TMP_Text label)
    {
        float s = size.y / 156f;
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/menuNormal.png");
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 4.49f / s;
        image.color = Color.white;

        label = Label("Text (TMP)", go.transform, text, fontSize, new Color32(0xE8, 0xDC, 0xC6, 0xFF), font, TextAlignmentOptions.Center);
        label.characterSpacing = 2f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f * s;
        label.fontSizeMax = fontSize;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(34f * s, 0f);
        labelRect.offsetMax = new Vector2(-34f * s, 0f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        Sprite hover = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/menuHover.png");
        button.spriteState = new SpriteState
        {
            highlightedSprite = hover,
            pressedSprite = hover,
            selectedSprite = hover,
            disabledSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/menuDisable.png")
        };
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        go.AddComponent<MenuButtonLabel>();
        return button;
    }
}
