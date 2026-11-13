using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Construye la escena "NoelleDialogo" (evento "La mascara rota", Santuario Velado) con el mismo sistema visual de
// IntroDialogo y ValdoriaDialogo (DialogueController). El nodo del evento ya existia en la progresion (B1 > EVT_NOELLE > B2):
// solo cambia su presentacion, del panel provisional a esta escena. NO toca IntroDialogo ni ValdoriaDialogo.
//
// Fondo: no hay una ilustracion del Santuario Velado en el proyecto. Si existe Assets/Sprites/Backgrounds/SantuarioVelado.png
// se usa; si no, se muestra de forma provisional un recorte del mapa de Aethel sobre el santuario. Para poner la definitiva:
// en la escena, objeto Fondo > "FondoSantuario", asignar el sprite (o crear el archivo con ese nombre y reconstruir).
public static class NoelleDialogueBuilder
{
    const string NoelleScene = "Assets/Scenes/NoelleDialogo.unity";
    const string ValdoriaScene = "Assets/Scenes/ValdoriaDialogo.unity";
    const string DataPath = "Assets/ScriptableObjects/Dialogues/NoelleMascaraRota.asset";
    const string ShrineBackground = "Assets/Sprites/Backgrounds/SantuarioVelado.png";
    const string MapBackground = "Assets/Sprites/Backgrounds/Newmap.png";
    const string SpriteFolder = "Assets/Sprites/DialogosUI/";

    // (hablante, texto, luz). Hablante vacio = narracion (cursiva, sin placa, los personajes siguen en pantalla).
    static readonly (string speaker, string text, DialogueLight light)[] Script =
    {
        ("Elaina", "Este lugar es extraño… Parece abandonado, pero las piedras todavía brillan.", DialogueLight.None),
        ("Evanora", "Puedo sentir una magia muy antigua. Esperen… está reaccionando a Noelle.", DialogueLight.None),
        ("Noelle", "¿A mí? No… Hay algo… una voz que no consigo reconocer.", DialogueLight.None),
        ("", "Un destello ilumina el santuario. Una fina grieta aparece en la máscara de Noelle.", DialogueLight.Flash),
        ("Noelle", "Lluvia… unas luces muy fuertes… Recuerdo que alguien tomó mi mano.", DialogueLight.None),
        ("Elaina", "¡Noelle! ¿Estás recordando aquella noche? Estábamos juntas.", DialogueLight.None),
        ("Noelle", "No puedo verlo con claridad… Solo recuerdo que no quería soltarlas.", DialogueLight.None),
        ("Evanora", "No tienes que recordarlo todo ahora. Lo importante es que seguimos juntas.", DialogueLight.None),
        ("Valerian", "Un recuerdo a la vez. Forzarte no hará que vuelvan más rápido.", DialogueLight.None),
        ("Noelle", "Gracias… Todavía no entiendo qué me ocurrió ni por qué llevo esta máscara. Pero encontraré las respuestas.", DialogueLight.None),
        ("", "La luz del santuario se apaga lentamente. La máscara permanece sobre su rostro, ahora con una pequeña grieta.", DialogueLight.FadeOut),
    };

    [MenuItem("Reencarnacion Express/Construir escena de La mascara rota (Noelle)")]
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
        foreach (var (speaker, text, lightEffect) in Script)
            data.lines.Add(new DialogueData.Line(speaker, text, lightEffect));

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

        // Fondo (ver el comentario del archivo).
        GameObject bgCanvas = new GameObject("Fondo", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        bgCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        bgCanvas.GetComponent<Canvas>().sortingOrder = 0;
        CanvasScaler bgScaler = bgCanvas.GetComponent<CanvasScaler>();
        bgScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        bgScaler.referenceResolution = new Vector2(1920f, 1080f);
        bgScaler.matchWidthOrHeight = 0.5f;
        Sprite shrine = AssetDatabase.LoadAssetAtPath<Sprite>(ShrineBackground);
        GameObject bgGo;
        string bgReport;

        if (shrine != null)
        {
            bgGo = new GameObject("FondoSantuario", typeof(RectTransform), typeof(Image));
            Image img = bgGo.GetComponent<Image>();
            img.sprite = shrine;
            img.preserveAspect = false;
            img.raycastTarget = false;
            bgReport = "fondo: SantuarioVelado.png";
        }
        else
        {
            // Provisional: recorte del mapa de Aethel sobre el Santuario Velado (sin recursos nuevos).
            bgGo = new GameObject("FondoSantuario", typeof(RectTransform), typeof(RawImage));
            RawImage raw = bgGo.GetComponent<RawImage>();
            Sprite map = AssetDatabase.LoadAssetAtPath<Sprite>(MapBackground);
            raw.texture = map != null ? map.texture : null;
            raw.uvRect = new Rect(0.52f, 0.05f, 0.46f, 0.46f);
            raw.raycastTarget = false;
            bgReport = "fondo PROVISIONAL (recorte del mapa); falta SantuarioVelado.png";
        }

        bgGo.transform.SetParent(bgCanvas.transform, false);
        RectTransform bgRect = (RectTransform)bgGo.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

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

        // Luz del santuario (efecto sencillo): resplandor dorado a pantalla completa, detras de retratos y cuadro.
        RectTransform lightRect = Rect("Luz", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(lightRect);
        Image light = lightRect.gameObject.AddComponent<Image>();
        light.color = new Color(1f, 0.90f, 0.58f, 0f);
        light.raycastTarget = false;

        RectTransform panelRect = Rect("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(panelRect);
        CanvasGroup group = panelRect.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

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

        TMP_Text spaceHint = Text("PistaEspacio", box, new Color(0.78f, 0.74f, 0.62f, 0.85f), regular, 20f, TextAlignmentOptions.BottomRight);
        spaceHint.text = "Continuar con espacio";
        spaceHint.rectTransform.anchorMin = spaceHint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        spaceHint.rectTransform.sizeDelta = new Vector2(1660f, 36f);
        spaceHint.rectTransform.anchoredPosition = new Vector2(0f, -66f);
        spaceHint.textWrappingMode = TextWrappingModes.NoWrap;

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
        so.FindProperty("eventId").stringValue = GameSession.NoelleEventId;
        so.FindProperty("liberateNoelle").boolValue = true;
        so.FindProperty("hidePortraitsOnEmptySpeaker").boolValue = false;
        so.FindProperty("narrationItalic").boolValue = true;
        so.FindProperty("lightImage").objectReferenceValue = light;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, NoelleScene);

        // Segunda asignacion tras guardar (la referencia al asset recien importado a veces no se conserva a la primera).
        DialogueController saved = Object.FindAnyObjectByType<DialogueController>(FindObjectsInactive.Include);
        SerializedObject savedSo = new SerializedObject(saved);
        savedSo.FindProperty("dialogue").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueData>(DataPath);
        savedSo.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, NoelleScene);

        // Build Settings: NoelleDialogo despues de ValdoriaDialogo.
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == NoelleScene);
        int index = scenes.FindIndex(s => s.path == ValdoriaScene);
        scenes.Insert(index >= 0 ? index + 1 : scenes.Count, new EditorBuildSettingsScene(NoelleScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        return "Escena NoelleDialogo construida: " + data.lines.Count + " lineas; " + bgReport + "; agregada a Build Settings.";
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
