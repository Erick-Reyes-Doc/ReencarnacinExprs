using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Construye la escena "ValdoriaDialogo" (evento "El informe del ultimo vigia") reutilizando el DialogueController y el
// estilo visual de IntroDialogo, y conecta la ciudad de Valdoria como nodo narrativo en el mapa.
// NO toca IntroDialogo ni IntroCinematic (la escena de la introduccion tiene ajustes visuales propios y su constructor,
// DialogueSceneBuilder, no debe ejecutarse). Solo modifica: ValdoriaDialogo (nueva), Map (lugar de Valdoria) y Build Settings.
public static class ValdoriaDialogueBuilder
{
    const string MapScene = "Assets/Scenes/Map.unity";
    const string ValdoriaScene = "Assets/Scenes/ValdoriaDialogo.unity";
    const string IntroDialogueScene = "Assets/Scenes/IntroDialogo.unity";
    const string DataPath = "Assets/ScriptableObjects/Dialogues/ValdoriaInforme.asset";
    const string BackgroundSprite = "Assets/Sprites/Backgrounds/Valdoria.png";
    const string SpriteFolder = "Assets/Sprites/DialogosUI/";
    const string Officer = "Oficial de la ciudad";

    static readonly (string speaker, string text)[] Script =
    {
        (Officer, "Los informes del Refugio del Vigía confirman nuestros temores. Los ataques en la pradera y el bosque no son incidentes aislados."),
        ("Elaina", "¿Entonces alguien está controlando a esas criaturas?"),
        (Officer, "Así es. El responsable es el Capitán de las Fuerzas Expedicionarias, uno de los comandantes que sirven al Rey Demonio."),
        ("Evanora", "Eso explicaría por qué la corrupción se está extendiendo. ¿Dónde se encuentra?"),
        (Officer, "Nuestros exploradores lo han localizado en el Trono Sellado. Pero las Tierras Marchitas están infestadas de criaturas corrompidas."),
        ("Noelle", "Entonces tendremos que atravesarlas para llegar hasta él."),
        ("Valerian", "Fantástico. Cada vez que parece que estamos cerca de terminar, el trabajo se vuelve más peligroso."),
        (Officer, "Si logran alcanzar el Trono Sellado y derrotar al Capitán, podrían detener el avance de sus fuerzas en esta región."),
    };

    [MenuItem("Reencarnacion Express/Construir escena de Valdoria (informe del ultimo vigia)")]
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
        data = AssetDatabase.LoadAssetAtPath<DialogueData>(DataPath);

        // --- Escena nueva ---
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camGo = new GameObject("Main Camera", typeof(Camera));
        camGo.tag = "MainCamera";
        Camera cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;

        Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + n + ".png");
        TMP_FontAsset regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherRegular_SDF.asset");
        TMP_FontAsset bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherBold_SDF.asset");
        Color ivory = new Color(0.93f, 0.90f, 0.82f, 1f);
        Color gold = new Color(0.96f, 0.86f, 0.58f, 1f);

        // Fondo: ilustracion de Valdoria.
        GameObject bgCanvas = new GameObject("FondoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        bgCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        bgCanvas.GetComponent<Canvas>().sortingOrder = 0;
        CanvasScaler bgScaler = bgCanvas.GetComponent<CanvasScaler>();
        bgScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        bgScaler.referenceResolution = new Vector2(1920f, 1080f);
        bgScaler.matchWidthOrHeight = 0.5f;
        GameObject bgGo = new GameObject("FondoValdoria", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(bgCanvas.transform, false);
        RectTransform bgRect = (RectTransform)bgGo.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        Image bg = bgGo.GetComponent<Image>();
        bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSprite);
        bg.preserveAspect = false;
        bg.raycastTarget = false;

        GameObject canvasGo = new GameObject("DialogoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DialogueController));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
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

        RectTransform blockerRect = Rect("Bloqueo", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(blockerRect);
        Image blocker = blockerRect.gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = false;

        RectTransform panelRect = Rect("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(panelRect);
        CanvasGroup group = panelRect.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // Retratos de los cuatro protagonistas a la izquierda (mismas medidas que IntroDialogo).
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

        // Oficial de la ciudad: a la derecha, mirando hacia los heroes (la ilustracion ya mira a la izquierda).
        RectTransform officerRect = Rect("Retrato_Oficial", portraitsRect, new Vector2(1f, 0f), new Vector2(-60f, 190f), new Vector2(420f, 420f));
        officerRect.pivot = new Vector2(1f, 0f);
        Image officerImage = officerRect.gameObject.AddComponent<Image>();
        officerImage.sprite = Load("OficialDialogo");
        officerImage.preserveAspect = true;
        officerImage.raycastTarget = false;
        slots.Add(new DialogueController.PortraitSlot { speaker = Officer, image = officerImage });

        const float BoxW = 1800f;
        float boxH = BoxW * 724f / 2172f;
        RectTransform box = Rect("Cuadro", panelRect, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(BoxW, boxH));
        box.pivot = new Vector2(0.5f, 0.5f);
        box.anchoredPosition = new Vector2(0f, 135f);
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

        // Placa de nombre (con "Oficial de la ciudad" se ensancha para que el nombre quepa).
        const float PlateW = 560f;
        float plateH = PlateW * 724f / 2172f;
        RectTransform plate = Rect("Placa", panelRect, new Vector2(0f, 0f), new Vector2(60f + PlateW * 0.5f, 135f + 95f + 28f), new Vector2(PlateW, plateH));
        plate.pivot = new Vector2(0.5f, 0.5f);
        Image plateImage = plate.gameObject.AddComponent<Image>();
        plateImage.sprite = Load("PlacaNombre");
        plateImage.preserveAspect = true;
        plateImage.raycastTarget = false;

        TMP_Text nameText = Text("Nombre", plate, gold, bold, 34f, TextAlignmentOptions.Center);
        nameText.rectTransform.anchorMin = nameText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        nameText.rectTransform.sizeDelta = new Vector2(470f, 60f);
        nameText.rectTransform.anchoredPosition = Vector2.zero;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;

        // Aviso final: "Nueva amenaza descubierta: Trono Sellado" (placa dorada centrada, aparece y se desvanece).
        RectTransform noticeRect = Rect("Aviso", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1240f, 1240f * 724f / 2172f));
        CanvasGroup noticeGroup = noticeRect.gameObject.AddComponent<CanvasGroup>();
        noticeGroup.alpha = 0f;
        noticeGroup.interactable = false;
        noticeGroup.blocksRaycasts = false;
        Image noticeImage = noticeRect.gameObject.AddComponent<Image>();
        noticeImage.sprite = Load("PlacaNombre");
        noticeImage.preserveAspect = true;
        noticeImage.raycastTarget = false;
        TMP_Text noticeText = Text("Texto", noticeRect, gold, bold, 44f, TextAlignmentOptions.Center);
        noticeText.rectTransform.anchorMin = noticeText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        noticeText.rectTransform.sizeDelta = new Vector2(1060f, 120f);
        noticeText.rectTransform.anchoredPosition = Vector2.zero;
        noticeText.textWrappingMode = TextWrappingModes.NoWrap;

        // Fundido a negro de entrada y salida.
        RectTransform blackRect = Rect("FundidoNegro", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(blackRect);
        Image black = blackRect.gameObject.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;

        DialogueController controller = canvasGo.GetComponent<DialogueController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("dialogue").objectReferenceValue = data;
        so.FindProperty("panel").objectReferenceValue = group;
        so.FindProperty("blocker").objectReferenceValue = blocker;
        so.FindProperty("blackOverlay").objectReferenceValue = black;
        so.FindProperty("namePlate").objectReferenceValue = plate.gameObject;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.FindProperty("portraitsGroup").objectReferenceValue = portraitsGroup;
        SerializedProperty list = so.FindProperty("portraits");
        list.arraySize = slots.Count;

        for (int i = 0; i < slots.Count; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("speaker").stringValue = slots[i].speaker;
            element.FindPropertyRelative("image").objectReferenceValue = slots[i].image;
        }

        so.FindProperty("nextScene").stringValue = "Map";
        so.FindProperty("startEncounterId").stringValue = "";
        so.FindProperty("combatTutorial").boolValue = false;
        so.FindProperty("eventId").stringValue = GameSession.ValdoriaEventId;
        so.FindProperty("noticeMessage").stringValue = "Nueva amenaza descubierta: Trono Sellado";
        so.FindProperty("noticeGroup").objectReferenceValue = noticeGroup;
        so.FindProperty("noticeText").objectReferenceValue = noticeText;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ValdoriaScene);

        // Segunda asignacion tras guardar (la referencia al asset recien importado a veces no se conserva a la primera).
        DialogueController saved = Object.FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        SerializedObject savedSo = new SerializedObject(saved);
        savedSo.FindProperty("dialogue").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueData>(DataPath);
        savedSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ValdoriaScene);

        return "Escena ValdoriaDialogo construida: " + data.lines.Count + " lineas.\n" + ConnectMap();
    }

    // Build Settings (ValdoriaDialogo despues de IntroDialogo) y el lugar "city" en el mapa.
    static string ConnectMap()
    {
        string report = "";

        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == ValdoriaScene);
        int index = list.FindIndex(s => s.path == IntroDialogueScene);
        list.Insert(index >= 0 ? index + 1 : list.Count, new EditorBuildSettingsScene(ValdoriaScene, true));
        EditorBuildSettings.scenes = list.ToArray();
        report += "- ValdoriaDialogo agregada a Build Settings.\n";

        var map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
        var controller = Object.FindAnyObjectByType<WorldMapController>(FindObjectsInactive.Include);

        if (controller == null)
            return report + "- No se encontro WorldMapController en Map.";

        SerializedObject so = new SerializedObject(controller);
        SerializedProperty locations = so.FindProperty("locations");
        bool exists = false;

        for (int i = 0; i < locations.arraySize; i++)
            if (locations.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == "city")
                exists = true;

        if (!exists)
        {
            // Valdoria se inserta antes del Claro del Susurro (orden del resto de lugares intacto).
            int at = locations.arraySize;

            for (int i = 0; i < locations.arraySize; i++)
                if (locations.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == "clearing") { at = i; break; }

            locations.InsertArrayElementAtIndex(at);
            SerializedProperty e = locations.GetArrayElementAtIndex(at);
            e.FindPropertyRelative("id").stringValue = "city";
            e.FindPropertyRelative("displayName").stringValue = "Ciudad de Valdoria";
            e.FindPropertyRelative("zone").stringValue = MapProgression.Forest;
            e.FindPropertyRelative("position").vector2Value = new Vector2(0.4542f, 0.5130f);
            SerializedProperty ids = e.FindPropertyRelative("encounterIds");
            ids.arraySize = 1;
            ids.GetArrayElementAtIndex(0).stringValue = GameSession.ValdoriaEventId;
            so.ApplyModifiedPropertiesWithoutUndo();
            report += "- Map: lugar 'city' agregado con el evento de Valdoria.\n";
        }
        else
        {
            report += "- Map: el lugar 'city' ya existia.\n";
        }

        controller.RebuildMedallionsInEditor();   // genera el medallon y enlaza la etiqueta existente "Ciudad de Valdoria"
        EditorSceneManager.MarkSceneDirty(map);
        EditorSceneManager.SaveScene(map);
        report += "- Map: medallones reconstruidos.\n";
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
