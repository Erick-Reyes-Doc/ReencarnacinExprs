using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Crea el boton "OMITIR" con el mismo estilo que el de la introduccion (menuNormal / menuHover, texto marfil de
// Georgia en negrita con espaciado 2). Se usa en los dialogos y en los tutoriales del combate y del mapa.
public static class SkipButtonFactory
{
    // Medidas del boton de la intro (348 x 117 con texto de 19,5 pt); el sprite menuNormal tiene margen transparente.
    public const float DefaultWidth = 300f;
    public const float DefaultHeight = 100f;

    public static Button Create(Transform parent, string label, Sprite normal, Sprite hover, TMP_FontAsset font,
        UnityAction onClick, Vector2 anchor, Vector2 pivot, Vector2 position, float width = DefaultWidth, float height = DefaultHeight)
    {
        float s = height / 156f;
        GameObject go = new GameObject("BotonOmitir", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(width, height);

        Image image = go.GetComponent<Image>();
        image.sprite = normal;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 4.49f / s;
        image.raycastTarget = true;

        GameObject textGo = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();

        if (font != null)
            text.font = font;

        text.text = label;
        text.fontSize = 26f * s;
        text.color = new Color32(0xE8, 0xDC, 0xC6, 0xFF);
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = 2f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 26f * s;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        RectTransform tr = text.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(34f * s, 0f);
        tr.offsetMax = new Vector2(-34f * s, 0f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState { highlightedSprite = hover, pressedSprite = hover, selectedSprite = hover, disabledSprite = normal };
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        if (onClick != null)
            button.onClick.AddListener(onClick);

        return button;
    }
}
