using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pestanas TODO / RELIQUIAS / CONSUMIBLES de la ventana de tienda. Todos los productos (consumibles y reliquias del
// catalogo marcadas como disponibles en la tienda) usan la misma tarjeta compacta en una cuadricula de cuatro columnas
// con scroll, y comparten el panel de detalle y el boton COMPRAR. Los consumibles se compran con ConsumableService (sin
// cambios) y las reliquias con RelicShop. La interfaz extra se construye al
// empezar a partir de los sprites y fuentes de la propia ventana; la compra y sus reglas viven en RelicShop.
public partial class MapShopController
{
    private const float TabsBand = 56f;          // alto que gana la ventana para las pestanas
    private const float RelicCardW = 172f, RelicCardH = 164f, RelicCardGap = 12f;
    private const int RelicColumns = 4;
    private const float BuyCooldownSeconds = 0.35f;

    private static readonly Color RelicNavy = new Color(0.090f, 0.122f, 0.169f, 1f);
    private static readonly Color RelicBorderIdle = new Color(0.239f, 0.290f, 0.361f, 1f);
    private static readonly Color RelicBorderHover = new Color(0.62f, 0.52f, 0.34f, 1f);
    private static readonly Color RelicBorderSelected = new Color(0.831f, 0.686f, 0.416f, 1f);
    private static readonly Color RelicBad = new Color(0.90f, 0.42f, 0.37f, 1f);

    private sealed class RelicCard
    {
        public RectTransform rect;
        public Image border;
        public GameObject hoverFrame;
        public ShopCardHover hover;
        public Image icon;
        public TMP_Text name;
        public TMP_Text price;
        public GameObject priceGroup;
        public GameObject ownedLabel;
        public Image[] gems;
    }

    private bool relicUiBuilt;
    private bool relicDirty = true;
    private int shopTab;                 // 0 todo, 1 reliquias, 2 consumibles
    private int appliedTab = -1;
    private int selectedRelic;
    private float buyLockedUntil;
    private string relicMessage = "";
    private float relicMessageUntil;
    // Producto de la tienda: una reliquia del catalogo o un consumible (relic == null).
    private struct ShopEntry
    {
        public RelicData relic;
        public int consumable;
        public bool IsRelic => relic != null;
    }

    private List<ShopEntry> relicProducts = new List<ShopEntry>();
    private readonly List<RelicCard> relicCards = new List<RelicCard>();
    private GameObject relicRoot;
    private RectTransform relicContent;
    private ScrollRect relicScroll;
    private readonly Image[] tabBorders = new Image[3];
    private readonly Image[] tabFills = new Image[3];
    private readonly ShopCardHover[] tabHovers = new ShopCardHover[3];

    // Sprites y fuentes tomados de la propia ventana de tienda.
    private Sprite relicCrown;
    private TMP_FontAsset relicBold, relicRegular;
    private float detailFontSize = 20f, detailLineSpacing = 12f;
    private CharacterPanelController relicNames;

    public int ShopTab => shopTab;

    public void SetShopTab(int tab)
    {
        tab = Mathf.Clamp(tab, 0, 2);

        if (tab == shopTab)
            return;

        shopTab = tab;
        message = "";
        relicMessage = "";
        relicDirty = true;

        if (relicScroll != null)
            relicScroll.verticalNormalizedPosition = 1f;
    }

    // ------------------------------------------------------------
    // Construccion
    // ------------------------------------------------------------

    private static RectTransform RRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform RCentered(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        Vector2 c = new Vector2(0.5f, 0.5f);
        return RRect(name, parent, c, c, c, pos, size);
    }

    private static Image RImage(RectTransform rect, Sprite sprite, Color color, float multiplier, bool raycast, bool preserve = false)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;

        if (multiplier > 0f && sprite != null)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
        }

        image.preserveAspect = preserve;
        return image;
    }

    // Recuadro con borde fino hecho de cuatro franjas "Borde_*" (igual que las tarjetas de la tienda); SetBorderColor
    // cambia el color de las cuatro. 'fill' es el color de fondo del recuadro.
    private static Image RBorder(string name, Transform parent, Vector2 pos, Vector2 size, Color fill, Color border)
    {
        RectTransform box = RCentered(name, parent, pos, size);
        Image image = RImage(box, null, fill, 0f, false);
        RImage(RRect("Borde_Sup", box, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 1.5f)), null, border, 0f, false);
        RImage(RRect("Borde_Inf", box, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 1.5f)), null, border, 0f, false);
        RImage(RRect("Borde_Izq", box, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(1.5f, -3f)), null, border, 0f, false);
        RImage(RRect("Borde_Der", box, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(1.5f, -3f)), null, border, 0f, false);
        return image;
    }

    private TMP_Text RText(Transform parent, string name, Vector2 pos, Vector2 size, string text, float fontSize, Color color,
        TextAlignmentOptions alignment, TMP_FontAsset fontAsset)
    {
        RectTransform rect = RCentered(name, parent, pos, size);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = fontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private void BuildRelicShopUI()
    {
        if (relicUiBuilt)
            return;

        if (shopWindow == null || productRows == null || productRows.Length == 0 || productRows[0] == null)
        {
            Debug.LogWarning("Tienda de reliquias: la ventana de tienda no esta construida (sin VentanaTienda o tarjetas).");
            return;
        }

        Transform nameFrame = productRows[0].transform.Find("MarcoNombre");
        Transform crown = productRows[0].transform.Find("Precio/Corona");

        if (nameFrame == null || crown == null)
        {
            Debug.LogWarning("Tienda de reliquias: estructura de la ventana distinta a la esperada (nameFrame=" + (nameFrame != null) + ", corona=" + (crown != null) + ").");
            return;
        }

        relicUiBuilt = true;

        // Mismo oscurecimiento de fondo que el inventario y las fichas de personaje (0,55).
        if (veil != null)
        {
            Image veilImage = veil.GetComponent<Image>();

            if (veilImage != null)
            {
                Color veilColor = veilImage.color;
                veilColor.a = 0.55f;
                veilImage.color = veilColor;
            }
        }

        RectTransform window = (RectTransform)shopWindow.transform;
        relicCrown = crown.GetComponent<Image>().sprite;
        Transform nameText = nameFrame.Find("Nombre");
        relicBold = nameText != null ? nameText.GetComponent<TMP_Text>().font : (shopCoinsText != null ? shopCoinsText.font : null);
        relicRegular = detailDescription != null ? detailDescription.font : relicBold;

        if (detailDescription != null)
        {
            detailFontSize = detailDescription.fontSize;
            detailLineSpacing = detailDescription.lineSpacing;
        }

        // Titulo plano como el del inventario: se quita el estandarte y se centra icono + texto.
        Transform ribbonZone = window.Find("ZonaTitulo");

        if (ribbonZone != null)
            ribbonZone.gameObject.SetActive(false);

        Sprite shopIcon = null;

        foreach (Image candidate in FindObjectsByType<Image>(FindObjectsInactive.Include))
        {
            if (candidate.name == "Icono" && candidate.transform.parent != null && candidate.transform.parent.name == "BotonTienda")
            {
                shopIcon = candidate.sprite;
                break;
            }
        }

        {
            Vector2 top = new Vector2(0.5f, 1f);
            RectTransform titleRow = RRect("TituloTienda", window, top, top, top, new Vector2(0f, -36f), new Vector2(520f, 72f));
            HorizontalLayoutGroup row = titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            if (shopIcon != null)
            {
                RectTransform iconRect = RCentered("Icono", titleRow, Vector2.zero, new Vector2(72f, 72f));
                RImage(iconRect, shopIcon, Color.white, 0f, false, true);
                LayoutElement iconSize = iconRect.gameObject.AddComponent<LayoutElement>();
                iconSize.preferredWidth = 72f;
                iconSize.preferredHeight = 72f;
            }

            TMP_Text title = RText(titleRow, "Texto", Vector2.zero, new Vector2(300f, 72f), "TIENDA", 50f, new Color(0.910f, 0.863f, 0.776f, 1f), TextAlignmentOptions.Midline, relicBold);
            LayoutElement titleSize = title.gameObject.AddComponent<LayoutElement>();
            titleSize.preferredHeight = 72f;
        }

        // Contador de Coronas discreto: se retira el marco ornamental y queda un recuadro fino al nivel del titulo.
        Transform coinsZone = window.Find("ZonaCoronas");

        if (coinsZone != null)
        {
            Transform oldFill = coinsZone.Find("FondoCoronas");
            Transform chip = coinsZone.Find("Coronas");

            if (oldFill != null)
                oldFill.gameObject.SetActive(false);

            if (chip != null)
            {
                Image chipImage = chip.GetComponent<Image>();

                if (chipImage != null)
                    chipImage.enabled = false;

                RectTransform chipRect = (RectTransform)chip;
                chipRect.sizeDelta = new Vector2(150f, 42f);
                chipRect.anchoredPosition = Vector2.zero;
                Image box = RBorder("MarcoDiscreto", chip, Vector2.zero, new Vector2(150f, 42f), new Color(0.075f, 0.098f, 0.141f, 1f), RelicBorderIdle);
                box.transform.SetAsFirstSibling();

                Transform coinIcon = chip.Find("Icono");
                Transform amount = chip.Find("Cantidad");

                if (coinIcon != null)
                {
                    ((RectTransform)coinIcon).sizeDelta = new Vector2(30f, 30f);
                    ((RectTransform)coinIcon).anchoredPosition = new Vector2(-40f, 0f);
                }

                if (amount != null)
                {
                    ((RectTransform)amount).sizeDelta = new Vector2(80f, 40f);
                    ((RectTransform)amount).anchoredPosition = new Vector2(14f, 0f);
                    TMP_Text amountText = amount.GetComponent<TMP_Text>();

                    if (amountText != null)
                        amountText.fontSize = 28f;
                }
            }

            // Centro vertical al nivel del titulo (la fila del titulo mide 72 px y empieza 36 px bajo el borde superior).
            RectTransform zoneRect = (RectTransform)coinsZone;
            zoneRect.anchoredPosition = new Vector2(-96f, -(36f + 36f - zoneRect.sizeDelta.y * 0.5f));
        }

        // La ventana crece TabsBand: la cabecera sube con el borde superior y el resto del contenido baja la mitad,
        // asi cabe una fila de pestanas entre el titulo y las tarjetas sin que se acerque al borde inferior.
        window.sizeDelta += new Vector2(0f, TabsBand);

        foreach (Transform child in window)
        {
            if (child.name == "ZonaTitulo" || child.name == "ZonaCoronas" || child.name == "ZonaCerrar" || child.name == "TituloTienda")
                continue;

            ((RectTransform)child).anchoredPosition -= new Vector2(0f, TabsBand * 0.5f);
        }

        detailIconBasePos.y -= TabsBand * 0.5f;

        // Pestanas.
        string[] tabNames = { "Todo", "Reliquias", "Consumibles" };

        for (int i = 0; i < 3; i++)
        {
            RectTransform tab = RCentered("Pestana_" + tabNames[i], window, new Vector2((i - 1) * 206f, 290f), new Vector2(190f, 46f));
            Image hit = RImage(tab, null, new Color(0f, 0f, 0f, 0f), 0f, true);
            Button button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            Navigation none = button.navigation;
            none.mode = Navigation.Mode.None;
            button.navigation = none;
            int index = i;
            button.onClick.AddListener(() => SetShopTab(index));
            tabHovers[i] = tab.gameObject.AddComponent<ShopCardHover>();

            tabFills[i] = RBorder("Marco", tab, Vector2.zero, new Vector2(190f, 46f), RelicNavy, RelicBorderIdle);
            tabBorders[i] = tabFills[i];
            TMP_Text label = RText(tab, "Texto", Vector2.zero, new Vector2(176f, 40f), tabNames[i], 25f, new Color(0.910f, 0.863f, 0.776f, 1f),
                TextAlignmentOptions.Midline, relicBold);
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // Zona de reliquias (mismo hueco que las tarjetas de consumibles) con scroll vertical.
        relicRoot = RCentered("TiendaReliquias", window, new Vector2(0f, 75f), new Vector2(780f, 346f)).gameObject;
        RectTransform rootRect = (RectTransform)relicRoot.transform;
        RectTransform viewport = RCentered("Viewport", rootRect, Vector2.zero, new Vector2(744f, 346f));
        RImage(viewport, null, new Color(0f, 0f, 0f, 0f), 0f, true);
        viewport.gameObject.AddComponent<RectMask2D>();

        relicContent = RRect("Contenido", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 346f));

        RectTransform barRect = RCentered("Barra", rootRect, new Vector2(384f, 0f), new Vector2(8f, 346f));
        RImage(barRect, null, new Color(RelicNavy.r, RelicNavy.g, RelicNavy.b, 0.9f), 0f, true);
        RectTransform handle = RRect("Asa", barRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image handleImage = RImage(handle, null, new Color(0.831f, 0.686f, 0.416f, 0.8f), 0f, true);
        Scrollbar scrollbar = barRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;

        relicScroll = viewport.gameObject.AddComponent<ScrollRect>();
        relicScroll.viewport = viewport;
        relicScroll.content = relicContent;
        relicScroll.horizontal = false;
        relicScroll.vertical = true;
        relicScroll.movementType = ScrollRect.MovementType.Clamped;
        relicScroll.scrollSensitivity = 40f;
        relicScroll.verticalScrollbar = scrollbar;
        relicScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        BuildRelicTip(window);
        relicRoot.SetActive(false);
    }

    // Tarjeta compacta: solo icono y precio. Nombre, rareza y efectos salen en un tooltip al pasar el cursor y en el
    // panel inferior al seleccionarla.
    private RelicCard MakeRelicCard(int index)
    {
        RelicCard card = new RelicCard();
        Vector2 top = new Vector2(0.5f, 1f);
        float x = (index % RelicColumns - (RelicColumns - 1) * 0.5f) * (RelicCardW + RelicCardGap);
        float y = -(index / RelicColumns) * (RelicCardH + RelicCardGap) - RelicCardH * 0.5f - 4f;
        card.rect = RRect("Tarjeta_Reliquia_" + index, relicContent, top, top, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(RelicCardW, RelicCardH));
        Image fill = RImage(card.rect, null, RelicNavy, 0f, true);
        Button button = card.rect.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        button.transition = Selectable.Transition.None;
        Navigation none = button.navigation;
        none.mode = Navigation.Mode.None;
        button.navigation = none;
        int captured = index;
        button.onClick.AddListener(() => selectedRelic = captured);
        card.hover = card.rect.gameObject.AddComponent<ShopCardHover>();

        card.border = RBorder("Marco", card.rect, Vector2.zero, new Vector2(RelicCardW, RelicCardH), new Color(0.075f, 0.098f, 0.141f, 1f), RelicBorderIdle);
        card.icon = RImage(RCentered("Icono", card.rect, new Vector2(0f, 22f), new Vector2(98f, 98f)), null, Color.white, 0f, false, true);

        RectTransform priceGroup = RCentered("Precio", card.rect, new Vector2(0f, -52f), new Vector2(150f, 40f));
        RImage(RCentered("Corona", priceGroup, new Vector2(-34f, 0f), new Vector2(32f, 32f)), relicCrown, Color.white, 0f, false, true);
        card.price = RText(priceGroup, "Cantidad", new Vector2(18f, 0f), new Vector2(80f, 40f), "", 30f, new Color(0.831f, 0.686f, 0.416f, 1f), TextAlignmentOptions.Midline, relicBold);
        card.priceGroup = priceGroup.gameObject;

        TMP_Text owned = RText(card.rect, "Poseida", new Vector2(0f, -52f), new Vector2(150f, 40f), "YA POSEÍDA", 20f, new Color(0.831f, 0.686f, 0.416f, 1f), TextAlignmentOptions.Midline, relicBold);
        card.ownedLabel = owned.gameObject;
        card.ownedLabel.SetActive(false);
        return card;
    }

    // Tooltip de la tienda (hijo de la ventana: se oculta con ella).
    private RectTransform relicTip;
    private TMP_Text relicTipTitle, relicTipBody;

    private void BuildRelicTip(RectTransform window)
    {
        Vector2 topLeft = new Vector2(0f, 1f);
        relicTip = RRect("TooltipReliquia", window, topLeft, topLeft, topLeft, Vector2.zero, new Vector2(330f, 120f));
        RImage(relicTip, null, RelicBorderSelected, 0f, false);
        RectTransform inner = RRect("Fondo", relicTip, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        inner.offsetMin = new Vector2(1.5f, 1.5f);
        inner.offsetMax = new Vector2(-1.5f, -1.5f);
        RImage(inner, null, new Color(0.063f, 0.082f, 0.118f, 0.98f), 0f, false);
        relicTipTitle = RText(relicTip, "Titulo", Vector2.zero, new Vector2(300f, 30f), "", 25f, Color.white, TextAlignmentOptions.TopLeft, relicBold);
        relicTipBody = RText(relicTip, "Texto", Vector2.zero, new Vector2(300f, 60f), "", 21f, Color.white, TextAlignmentOptions.TopLeft, relicRegular);
        relicTipTitle.textWrappingMode = TextWrappingModes.Normal;
        relicTipBody.textWrappingMode = TextWrappingModes.Normal;
        Vector2 tl = new Vector2(0f, 1f);

        foreach (TMP_Text t in new[] { relicTipTitle, relicTipBody })
        {
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = tl;
            t.rectTransform.pivot = tl;
        }

        relicTip.gameObject.SetActive(false);
    }

    private void UpdateRelicTip(ShopEntry? hoveredEntry)
    {
        if (relicTip == null)
            return;

        if (hoveredEntry == null || UnityEngine.InputSystem.Mouse.current == null)
        {
            if (relicTip.gameObject.activeSelf)
                relicTip.gameObject.SetActive(false);

            return;
        }

        ShopEntry entry = hoveredEntry.Value;
        string title, body;

        if (entry.IsRelic)
        {
            RelicData data = entry.relic;
            string rarityColor = ColorUtility.ToHtmlStringRGB(RelicData.RarityColor(data.rarity));
            title = "<color=#" + rarityColor + ">" + data.displayName + "</color>";
            body = "<color=#" + rarityColor + ">" + RelicData.RarityName(data.rarity) + "</color>";

            if (data.IsExclusive)
                body += "  ·  Exclusiva de " + HeroName(data.exclusiveHeroId);

            if (data.uniqueOwnership)
                body += "  ·  Única";

            // Que hace la reliquia (efecto especial), igual que en la ficha del personaje.
            if (!string.IsNullOrEmpty(data.description))
                body += "\n" + data.description;

            foreach (RelicModifier m in data.bonuses)
                body += "\n<color=#7FD08F>+" + m.amount + RelicData.Suffix(m.stat) + " " + RelicData.StatName(m.stat) + "</color>";

            foreach (RelicModifier m in data.penalties)
                body += "\n<color=#E56B5E>−" + m.amount + RelicData.Suffix(m.stat) + " " + RelicData.StatName(m.stat) + "</color>";
        }
        else
        {
            GameSession.Consumable type = (GameSession.Consumable)entry.consumable;
            title = ConsumableService.Name(type);
            body = ConsumableService.Description(type);
        }

        // Las reliquias llevan descripcion: tarjeta algo mas ancha para que se lea sin ocupar demasiado alto.
        float width = entry.IsRelic ? 420f : 330f;
        const float pad = 14f;
        relicTipTitle.text = title;
        relicTipBody.text = body;
        float titleH = relicTipTitle.GetPreferredValues(title, width - 2f * pad, 0f).y;
        float bodyH = relicTipBody.GetPreferredValues(body, width - 2f * pad, 0f).y;
        relicTipTitle.rectTransform.sizeDelta = new Vector2(width - 2f * pad, titleH);
        relicTipTitle.rectTransform.anchoredPosition = new Vector2(pad, -10f);
        relicTipBody.rectTransform.sizeDelta = new Vector2(width - 2f * pad, bodyH);
        relicTipBody.rectTransform.anchoredPosition = new Vector2(pad, -10f - titleH - 4f);
        relicTip.sizeDelta = new Vector2(width, titleH + bodyH + 28f);

        RectTransform window = (RectTransform)relicTip.parent;
        Canvas canvas = window.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(window, UnityEngine.InputSystem.Mouse.current.position.ReadValue(), cam, out Vector2 local))
        {
            // El ancla de la tooltip es la esquina superior izquierda; el origen local de la ventana es su centro.
            Vector2 pos = local + new Vector2(24f, -24f);
            Rect bounds = window.rect;

            if (pos.x + width > bounds.xMax + 40f)
                pos.x = local.x - 24f - width;

            if (pos.y - relicTip.sizeDelta.y < bounds.yMin - 20f)
                pos.y = local.y + 24f + relicTip.sizeDelta.y;

            relicTip.anchorMin = relicTip.anchorMax = new Vector2(0.5f, 0.5f);
            relicTip.anchoredPosition = pos;
        }

        if (!relicTip.gameObject.activeSelf)
            relicTip.gameObject.SetActive(true);

        relicTip.SetAsLastSibling();
    }

    // Lista de productos segun la pestana y datos fijos de las tarjetas (se rehace al abrir la tienda o cambiar de pestana).
    private void RebuildRelicProducts()
    {
        relicDirty = false;
        relicProducts = new List<ShopEntry>();

        if (shopTab != 1)
            for (int c = 0; c < 2; c++)
                relicProducts.Add(new ShopEntry { consumable = c });

        if (shopTab != 2)
            foreach (RelicData relic in RelicShop.Products())
                relicProducts.Add(new ShopEntry { relic = relic, consumable = -1 });

        while (relicCards.Count < relicProducts.Count)
            relicCards.Add(MakeRelicCard(relicCards.Count));

        for (int i = 0; i < relicCards.Count; i++)
        {
            bool has = i < relicProducts.Count;
            relicCards[i].rect.gameObject.SetActive(has);

            if (!has)
                continue;

            ShopEntry entry = relicProducts[i];
            RelicCard card = relicCards[i];
            Sprite sprite = entry.IsRelic ? entry.relic.icon : (entry.consumable < productIcons.Length ? productIcons[entry.consumable] : null);
            card.icon.sprite = sprite;
            card.icon.enabled = sprite != null;
            card.icon.rectTransform.localScale = Vector3.one * (entry.IsRelic ? 1f : 1.4f);   // los iconos de consumibles tienen mas margen transparente
            card.price.text = EntryPrice(entry).ToString();
        }

        int rows = Mathf.CeilToInt(relicProducts.Count / (float)RelicColumns);
        float contentH = rows * RelicCardH + Mathf.Max(0, rows - 1) * RelicCardGap + 8f;
        relicContent.sizeDelta = new Vector2(0f, Mathf.Max(346f, contentH));
        const float lift = 0f;   // siempre arriba: los productos inician en la misma posicion en todas las pestanas

        for (int i = 0; i < relicProducts.Count && i < relicCards.Count; i++)
            relicCards[i].rect.anchoredPosition = new Vector2(relicCards[i].rect.anchoredPosition.x, -(i / RelicColumns) * (RelicCardH + RelicCardGap) - RelicCardH * 0.5f - 4f - lift);

        selectedRelic = relicProducts.Count > 0 ? Mathf.Clamp(selectedRelic, 0, relicProducts.Count - 1) : -1;
    }

    private static int EntryPrice(ShopEntry entry)
    {
        return entry.IsRelic ? RelicShop.Price(entry.relic) : ConsumableService.Price((GameSession.Consumable)entry.consumable);
    }

    // ------------------------------------------------------------
    // Presentacion
    // ------------------------------------------------------------

    // Pestanas y visibilidad de cada vista; se llama cada fotograma con la tienda abierta.
    private void ApplyShopTab()
    {
        for (int i = 0; i < 3; i++)
        {
            bool selected = i == shopTab;
            bool hovered = tabHovers[i] != null && tabHovers[i].Hovered;

            Color border = selected ? RelicBorderSelected : hovered ? RelicBorderHover : RelicBorderIdle;

            SetBorderColor(tabBorders[i], border);

            Color fill = selected ? new Color(0.150f, 0.200f, 0.285f, 1f) : RelicNavy;

            if (tabFills[i].color != fill)
                tabFills[i].color = fill;
        }

        if (appliedTab == shopTab)
            return;

        appliedTab = shopTab;

        for (int i = 0; i < productRows.Length; i++)
            if (productRows[i] != null)
                productRows[i].gameObject.SetActive(false);   // las tarjetas grandes antiguas ya no se usan

        relicRoot.SetActive(true);

        if (relicTip != null)
            relicTip.gameObject.SetActive(false);

        // La descripcion de reliquias lleva mas lineas: texto autoajustable; en consumibles se restaura el estilo original.
        if (detailDescription != null)
        {
            detailDescription.enableAutoSizing = true;
            detailDescription.fontSizeMin = 13f;
            detailDescription.fontSizeMax = detailFontSize;
            detailDescription.fontSize = detailFontSize;
            detailDescription.lineSpacing = 2f;
            detailDescription.overflowMode = TextOverflowModes.Truncate;
        }

        if (detailIcon != null)
            detailIcon.preserveAspect = true;
    }

    private string HeroName(string heroId)
    {
        if (relicNames == null)
            relicNames = FindAnyObjectByType<CharacterPanelController>(FindObjectsInactive.Include);

        HeroProfile hero = relicNames != null ? relicNames.Profile(heroId) : null;
        return hero != null ? hero.displayName : heroId;
    }

    private static string EffectsText(RelicData data)
    {
        string text = "";

        foreach (RelicModifier m in data.bonuses)
            text += (text != "" ? "   " : "") + "<color=#7FD08F>+" + m.amount + RelicData.Suffix(m.stat) + " " + RelicData.StatName(m.stat) + "</color>";

        foreach (RelicModifier m in data.penalties)
            text += (text != "" ? "   " : "") + "<color=#E56B5E>−" + m.amount + RelicData.Suffix(m.stat) + " " + RelicData.StatName(m.stat) + "</color>";

        return text;
    }

    private void RefreshRelicShop()
    {
        if (relicDirty)
            RebuildRelicProducts();

        SetText(shopCoinsText, GameSession.Coins.ToString());

        int coins = GameSession.Coins;
        ShopEntry? hoveredEntry = null;

        for (int i = 0; i < relicProducts.Count && i < relicCards.Count; i++)
        {
            RelicCard card = relicCards[i];
            ShopEntry entry = relicProducts[i];
            bool selected = i == selectedRelic;
            bool hovered = card.hover != null && card.hover.Hovered;
            bool blocked;       // ya poseida (reliquia unica) o agotado (consumible)
            string blockedText;
            Color idle;

            if (entry.IsRelic)
            {
                RelicShop.Result state = RelicShop.Check(entry.relic);
                blocked = state == RelicShop.Result.AlreadyOwned || state == RelicShop.Result.SoldOut;
                blockedText = state == RelicShop.Result.SoldOut ? "AGOTADA" : "YA POSEÍDA";
                idle = Color.Lerp(RelicBorderIdle, RelicData.RarityColor(entry.relic.rarity), 0.45f);
            }
            else
            {
                blocked = GameSession.GetShopStock((GameSession.Consumable)entry.consumable) <= 0;
                blockedText = "AGOTADO";
                idle = RelicBorderIdle;
            }

            SetBorderColor(card.border, selected ? RelicBorderSelected : hovered ? RelicBorderHover : idle);

            if (hovered)
                hoveredEntry = entry;

            if (card.priceGroup.activeSelf == blocked)
                card.priceGroup.SetActive(!blocked);

            if (card.ownedLabel.activeSelf != blocked)
                card.ownedLabel.SetActive(blocked);

            card.ownedLabel.GetComponent<TMP_Text>().text = blockedText;

            Color priceColor = coins < EntryPrice(entry) ? RelicBad : new Color(0.831f, 0.686f, 0.416f, 1f);

            if (card.price.color != priceColor)
                card.price.color = priceColor;

            card.icon.color = blocked ? new Color(0.5f, 0.5f, 0.55f, 1f) : Color.white;
        }

        UpdateRelicTip(hoveredEntry);

        bool has = selectedRelic >= 0 && selectedRelic < relicProducts.Count;
        ShopEntry current = has ? relicProducts[selectedRelic] : default;
        bool canBuy = false;
        string status = "";

        if (!has)
        {
            SetText(detailName, "Sin productos a la venta");
            SetText(detailDescription, "");
            SetText(detailAvailable, "");
            SetText(detailOwned, "");

            if (detailIcon != null)
                detailIcon.enabled = false;
        }
        else if (current.IsRelic)
        {
            RelicData relic = current.relic;

            if (detailIcon != null)
            {
                detailIcon.enabled = relic.icon != null;
                detailIcon.sprite = relic.icon;
                detailIcon.rectTransform.sizeDelta = new Vector2(84f, 84f);
                detailIcon.rectTransform.anchoredPosition = detailIconBasePos;
            }

            RelicShop.Result result = RelicShop.Check(relic);
            canBuy = result == RelicShop.Result.Success && Time.unscaledTime >= buyLockedUntil;
            string rarityColor = ColorUtility.ToHtmlStringRGB(RelicData.RarityColor(relic.rarity));
            string header = "<color=#" + rarityColor + ">" + RelicData.RarityName(relic.rarity) + "</color>";

            if (relic.IsExclusive)
                header += "  ·  Exclusiva de " + HeroName(relic.exclusiveHeroId);

            if (relic.uniqueOwnership)
                header += "  ·  Única";

            string body = EffectsText(relic) + "\n" + header;

            if (!string.IsNullOrEmpty(relic.description))
                body += "\n" + relic.description;

            SetText(detailName, relic.displayName);
            SetText(detailDescription, body);
            SetText(detailAvailable, "");
            SetText(detailOwned, "En inventario: " + RelicInventory.CountOwned(relic.typeId));

            if (result == RelicShop.Result.InsufficientFunds || result == RelicShop.Result.AlreadyOwned || result == RelicShop.Result.SoldOut)
                status = RelicShop.Describe(result, relic);
        }
        else
        {
            GameSession.Consumable type = (GameSession.Consumable)current.consumable;
            int stock = GameSession.GetShopStock(type);
            int owned = GameSession.GetCount(type);
            int price = ConsumableService.Price(type);

            if (detailIcon != null)
            {
                detailIcon.enabled = true;
                detailIcon.sprite = current.consumable < productIcons.Length ? productIcons[current.consumable] : null;

                if (detailIconSizes != null && current.consumable < detailIconSizes.Length)
                {
                    detailIcon.rectTransform.sizeDelta = detailIconSizes[current.consumable];
                    detailIcon.rectTransform.anchoredPosition = detailIconBasePos + detailIconOffsets[current.consumable];
                }
            }

            canBuy = stock > 0 && owned < GameSession.MaxPerItem && coins >= price && Time.unscaledTime >= buyLockedUntil;
            SetText(detailName, ConsumableService.Name(type));
            SetText(detailDescription, ProductDescription(type));
            SetText(detailAvailable, "");
            SetText(detailOwned, "En inventario: " + owned + "/" + GameSession.MaxPerItem);

            if (stock <= 0)
                status = "Agotado";
            else if (owned >= GameSession.MaxPerItem)
                status = "Inventario lleno";
            else if (coins < price)
                status = "Monedas insuficientes";
        }

        // Mensaje de la ultima compra unos segundos; despues, el estado del producto seleccionado.
        SetText(shopMessageText, Time.unscaledTime < relicMessageUntil ? relicMessage : status);

        if (detailBuyButton != null && detailBuyButton.interactable != canBuy)
            detailBuyButton.interactable = canBuy;
    }

    // Boton COMPRAR: una sola operacion validada (RelicShop para reliquias, ConsumableService para consumibles), con
    // una pausa corta para que varios clics rapidos no repitan la compra sin querer.
    private void BuySelectedRelic()
    {
        if (Time.unscaledTime < buyLockedUntil)
            return;

        if (selectedRelic < 0 || selectedRelic >= relicProducts.Count)
        {
            relicMessage = "Selecciona un producto.";
            relicMessageUntil = Time.unscaledTime + 3f;
            return;
        }

        ShopEntry entry = relicProducts[selectedRelic];
        buyLockedUntil = Time.unscaledTime + BuyCooldownSeconds;
        relicMessageUntil = Time.unscaledTime + 3f;

        if (entry.IsRelic)
        {
            RelicShop.Result result = RelicShop.TryBuy(entry.relic);
            relicMessage = RelicShop.Describe(result, entry.relic);
            return;
        }

        Buy((GameSession.Consumable)entry.consumable);
        relicMessage = message;
    }

}
