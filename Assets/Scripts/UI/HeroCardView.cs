using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tarjeta de personaje de la barra inferior del mapa: brillo dorado suave al pasar el cursor, resplandor discreto
// cuando es la tarjeta seleccionada y un tooltip pequeno con nombre, nivel y vida. Solo presenta: los datos los
// entrega WorldMapController.RefreshHud (misma fuente que la ficha y el combate).
public class HeroCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float HoverAlpha = 0.45f;
    private const float SelectedAlpha = 0.85f;
    private const float FadeSpeed = 12f;

    [SerializeField] private Image glow;
    [SerializeField] private TMP_FontAsset font;
    private Outline halo;

    private bool hovered;
    private bool selected;
    private float alpha;
    private string title = "";
    private int level = 1;
    private int health;
    private int maxHealth = 1;

    private static RectTransform tooltip;
    private static TMP_Text tooltipText;
    private static HeroCardView tooltipOwner;

    public void Configure(Image glowImage, TMP_FontAsset tooltipFont)
    {
        glow = glowImage;
        halo = glowImage != null ? glowImage.GetComponent<Outline>() : null;
        font = tooltipFont;
    }

    public void SetData(string heroName, int heroLevel, int hp, int max)
    {
        title = heroName;
        level = heroLevel;
        health = hp;
        maxHealth = max;

        if (tooltipOwner == this)
            UpdateTooltipText();
    }

    public void SetSelected(bool value)
    {
        selected = value;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        HideTooltip();
    }

    private void OnDisable()
    {
        hovered = false;
        HideTooltip();
    }

    private void Update()
    {
        if (hovered && tooltipOwner == this && tooltip != null)
            PlaceTooltip();

        if (glow == null)
            return;

        if (halo == null)
            halo = glow.GetComponent<Outline>();

        float target = selected ? SelectedAlpha : hovered ? HoverAlpha : 0f;
        alpha = Mathf.MoveTowards(alpha, target, FadeSpeed * Time.unscaledDeltaTime);
        Color c = glow.color;

        if (!Mathf.Approximately(c.a, alpha))
        {
            c.a = alpha;
            glow.color = c;
        }

        if (halo != null)
        {
            Color h = halo.effectColor;
            h.a = alpha * 0.55f;
            halo.effectColor = h;
        }
    }

    // ---------------- Tooltip compartido ----------------

    private void ShowTooltip()
    {
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            return;

        canvas = canvas.rootCanvas;

        if (tooltip == null)
            BuildTooltip(canvas);
        else if (tooltip.parent != canvas.transform)
            tooltip.SetParent(canvas.transform, false);

        tooltipOwner = this;
        UpdateTooltipText();
        tooltip.gameObject.SetActive(true);
        tooltip.SetAsLastSibling();
        PlaceTooltip();
    }

    private void HideTooltip()
    {
        if (tooltipOwner != this)
            return;

        tooltipOwner = null;

        if (tooltip != null)
            tooltip.gameObject.SetActive(false);
    }

    private void UpdateTooltipText()
    {
        if (tooltipText == null)
            return;

        tooltipText.text = "<color=#D4AF6A>" + title + "</color>\nNivel " + level + "\nVida " + health + " / " + maxHealth;
    }

    private void PlaceTooltip()
    {
        RectTransform card = (RectTransform)transform;
        Canvas canvas = tooltip.GetComponentInParent<Canvas>();
        Vector3[] corners = new Vector3[4];
        card.GetWorldCorners(corners);
        Vector3 top = (corners[1] + corners[2]) * 0.5f;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        tooltip.position = top + Vector3.up * 10f * scale;
    }

    private void BuildTooltip(Canvas canvas)
    {
        GameObject go = new GameObject("TooltipPersonaje", typeof(RectTransform), typeof(Image));
        go.layer = canvas.gameObject.layer;
        tooltip = (RectTransform)go.transform;
        tooltip.SetParent(canvas.transform, false);
        tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 0.5f);
        tooltip.pivot = new Vector2(0.5f, 0f);
        tooltip.sizeDelta = new Vector2(230f, 104f);

        Image border = go.GetComponent<Image>();
        border.color = new Color32(0xB7, 0x95, 0x5B, 0xFF);
        border.raycastTarget = false;

        GameObject inner = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        inner.layer = go.layer;
        RectTransform innerRect = (RectTransform)inner.transform;
        innerRect.SetParent(tooltip, false);
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2f, 2f);
        innerRect.offsetMax = new Vector2(-2f, -2f);
        Image fill = inner.GetComponent<Image>();
        fill.color = new Color32(0x10, 0x15, 0x1E, 0xF5);
        fill.raycastTarget = false;

        GameObject label = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.layer = go.layer;
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.SetParent(innerRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 6f);
        labelRect.offsetMax = new Vector2(-14f, -6f);
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();

        if (font != null)
            text.font = font;

        text.fontSize = 24f;
        text.color = new Color32(0xE8, 0xDC, 0xC6, 0xFF);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        tooltipText = text;
        go.SetActive(false);
    }
}
