using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Construye la escena "IntroDialogo" (mapa de Aethel de fondo, sin iconos ni HUD, y la interfaz de conversacion del
// Altar de los Invocados), crea el asset de dialogo y deja la escena Map con un fundido de entrada.
// Flujo: IntroCinematic -> IntroDialogo -> Map. Los textos se editan despues en el asset (Assets/ScriptableObjects/Dialogues).
public static class DialogueSceneBuilder
{
    const string MapScene = "Assets/Scenes/Map.unity";
    const string DialogueScene = "Assets/Scenes/IntroDialogo.unity";
    const string IntroScene = "Assets/Scenes/IntroCinematic.unity";
    const string BackgroundSprite = "Assets/Sprites/Backgrounds/Newmap.png";   // el mismo fondo del mapa
    const string DataPath = "Assets/ScriptableObjects/Dialogues/AltarDeLosInvocados.asset";
    const string SpriteFolder = "Assets/Sprites/DialogosUI/";

    static readonly (string speaker, string text)[] Script =
    {
        ("Elaina", "¿Estamos… en otro mundo? No puede ser. ¡Miren mis orejas!"),
        ("Evanora", "Esto es increíble… Puedo sentir la magia a nuestro alrededor. Es como si siempre hubiera estado aquí."),
        ("Noelle", "…"),
        ("Elaina", "¿Noelle? ¿Estás bien? Desde que despertamos no has dicho una palabra."),
        ("Noelle", "Estoy bien. Solo… necesito entender qué está pasando."),
        ("Valerian", "Genial. Morí después de treinta y cuatro horas de trabajo y ahora estoy en un bosque. ¿Aquí también se trabaja los domingos?"),
        ("Dios", "Bienvenidos a Aethel, héroes. Este lugar es conocido como el Altar de los Invocados."),
        ("Elaina", "¡Esa voz! Tú eres quien nos habló antes de llegar aquí, ¿verdad?"),
        ("Dios", "Así es. Los guiaré durante su viaje. A través de nuestro vínculo, podré orientar sus acciones y ayudarlos en sus combates."),
        ("Valerian", "¿Combates? Espera un momento. ¿No existe una opción en la que simplemente podamos descansar?"),
        ("Evanora", "Valerian, acabamos de llegar. Al menos deberíamos escuchar qué necesita este mundo."),
        ("Dios", "En el Campamento del Alba, un cobrador ha desaparecido mientras recorría los caminos de la pradera. Su destino podría estar relacionado con los ataques recientes."),
        ("Elaina", "Entonces debemos encontrarlo. Quizá todavía podamos ayudarlo."),
        ("Valerian", "Supongo que no tengo escapatoria… Está bien. Pero después de esto quiero una cama."),
        ("Dios", "Diríjanse al Campamento del Alba. Allí comenzará su primera misión: “Un mal día para cobrar”."),
        // Tutorial breve en el mismo cuadro (sin hablante).
        ("", "Presiona ESPACIO para completar el texto o avanzar en la conversación."),
        ("", "Tu primera misión comienza ahora: “Un mal día para cobrar”. Te mostraré cómo se combate."),
    };

    [MenuItem("Reencarnacion Express/Construir escena de dialogo del Altar")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        if (EditorApplication.isPlaying)
            return "Sal de Play antes de construir.";

        // --- Asset de dialogo ---
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/Dialogues"))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Dialogues");

        DialogueData data = AssetDatabase.LoadAssetAtPath<DialogueData>(DataPath);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<DialogueData>();
            AssetDatabase.CreateAsset(data, DataPath);
        }

        data.lines = new List<DialogueData.Line>();
        foreach (var (speaker, text) in Script)
            data.lines.Add(new DialogueData.Line(speaker, text));

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(DataPath, ImportAssetOptions.ForceSynchronousImport);
        data = AssetDatabase.LoadAssetAtPath<DialogueData>(DataPath);   // referencia del asset ya importado

        // --- Escena nueva: camara, fondo (mapa) y dialogo ---
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        Camera cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.cullingMask = 0;   // solo interfaz

        Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + n + ".png");
        TMP_FontAsset regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherRegular_SDF.asset");
        TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherBold_SDF.asset");
        Color ivory = new Color(0.93f, 0.90f, 0.82f, 1f);
        Color gold = new Color(0.96f, 0.86f, 0.58f, 1f);

        // Fondo: solo el mapa ilustrado, sin marcadores, etiquetas ni HUD.
        GameObject bgCanvas = new GameObject("FondoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        bgCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        bgCanvas.GetComponent<Canvas>().sortingOrder = 0;
        CanvasScaler bgScaler = bgCanvas.GetComponent<CanvasScaler>();
        bgScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        bgScaler.referenceResolution = new Vector2(1920f, 1080f);
        bgScaler.matchWidthOrHeight = 0.5f;
        GameObject bgGo = new GameObject("MapaFondo", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(bgCanvas.transform, false);
        RectTransform bgRect = (RectTransform)bgGo.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        Image bgImage = bgGo.GetComponent<Image>();
        bgImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSprite);
        bgImage.raycastTarget = false;
        bgImage.preserveAspect = false;

        GameObject canvasGo = new GameObject("DialogoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DialogueController));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;   // por encima del mapa, de la tienda (100) y de la ficha
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGo.transform;

        RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = anchor;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        // Bloqueo de clics (transparente; solo capta mientras dura la conversacion).
        RectTransform blockerRect = Rect("Bloqueo", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(blockerRect);
        Image blocker = blockerRect.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = false;

        // Panel (CanvasGroup): retratos, cuadro y placa.
        RectTransform panelRect = Rect("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(panelRect);
        CanvasGroup group = panelRect.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // Retratos agrupados a la izquierda (no cambian de sitio al hablar). Se dibujan antes que el cuadro,
        // que tapa un poco su base.
        RectTransform portraitsRect = Rect("Retratos", panelRect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(portraitsRect);
        CanvasGroup portraitsGroup = portraitsRect.gameObject.AddComponent<CanvasGroup>();
        string[] names = { "Elaina", "Evanora", "Noelle", "Valerian" };
        var slots = new List<DialogueController.PortraitSlot>();
        const float PortraitSize = 400f, Step = 225f;

        for (int i = 0; i < names.Length; i++)
        {
            RectTransform r = Rect("Retrato_" + names[i], portraitsRect, new Vector2(0f, 0f), new Vector2(30f + i * Step, 190f), new Vector2(PortraitSize, PortraitSize));
            r.pivot = new Vector2(0f, 0f);
            Image img = r.gameObject.AddComponent<Image>();
            img.sprite = Load(names[i] + "Dialogo");
            img.preserveAspect = true;
            img.raycastTarget = false;
            slots.Add(new DialogueController.PortraitSlot { speaker = names[i], image = img });
        }

        // Cuadro de dialogo (2172 x 724 con margen transparente: se dibuja a escala uniforme; el marco visible
        // mide ~1765 x 190 px y queda centrado en el rectangulo).
        const float BoxW = 1800f;
        float boxH = BoxW * 724f / 2172f;
        RectTransform box = Rect("Cuadro", panelRect, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(BoxW, boxH));
        box.pivot = new Vector2(0.5f, 0.5f);
        box.anchoredPosition = new Vector2(0f, 135f);   // el marco visible (~190 px) queda entre y = 40 y y = 230 (no hay HUD en esta escena)
        Image boxImage = box.gameObject.AddComponent<Image>();
        boxImage.sprite = Load("CuadroDialogo");
        boxImage.preserveAspect = true;
        boxImage.raycastTarget = false;

        TMP_Text body = Text("Texto", box, ivory, regular, 30f, TextAlignmentOptions.TopLeft);
        body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        body.rectTransform.sizeDelta = new Vector2(1660f, 150f);
        body.rectTransform.anchoredPosition = Vector2.zero;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Overflow;
        body.enableAutoSizing = false;

        // Pista discreta en la parte inferior del cuadro.
        TMP_Text spaceHint = Text("PistaEspacio", box, new Color(0.78f, 0.74f, 0.62f, 0.85f), regular, 20f, TextAlignmentOptions.BottomRight);
        spaceHint.text = "Continuar con espacio";
        spaceHint.rectTransform.anchorMin = spaceHint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        spaceHint.rectTransform.sizeDelta = new Vector2(1660f, 36f);
        spaceHint.rectTransform.anchoredPosition = new Vector2(0f, -66f);
        spaceHint.textWrappingMode = TextWrappingModes.NoWrap;

        // Placa de nombre sobre el borde superior del cuadro, a la izquierda.
        const float PlateW = 460f;
        float plateH = PlateW * 724f / 2172f;
        RectTransform plate = Rect("Placa", panelRect, new Vector2(0f, 0f), new Vector2(60f + PlateW * 0.5f, 135f + 95f + 28f), new Vector2(PlateW, plateH));
        plate.pivot = new Vector2(0.5f, 0.5f);
        Image plateImage = plate.gameObject.AddComponent<Image>();
        plateImage.sprite = Load("PlacaNombre");
        plateImage.preserveAspect = true;
        plateImage.raycastTarget = false;

        TMP_Text nameText = Text("Nombre", plate, gold, bold, 34f, TextAlignmentOptions.Center);
        nameText.rectTransform.anchorMin = nameText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        nameText.rectTransform.sizeDelta = new Vector2(380f, 60f);
        nameText.rectTransform.anchoredPosition = Vector2.zero;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;

        // Fundido a negro de entrada (encima de todo).
        RectTransform blackRect = Rect("FundidoNegro", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(blackRect);
        Image black = blackRect.gameObject.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;

        // Conexion del controlador.
        DialogueController controller = canvasGo.GetComponent<DialogueController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("dialogue").objectReferenceValue = data;
        so.FindProperty("panel").objectReferenceValue = group;
        so.FindProperty("blocker").objectReferenceValue = blocker;
        so.FindProperty("blackOverlay").objectReferenceValue = black;
        so.FindProperty("namePlate").objectReferenceValue = plate.gameObject;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("bodyText").objectReferenceValue = body;
        SerializedProperty list = so.FindProperty("portraits");
        list.arraySize = slots.Count;

        for (int i = 0; i < slots.Count; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("speaker").stringValue = slots[i].speaker;
            element.FindPropertyRelative("image").objectReferenceValue = slots[i].image;
        }

        so.FindProperty("portraitsGroup").objectReferenceValue = portraitsGroup;
        so.FindProperty("nextScene").stringValue = "CombateIntegracion";
        so.FindProperty("startEncounterId").stringValue = "P1";
        so.FindProperty("combatTutorial").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, DialogueScene);

        // Segunda asignacion tras guardar: en algunas ejecuciones la primera referencia al asset recien importado no se conserva.
        DialogueController saved = Object.FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        SerializedObject savedSo = new SerializedObject(saved);
        savedSo.FindProperty("dialogue").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueData>(DataPath);
        savedSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, DialogueScene);

        string report = ConnectScenes();
        return "Escena IntroDialogo construida: " + data.lines.Count + " lineas (incluye 2 de tutorial).\n" + report;
    }

    // IntroCinematic -> IntroDialogo -> Map; Map pierde el dialogo y gana un fundido de entrada; Build Settings.
    static string ConnectScenes()
    {
        string report = "";

        // Build Settings: IntroDialogo justo despues de IntroCinematic.
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == DialogueScene);
        int index = list.FindIndex(s => s.path == IntroScene);
        list.Insert(index >= 0 ? index + 1 : list.Count, new EditorBuildSettingsScene(DialogueScene, true));
        EditorBuildSettings.scenes = list.ToArray();
        report += "- IntroDialogo agregada a Build Settings.\n";

        // La introduccion pasa al dialogo.
        var intro = EditorSceneManager.OpenScene(IntroScene, OpenSceneMode.Single);
        var controller = Object.FindAnyObjectByType<IntroCinematicController>(FindObjectsInactive.Include);

        if (controller != null)
        {
            controller.nextScene = "IntroDialogo";
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(intro);
            EditorSceneManager.SaveScene(intro);
            report += "- IntroCinematic.nextScene = IntroDialogo.\n";
        }

        // Map: sin dialogo, con fundido de entrada.
        var map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);

        foreach (GameObject sceneRoot in map.GetRootGameObjects())
            if (sceneRoot.name == "DialogoCanvas" || sceneRoot.name == "FundidoEntrada")
                Object.DestroyImmediate(sceneRoot);

        GameObject fadeCanvas = new GameObject("FundidoEntrada", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(SceneFadeIn));
        Canvas c = fadeCanvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 300;
        CanvasScaler s2 = fadeCanvas.GetComponent<CanvasScaler>();
        s2.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s2.referenceResolution = new Vector2(1920f, 1080f);
        GameObject blackGo = new GameObject("Negro", typeof(RectTransform), typeof(Image));
        blackGo.transform.SetParent(fadeCanvas.transform, false);
        RectTransform br = (RectTransform)blackGo.transform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.offsetMin = br.offsetMax = Vector2.zero;
        Image black = blackGo.GetComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;
        SerializedObject so = new SerializedObject(fadeCanvas.GetComponent<SceneFadeIn>());
        so.FindProperty("overlay").objectReferenceValue = black;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(map);
        EditorSceneManager.SaveScene(map);
        report += "- Map: dialogo retirado y fundido de entrada agregado.\n";
        return report;
    }

    static TMP_Text Text(string name, RectTransform parent, Color color, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.richText = false;
        return t;
    }
}
