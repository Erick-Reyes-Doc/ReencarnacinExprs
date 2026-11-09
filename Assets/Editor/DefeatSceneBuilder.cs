using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Construye la escena "Defeat": muy oscura, con la palabra DERROTA en rojo apagado, cuatro sombras de los heroes caidos
// (retratos oscurecidos con una brasa roja tenue detras), un texto breve y un boton "Volver al menu". 1920 x 1080 con Canvas Scaler.
// Se ejecuta desde "Reencarnacion Express/Construir escena Defeat".
public static class DefeatSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Defeat.unity";
    static readonly Color Ivory = new Color(0.78f, 0.74f, 0.68f, 1f);
    static readonly Color Blood = new Color(0.72f, 0.13f, 0.13f, 1f);

    static TMP_FontAsset bold, regular;

    [MenuItem("Reencarnacion Express/Construir escena Defeat")]
    public static void BuildMenu() { Debug.Log(Build()); }

    static Sprite Sp(string path) { return AssetDatabase.LoadAssetAtPath<Sprite>(path); }

    static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    static Image Img(RectTransform r, Sprite sprite, Color color, bool raycast = false)
    {
        Image img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    static TMP_Text Txt(string name, Transform parent, Vector2 pos, Vector2 size, float fontSize, bool isBold, Color color, string text)
    {
        RectTransform r = Rect(name, parent, pos, size);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = isBold ? bold : regular;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.text = text;
        t.raycastTarget = false;
        t.richText = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    public static string Build()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            return "Sal de Play antes de construir.";

        regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherRegular_SDF.asset");
        bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherBold_SDF.asset");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        Camera cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;

        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));

        GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DefeatController));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGo.transform;

        // Fondo (el del encuentro perdido, apagado en tiempo de ejecucion) + velos oscuros + vineta
        RectTransform bgRect = Rect("Fondo", root, Vector2.zero, Vector2.zero);
        Stretch(bgRect);
        Image bg = Img(bgRect, null, new Color(0.03f, 0.03f, 0.05f, 1f));

        RectTransform veil = Rect("Velo", root, Vector2.zero, Vector2.zero);
        Stretch(veil);
        Img(veil, null, new Color(0.01f, 0.01f, 0.03f, 0.84f));

        Sprite glowSprite = Sp("Assets/Sprites/DialogosUI/BrilloRadial.png");

        // Titulo DERROTA
        RectTransform titleRect = Rect("Titulo", root, new Vector2(0f, 300f), new Vector2(1200f, 200f));
        CanvasGroup titleGroup = titleRect.gameObject.AddComponent<CanvasGroup>();
        TMP_Text title = Txt("Texto", titleRect, Vector2.zero, new Vector2(1200f, 200f), 150f, true, Blood, "DERROTA");
        title.characterSpacing = 18f;
        title.outlineWidth = 0.18f;
        title.outlineColor = new Color32(12, 2, 2, 255);

        // Sombras de los heroes caidos (retratos muy oscurecidos, ligeramente inclinados y bajados)
        string[,] heroes = { { "Evanora", "Evanora" }, { "Elaina", "Elaina" }, { "Noelle", "Noelle" }, { "Valerian", "Valerian" } };
        float[] tilt = { 7f, -5f, 6f, -8f };
        float[] drop = { 0f, -14f, -6f, -18f };
        List<DefeatController.Shadow> shadows = new List<DefeatController.Shadow>();

        for (int i = 0; i < 4; i++)
        {
            float x = -495f + i * 330f;
            RectTransform holder = Rect("Sombra_" + heroes[i, 0], root, new Vector2(x, 20f + drop[i]), new Vector2(260f, 260f));
            CanvasGroup group = holder.gameObject.AddComponent<CanvasGroup>();

            RectTransform glowRect = Rect("Brasa", holder, new Vector2(0f, -10f), new Vector2(520f, 520f));
            Image glow = Img(glowRect, glowSprite, new Color(0.65f, 0.08f, 0.08f, 0.15f));

            RectTransform body = Rect("Cuerpo", holder, Vector2.zero, new Vector2(230f, 230f));
            body.localRotation = Quaternion.Euler(0f, 0f, tilt[i]);
            Img(body, null, new Color(0.35f, 0.07f, 0.07f, 0.9f));                 // contorno rojo apagado
            RectTransform back = Rect("Fondo", body, Vector2.zero, new Vector2(224f, 224f));
            Img(back, null, new Color(0.02f, 0.02f, 0.03f, 1f));
            RectTransform portraitRect = Rect("Retrato", body, Vector2.zero, new Vector2(224f, 224f));
            Image portrait = Img(portraitRect, Sp("Assets/Sprites/Personajes/Iconos/" + heroes[i, 1] + ".png"), new Color(0.13f, 0.13f, 0.16f, 1f));
            portrait.preserveAspect = true;

            TMP_Text name = Txt("Nombre", holder, new Vector2(0f, -150f), new Vector2(300f, 40f), 28f, false, new Color(0.55f, 0.52f, 0.50f, 0.7f), heroes[i, 0]);
            name.fontStyle = FontStyles.Italic;

            shadows.Add(new DefeatController.Shadow { group = group, portrait = portrait, glow = glow });
        }

        // Subtitulo
        RectTransform subRect = Rect("Subtitulo", root, new Vector2(0f, -250f), new Vector2(1400f, 80f));
        CanvasGroup subtitleGroup = subRect.gameObject.AddComponent<CanvasGroup>();
        TMP_Text sub = Txt("Texto", subRect, Vector2.zero, new Vector2(1400f, 80f), 38f, false, Ivory, "Los héroes han caído... y el viaje termina aquí.");
        sub.fontStyle = FontStyles.Italic;

        // Boton Volver al menu
        RectTransform button = Rect("BotonMenu", root, new Vector2(0f, -400f), new Vector2(490f, 163f));
        CanvasGroup buttonGroup = button.gameObject.AddComponent<CanvasGroup>();
        Image buttonImage = Img(button, Sp("Assets/Sprites/Buttons/Misiones.png"), new Color(0.80f, 0.80f, 0.86f, 1f), true);
        buttonImage.preserveAspect = true;
        buttonImage.raycastPadding = new Vector4(11f, 34f, 11f, 31f);
        Button menuButton = button.gameObject.AddComponent<Button>();
        menuButton.targetGraphic = buttonImage;
        menuButton.transition = Selectable.Transition.None;
        button.gameObject.AddComponent<HoverScale>().HoverFactor = 1.03f;
        Txt("Texto", button, new Vector2(0f, 1f), new Vector2(440f, 80f), 40f, true, new Color(0.93f, 0.90f, 0.82f, 1f), "Volver al menú");

        // Fundido a negro (entrada y salida)
        RectTransform fadeRect = Rect("FundidoNegro", root, Vector2.zero, Vector2.zero);
        Stretch(fadeRect);
        Image fade = Img(fadeRect, null, Color.black, true);

        // Controlador
        DefeatController controller = canvasGo.GetComponent<DefeatController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("titleGroup").objectReferenceValue = titleGroup;
        so.FindProperty("titleRect").objectReferenceValue = titleRect;
        so.FindProperty("subtitleGroup").objectReferenceValue = subtitleGroup;
        so.FindProperty("buttonGroup").objectReferenceValue = buttonGroup;
        so.FindProperty("menuButton").objectReferenceValue = menuButton;
        so.FindProperty("fade").objectReferenceValue = fade;
        so.FindProperty("menuScene").stringValue = "Menu";
        SerializedProperty list = so.FindProperty("shadows");
        list.arraySize = shadows.Count;

        for (int i = 0; i < shadows.Count; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("group").objectReferenceValue = shadows[i].group;
            e.FindPropertyRelative("portrait").objectReferenceValue = shadows[i].portrait;
            e.FindPropertyRelative("glow").objectReferenceValue = shadows[i].glow;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        return "Escena Defeat construida y agregada a Build Settings.";
    }
}
