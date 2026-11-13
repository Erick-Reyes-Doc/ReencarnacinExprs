using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Construye la escena "Victory" (pantalla de resultados tras ganar un combate) con elementos independientes de Unity UI y
// TextMeshPro sobre un Canvas de 1920 x 1080, siguiendo el boceto aprobado. Reutiliza los sprites del proyecto: marco de panel
// (MarcoInfo), banners (TituloReino), filas (MarcoInferior), boton (Misiones), medallon de espadas (Combate), retratos de los
// heroes (Personajes/Iconos), iconos de Coronas y el brillo radial. Los textos y numeros los rellena VictoryController con datos reales.
// Se ejecuta desde el menu "Reencarnacion Express/Construir escena Victory" (reconstruye solo esta escena).
public static class VictorySceneBuilder
{
    const string ScenePath = "Assets/Scenes/Victory.unity";

    static readonly Color Gold = new Color(0.96f, 0.86f, 0.58f, 1f);
    static readonly Color GoldLine = new Color(0.83f, 0.69f, 0.42f, 1f);
    static readonly Color Ivory = new Color(0.93f, 0.90f, 0.82f, 1f);
    static readonly Color Navy = new Color(0.063f, 0.082f, 0.118f, 0.96f);

    static TMP_FontAsset regular, bold;

    [MenuItem("Reencarnacion Express/Construir escena Victory")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

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

    static Image Img(RectTransform r, Sprite sprite, Color color)
    {
        Image img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = false;
        img.raycastTarget = false;
        return img;
    }

    static TMP_Text Txt(string name, Transform parent, Vector2 pos, Vector2 size, float fontSize, bool isBold, Color color, TextAlignmentOptions align, string text = "")
    {
        RectTransform r = Rect(name, parent, pos, size);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = isBold ? bold : regular;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.text = text;
        t.raycastTarget = false;
        t.richText = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    // Cinta de titulo sin texto incrustado: fondo azul oscuro con doble filete dorado.
    static void Banner(RectTransform r)
    {
        Img(r, null, new Color(0.05f, 0.09f, 0.18f, 1f));
        Border(r, 0f, 3f, GoldLine);
        Border(r, 8f, 1.5f, new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.5f));
    }

    // Marco de cuatro franjas (no tapa el fondo, asi no se mezcla con el al aparecer con fundido).
    static void Border(RectTransform r, float inset, float thick, Color c)
    {
        float w = r.sizeDelta.x, h = r.sizeDelta.y;
        Img(Rect("Marco", r, new Vector2(0f, h * 0.5f - inset - thick * 0.5f), new Vector2(w - inset * 2f, thick)), null, c);
        Img(Rect("Marco", r, new Vector2(0f, -h * 0.5f + inset + thick * 0.5f), new Vector2(w - inset * 2f, thick)), null, c);
        Img(Rect("Marco", r, new Vector2(-w * 0.5f + inset + thick * 0.5f, 0f), new Vector2(thick, h - inset * 2f)), null, c);
        Img(Rect("Marco", r, new Vector2(w * 0.5f - inset - thick * 0.5f, 0f), new Vector2(thick, h - inset * 2f)), null, c);
    }

    static Image Divider(Transform parent, float x, float height)
    {
        RectTransform r = Rect("Divisor", parent, new Vector2(x, 0f), new Vector2(2f, height));
        return Img(r, null, new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.65f));
    }

    public static string Build()
    {
        if (EditorApplication.isPlaying)
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

        GameObject es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));

        GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(VictoryController));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGo.transform;

        // ---- Fondo (el del encuentro se asigna al abrir la escena) + velo oscuro discreto
        RectTransform bgRect = Rect("Fondo", root, Vector2.zero, Vector2.zero);
        Stretch(bgRect);
        Image bg = Img(bgRect, null, new Color(0.05f, 0.07f, 0.11f, 1f));

        RectTransform veilRect = Rect("Velo", root, Vector2.zero, Vector2.zero);
        Stretch(veilRect);
        Img(veilRect, null, new Color(0f, 0.02f, 0.06f, 0.55f));

        // ---- Panel principal
        RectTransform panel = Rect("Panel", root, new Vector2(0f, -20f), new Vector2(1500f, 820f));
        Img(panel, Sp("Assets/Sprites/UICombate+/MarcoInfo.png"), Color.white);

        // ---- Titulo ¡VICTORIA! (banner TituloReino) + espadas cruzadas
        RectTransform titleRect = Rect("Titulo", root, new Vector2(0f, 385f), new Vector2(980f, 126.6f));
        CanvasGroup titleGroup = titleRect.gameObject.AddComponent<CanvasGroup>();
        Img(titleRect, Sp("Assets/Sprites/MenuUi/MarcoTitulo.png"), Color.white).preserveAspect = true;
        TMP_Text titleText = Txt("Texto", titleRect, new Vector2(0f, 2f), new Vector2(640f, 100f), 78f, true, Gold, TextAlignmentOptions.Center, "¡VICTORIA!");
        titleText.outlineWidth = 0.12f;
        titleText.outlineColor = new Color32(20, 14, 6, 255);


        // ---- Progreso del equipo
        RectTransform progressHeader = Rect("CabeceraProgreso", root, new Vector2(0f, 238f), new Vector2(460f, 56f));
        CanvasGroup progressGroup = progressHeader.gameObject.AddComponent<CanvasGroup>();
        Banner(progressHeader);
        Txt("Texto", progressHeader, new Vector2(0f, 1f), new Vector2(440f, 50f), 34f, true, Ivory, TextAlignmentOptions.Center, "Progreso del equipo");

        string[,] heroes = { { "PlayerEvanora", "Evanora", "Evanora" }, { "PlayerArquera", "Elaina", "Elaina" }, { "PlayerNoelle", "Noelle", "Noelle" }, { "PlayerCaballero", "Valerian", "Valerian" } };
        var rows = new List<VictoryController.HeroRow>();

        for (int i = 0; i < 4; i++)
        {
            float y = 150f - i * 80f;
            RectTransform row = Rect("Fila_" + heroes[i, 1], root, new Vector2(0f, y), new Vector2(1280f, 427f));
            CanvasGroup group = row.gameObject.AddComponent<CanvasGroup>();
            Img(row, Sp("Assets/Sprites/MenuUi/MarcoInferior.png"), Color.white);

            RectTransform hi = Rect("Realce", row, Vector2.zero, new Vector2(1240f, 66f));
            Image highlight = Img(hi, null, new Color(Gold.r, Gold.g, Gold.b, 0f));

            // retrato con marco dorado
            RectTransform frame = Rect("MarcoRetrato", row, new Vector2(-592f, 0f), new Vector2(74f, 74f));
            Img(frame, null, GoldLine);
            RectTransform inner = Rect("Fondo", frame, Vector2.zero, new Vector2(68f, 68f));
            Img(inner, null, new Color(0.05f, 0.08f, 0.14f, 1f));
            RectTransform portrait = Rect("Retrato", inner, Vector2.zero, new Vector2(68f, 68f));
            Image portraitImage = Img(portrait, Sp("Assets/Sprites/Personajes/Iconos/" + heroes[i, 2] + ".png"), Color.white);
            portraitImage.preserveAspect = true;

            Txt("Nombre", row, new Vector2(-425f, 0f), new Vector2(230f, 60f), 34f, true, Ivory, TextAlignmentOptions.MidlineLeft, heroes[i, 1]);
            Divider(row, -300f, 44f);
            TMP_Text level = Txt("Nivel", row, new Vector2(-215f, 0f), new Vector2(120f, 60f), 30f, false, Ivory, TextAlignmentOptions.Center, "Nv. 1");
            TMP_Text tag = Txt("EtiquetaNivel", row, new Vector2(480f, -12f), new Vector2(320f, 26f), 18f, true, new Color(0.50f, 0.82f, 0.56f, 1f), TextAlignmentOptions.Center, "");
            tag.gameObject.SetActive(false);
            Divider(row, -140f, 44f);

            // barra de experiencia
            RectTransform track = Rect("BarraFondo", row, new Vector2(95f, 0f), new Vector2(460f, 28f));
            Img(track, null, new Color(0.04f, 0.07f, 0.13f, 1f));
            Outline outline = track.gameObject.AddComponent<Outline>();
            outline.effectColor = GoldLine;
            outline.effectDistance = new Vector2(2f, -2f);
            RectTransform fill = Rect("Relleno", track, Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            Img(fill, null, new Color(0.16f, 0.52f, 0.90f, 1f));
            RectTransform sheen = Rect("Brillo", fill, Vector2.zero, Vector2.zero);
            sheen.anchorMin = new Vector2(0f, 0.5f);
            sheen.anchorMax = new Vector2(1f, 1f);
            sheen.offsetMin = sheen.offsetMax = Vector2.zero;
            Img(sheen, null, new Color(1f, 1f, 1f, 0.12f));
            TMP_Text xp = Txt("XP", track, Vector2.zero, new Vector2(460f, 28f), 22f, true, Color.white, TextAlignmentOptions.Center, "0 / 100 XP");

            Divider(row, 345f, 44f);
            TMP_Text gain = Txt("Ganada", row, new Vector2(480f, 0f), new Vector2(220f, 60f), 34f, true, Ivory, TextAlignmentOptions.Center, "+0 XP");

            rows.Add(new VictoryController.HeroRow { heroId = heroes[i, 0], group = group, levelText = level, xpText = xp, gainText = gain, barFill = fill, levelUpTag = tag, highlight = highlight });
        }

        // ---- Recompensas
        RectTransform rewardsHeader = Rect("CabeceraRecompensas", root, new Vector2(0f, -165f), new Vector2(360f, 56f));
        CanvasGroup rewardsHeaderGroup = rewardsHeader.gameObject.AddComponent<CanvasGroup>();
        Banner(rewardsHeader);
        Txt("Texto", rewardsHeader, new Vector2(0f, 1f), new Vector2(330f, 50f), 34f, true, Ivory, TextAlignmentOptions.Center, "Recompensas");

        RectTransform box = Rect("CajaRecompensas", root, new Vector2(0f, -265f), new Vector2(1240f, 120f));
        CanvasGroup boxGroup = box.gameObject.AddComponent<CanvasGroup>();
        Img(box, null, new Color(0.05f, 0.09f, 0.18f, 1f));
        Border(box, 0f, 3f, GoldLine);

        RectTransform coinIcon = Rect("IconoCoronas", box, new Vector2(-440f, 0f), new Vector2(92f, 92f));
        Img(coinIcon, Sp("Assets/Sprites/Iconos/coronas.png"), Color.white).preserveAspect = true;
        TMP_Text coins = Txt("Coronas", box, new Vector2(-165f, 0f), new Vector2(330f, 70f), 42f, true, Ivory, TextAlignmentOptions.MidlineLeft, "+0 Coronas");
        Divider(box, -20f, 90f);

        RectTransform relicGroup = Rect("Reliquia", box, new Vector2(290f, 0f), new Vector2(560f, 110f));
        RectTransform glowRect = Rect("Resplandor", relicGroup, new Vector2(-205f, 0f), new Vector2(230f, 230f));
        Image glow = Img(glowRect, Sp("Assets/Sprites/DialogosUI/BrilloRadial.png"), new Color(1f, 1f, 1f, 0f));
        RectTransform relicFrame = Rect("MarcoReliquia", relicGroup, new Vector2(-205f, 0f), new Vector2(100f, 100f));
        Img(relicFrame, null, GoldLine);
        RectTransform relicBack = Rect("Fondo", relicFrame, Vector2.zero, new Vector2(92f, 92f));
        Img(relicBack, null, new Color(0.05f, 0.08f, 0.14f, 1f));
        RectTransform relicIconRect = Rect("Icono", relicBack, Vector2.zero, new Vector2(84f, 84f));
        Image relicIcon = Img(relicIconRect, null, Color.white);
        relicIcon.preserveAspect = true;
        RectTransform chip = Rect("Rareza", relicGroup, new Vector2(-60f, 30f), new Vector2(160f, 32f));
        Image chipImage = Img(chip, null, new Color(0.25f, 0.24f, 0.22f, 0.95f));
        Outline chipOutline = chip.gameObject.AddComponent<Outline>();
        chipOutline.effectColor = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.6f);
        chipOutline.effectDistance = new Vector2(1f, -1f);
        TMP_Text rarityText = Txt("Texto", chip, Vector2.zero, new Vector2(160f, 32f), 20f, true, Ivory, TextAlignmentOptions.Center, "Común");
        TMP_Text relicName = Txt("Nombre", relicGroup, new Vector2(90f, -20f), new Vector2(460f, 50f), 34f, true, Ivory, TextAlignmentOptions.MidlineLeft, "Reliquia");
        relicName.enableAutoSizing = true;
        relicName.fontSizeMin = 22f;
        relicName.fontSizeMax = 34f;
        TMP_Text noRelic = Txt("SinReliquia", box, new Vector2(290f, 0f), new Vector2(520f, 60f), 32f, false, new Color(0.62f, 0.62f, 0.66f, 0.85f), TextAlignmentOptions.Center, "Sin reliquia");
        noRelic.fontStyle = FontStyles.Italic;

        // ---- Boton Continuar
        RectTransform button = Rect("BotonContinuar", root, new Vector2(0f, -440f), new Vector2(490f, 163f));
        CanvasGroup continueGroup = button.gameObject.AddComponent<CanvasGroup>();
        Image buttonImage = Img(button, Sp("Assets/Sprites/Buttons/Misiones.png"), Color.white);
        buttonImage.preserveAspect = true;
        buttonImage.raycastTarget = true;
        buttonImage.raycastPadding = new Vector4(11f, 34f, 11f, 31f);
        Button continueButton = button.gameObject.AddComponent<Button>();
        continueButton.targetGraphic = buttonImage;
        continueButton.transition = Selectable.Transition.None;
        button.gameObject.AddComponent<HoverScale>().HoverFactor = 1.03f;
        Txt("Texto", button, new Vector2(0f, 1f), new Vector2(420f, 80f), 44f, true, Ivory, TextAlignmentOptions.Center, "Continuar");

        // ---- Fundido a negro (entrada y salida)
        RectTransform fadeRect = Rect("FundidoNegro", root, Vector2.zero, Vector2.zero);
        Stretch(fadeRect);
        Image fade = Img(fadeRect, null, Color.black);
        fade.raycastTarget = true;

        // ---- Controlador
        VictoryController controller = canvasGo.GetComponent<VictoryController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("titleGroup").objectReferenceValue = titleGroup;
        so.FindProperty("titleRect").objectReferenceValue = titleRect;
        so.FindProperty("progressHeader").objectReferenceValue = progressGroup;
        so.FindProperty("rewardsHeader").objectReferenceValue = rewardsHeaderGroup;
        so.FindProperty("rewardsBox").objectReferenceValue = boxGroup;
        so.FindProperty("fade").objectReferenceValue = fade;
        SerializedProperty rowList = so.FindProperty("rows");
        rowList.arraySize = rows.Count;

        for (int i = 0; i < rows.Count; i++)
        {
            SerializedProperty e = rowList.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("heroId").stringValue = rows[i].heroId;
            e.FindPropertyRelative("group").objectReferenceValue = rows[i].group;
            e.FindPropertyRelative("levelText").objectReferenceValue = rows[i].levelText;
            e.FindPropertyRelative("xpText").objectReferenceValue = rows[i].xpText;
            e.FindPropertyRelative("gainText").objectReferenceValue = rows[i].gainText;
            e.FindPropertyRelative("barFill").objectReferenceValue = rows[i].barFill;
            e.FindPropertyRelative("levelUpTag").objectReferenceValue = rows[i].levelUpTag;
            e.FindPropertyRelative("highlight").objectReferenceValue = rows[i].highlight;
        }

        so.FindProperty("coinsText").objectReferenceValue = coins;
        so.FindProperty("relicGroup").objectReferenceValue = relicGroup;
        so.FindProperty("relicIcon").objectReferenceValue = relicIcon;
        so.FindProperty("relicName").objectReferenceValue = relicName;
        so.FindProperty("relicChip").objectReferenceValue = chipImage;
        so.FindProperty("relicRarity").objectReferenceValue = rarityText;
        so.FindProperty("relicGlow").objectReferenceValue = glow;
        so.FindProperty("noRelicText").objectReferenceValue = noRelic;
        so.FindProperty("continueGroup").objectReferenceValue = continueGroup;
        so.FindProperty("continueButton").objectReferenceValue = continueButton;
        so.FindProperty("nextScene").stringValue = "Map";
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);

        // Build Settings: Victory despues de las escenas de dialogo (si ya esta, no se duplica).
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        return "Escena Victory construida y agregada a Build Settings (" + rows.Count + " filas).";
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
