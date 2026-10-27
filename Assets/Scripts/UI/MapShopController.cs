using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Panel provisional de tienda e inventario para el mapa (fuera del
// combate). Toda la logica vive en ConsumableService y GameSession: este
// componente solo la presenta, asi que puede reemplazarse por la interfaz
// definitiva sin tocar las reglas.
//
// Se abre desde la franja superior del mapa (WorldMapController) como
// Tienda o como Inventario, y se cierra con su boton. No se puede usar
// mientras haya un combate cargado.
public class MapShopController : MonoBehaviour
{
    private const int MemberRows = 4;

    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject veil;
    [SerializeField] private Text titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text coinsText;
    [SerializeField] private Text messageText;
    [SerializeField] private Text[] shopLabels = new Text[2];
    [SerializeField] private Button[] buyButtons = new Button[2];
    [SerializeField] private Text[] memberLabels = new Text[MemberRows];
    [SerializeField] private Button[] potionButtons = new Button[MemberRows];
    [SerializeField] private Button[] antidoteButtons = new Button[MemberRows];

    [Header("Arte provisional (mismo conjunto que el mapa)")]
    [SerializeField] private MapArtSet art;

    // Ventana de tienda (modo Tienda): dos productos, detalle y boton Comprar. El modo Inventario
    // sigue usando el panel de arriba. La logica de compra sigue en ConsumableService / GameSession.
    [Header("Ventana de tienda")]
    [SerializeField] private GameObject shopWindow;
    [SerializeField] private Button shopCloseButton;
    [SerializeField] private TMP_Text shopCoinsText;
    [SerializeField] private TMP_Text shopMessageText;
    [SerializeField] private Button[] productRows = new Button[2];
    [SerializeField] private Image[] productRowImages = new Image[2];
    [SerializeField] private TMP_Text[] productNames = new TMP_Text[2];
    [SerializeField] private TMP_Text[] productDescriptions = new TMP_Text[2];
    [SerializeField] private TMP_Text[] productPrices = new TMP_Text[2];
    [SerializeField] private Sprite[] productIcons = new Sprite[2];
    [SerializeField] private Sprite rowNormalSprite;
    [SerializeField] private Sprite rowSelectedSprite;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private TMP_Text detailAvailable;
    [SerializeField] private TMP_Text detailOwned;
    [SerializeField] private Button detailBuyButton;

    private int selectedProduct;
    private int closedFrame = -1;

    private static readonly Color PanelColor = new Color(0.050f, 0.062f, 0.100f, 0.97f);
    private static readonly Color RowColor = new Color(0.095f, 0.118f, 0.185f, 1f);
    private static readonly Color GoldLine = new Color(0.78f, 0.62f, 0.30f, 0.85f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.21f, 0.32f, 1f);
    private static readonly Color TextColor = new Color(0.90f, 0.92f, 0.96f, 1f);
    private static readonly Color GoldColor = new Color(0.95f, 0.85f, 0.5f, 1f);

    private Font font;
    private string message = "";
    private bool isOpen;
    private bool shopMode = true;

    public bool IsOpen => isOpen && CanOpen();
    public bool ShopMode => shopMode;

    // La tienda se cerro en este mismo fotograma (p. ej. con Escape): el menu de pausa no debe
    // abrirse con esa misma pulsacion.
    public bool ClosedThisFrame => closedFrame == Time.frameCount;

    // Abre el panel: tienda (compra y uso) o inventario (solo uso).
    public void Open(bool asShop)
    {
        if (!CanOpen() || isOpen)
            return;

        isOpen = true;
        shopMode = asShop;
        message = "";
    }

    public void Close()
    {
        if (isOpen)
            closedFrame = Time.frameCount;

        isOpen = false;
    }

    public void SelectProduct(int index)
    {
        selectedProduct = Mathf.Clamp(index, 0, 1);
    }

    // Boton COMPRAR de la ventana de tienda: compra una unidad del producto seleccionado.
    public void BuySelected()
    {
        Buy((GameSession.Consumable)selectedProduct);
    }

    private void Start()
    {
        if (canvas == null)
            BuildUI();

        // Mapa abierto directamente (editor): sesion de desarrollo.
        GameSession.EnsureSession();

        WireButtons();
    }

    private void Update()
    {
        // Escape cierra la tienda (y el inventario). closedFrame evita que la misma pulsacion abra
        // despues la pausa.
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();

        Refresh();
    }

    // La tienda no se puede abrir durante el combate.
    public bool CanOpen()
    {
        return !GameSession.InCombat;
    }

    public void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        for (int i = 0; i < 2; i++)
        {
            GameSession.Consumable type = (GameSession.Consumable)i;
            buyButtons[i].onClick.RemoveAllListeners();
            buyButtons[i].onClick.AddListener(() => Buy(type));
        }

        if (shopCloseButton != null)
        {
            shopCloseButton.onClick.RemoveAllListeners();
            shopCloseButton.onClick.AddListener(Close);
        }

        if (detailBuyButton != null)
        {
            detailBuyButton.onClick.RemoveAllListeners();
            detailBuyButton.onClick.AddListener(BuySelected);
        }

        for (int i = 0; i < productRows.Length; i++)
        {
            if (productRows[i] == null)
                continue;

            int index = i;
            productRows[i].onClick.RemoveAllListeners();
            productRows[i].onClick.AddListener(() => SelectProduct(index));
        }

        for (int i = 0; i < MemberRows; i++)
        {
            int row = i;
            potionButtons[i].onClick.RemoveAllListeners();
            potionButtons[i].onClick.AddListener(
                () => UseOnMember(GameSession.Consumable.Potion, row));
            antidoteButtons[i].onClick.RemoveAllListeners();
            antidoteButtons[i].onClick.AddListener(
                () => UseOnMember(GameSession.Consumable.Antidote, row));
        }
    }

    // Compra una unidad por clic.
    public bool Buy(GameSession.Consumable type)
    {
        if (!CanOpen() || !shopMode)
        {
            message = "La tienda no esta disponible durante el combate.";
            return false;
        }

        message = ConsumableService.Buy(type, out bool success);
        return success;
    }

    // Usa un objeto sobre un miembro del equipo (no consume turno).
    public bool UseOnMember(GameSession.Consumable type, int row)
    {
        if (!CanOpen() || row < 0 || row >= GameSession.Party.Count)
        {
            message = "No hay un aliado en esa fila.";
            return false;
        }

        bool ok = ConsumableService.TryUseOnMember(
            type, GameSession.Party[row].id, out string result);

        message = result;
        return ok;
    }

    private void Refresh()
    {
        if (panel == null)
            return;

        bool open = isOpen && CanOpen();
        bool showShopWindow = open && shopMode && shopWindow != null;
        bool showPanel = open && !showShopWindow;

        if (panel.activeSelf != showPanel)
            panel.SetActive(showPanel);

        if (shopWindow != null && shopWindow.activeSelf != showShopWindow)
            shopWindow.SetActive(showShopWindow);

        if (veil != null && veil.activeSelf != open)
            veil.SetActive(open);

        if (!open)
            return;

        if (showShopWindow)
        {
            RefreshShopWindow();
            return;
        }

        SetText(coinsText, "Coronas: " + GameSession.Coins + " CR");
        SetText(messageText, message);
        SetText(titleText, shopMode ? "Tienda" : "Inventario");

        for (int i = 0; i < 2; i++)
        {
            GameSession.Consumable type = (GameSession.Consumable)i;
            shopLabels[i].gameObject.SetActive(shopMode);
            buyButtons[i].gameObject.SetActive(shopMode);
            SetText(shopLabels[i],
                ConsumableService.Name(type) + "   " +
                ConsumableService.Price(type) + " CR   (tienes " +
                GameSession.GetCount(type) + "/" + GameSession.MaxPerItem + ")");
        }

        for (int i = 0; i < MemberRows; i++)
        {
            bool has = i < GameSession.Party.Count;

            memberLabels[i].gameObject.SetActive(has);
            potionButtons[i].gameObject.SetActive(has);
            antidoteButtons[i].gameObject.SetActive(has);

            if (!has)
                continue;

            GameSession.PartyMemberState member = GameSession.Party[i];
            SetText(memberLabels[i],
                member.id + "   " + member.health + "/" + member.maxHealth + " HP" +
                (member.hasCurableStatus ? "  (estado curable)" : ""));
        }

        if (GameSession.Party.Count == 0)
            SetText(messageText, message == ""
                ? "Aun no hay datos del equipo: se guardan al ganar un combate."
                : message);
    }

    private static void SetText(Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

    private static string ProductDescription(GameSession.Consumable type)
    {
        return type == GameSession.Consumable.Potion
            ? "Recupera el " + ConsumableService.PotionHealPercent +
              " % del HP máximo de un aliado durante el combate."
            : "Elimina Sangrado, Veneno y Quemadura.";
    }

    // Datos de la ventana de tienda, siempre tomados de ConsumableService y GameSession. La tienda
    // no limita existencias (solo el maximo del inventario), asi que no se muestra una cifra.
    private void RefreshShopWindow()
    {
        SetText(shopCoinsText, GameSession.Coins.ToString());
        SetText(shopMessageText, message);

        for (int i = 0; i < 2; i++)
        {
            GameSession.Consumable type = (GameSession.Consumable)i;
            bool selected = i == selectedProduct;

            if (productRowImages[i] != null)
            {
                Sprite wanted = selected ? rowSelectedSprite : rowNormalSprite;

                if (wanted != null && productRowImages[i].sprite != wanted)
                    productRowImages[i].sprite = wanted;

                productRowImages[i].color = selected
                    ? Color.white
                    : new Color(0.50f, 0.50f, 0.56f, 1f);
            }

            SetText(productNames[i], ConsumableService.Name(type));
            SetText(productDescriptions[i], ProductDescription(type));
            SetText(productPrices[i], ConsumableService.Price(type).ToString());
        }

        GameSession.Consumable current = (GameSession.Consumable)selectedProduct;

        if (detailIcon != null && selectedProduct < productIcons.Length)
            detailIcon.sprite = productIcons[selectedProduct];

        SetText(detailName, ConsumableService.Name(current));
        SetText(detailDescription, ProductDescription(current));
        SetText(detailAvailable, "Disponible");
        SetText(detailOwned, "Inventario: " + GameSession.GetCount(current) +
                             "/" + GameSession.MaxPerItem);
    }

    // ------------------------------------------------------------
    // Construccion de la interfaz provisional
    // ------------------------------------------------------------

#if UNITY_EDITOR
    // ------------------------------------------------------------
    // Ventana de tienda (se genera en el editor y se guarda en Map.unity)
    // ------------------------------------------------------------

    private static readonly Color ShopGold = new Color(0.831f, 0.686f, 0.416f, 1f);   // #D4AF6A
    private static readonly Color ShopIvory = new Color(0.910f, 0.863f, 0.776f, 1f);  // #E8DCC6
    private static readonly Color ShopNavy = new Color(0.063f, 0.082f, 0.118f, 1f);   // #10151E

    private static RectTransform ShopRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image ShopImage(RectTransform rect, Sprite sprite, float multiplier, Color color, bool raycast)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;

        if (multiplier > 0f)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
        }
        else
        {
            image.preserveAspect = true;
        }

        return image;
    }

    private static TMP_Text ShopText(Transform parent, string name, Vector2 pos, Vector2 size, string text,
        float fontSize, Color color, TextAlignmentOptions alignment, TMP_FontAsset font, float spacing = 0f)
    {
        RectTransform rect = ShopRect(name, parent, pos, size);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.characterSpacing = spacing;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static Sprite ShopSubSprite(string path, string name)
    {
        foreach (Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite sprite && sprite.name == name)
                return sprite;

        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Construye "VentanaTienda" dentro del Canvas de la tienda: pestana TODO arriba a la izquierda,
    // chip de Coronas, lista de dos productos, mensaje, detalle con Comprar y boton X (UI_Cerrar).
    public void BuildShopWindowInEditor()
    {
        Transform parent = canvas.transform;
        Transform old = parent.Find("VentanaTienda");

        if (old != null)
            DestroyImmediate(old.gameObject);

        Sprite windowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/opciones 1.png");
        Sprite normal = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/menuNormal.png");
        Sprite hover = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/menuHover.png");
        Sprite disabled = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/menuDisable.png");
        Sprite closeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Iconos/Ayuda/UI_Cerrar.png");
        Sprite potion = ShopSubSprite("Assets/Sprites/Iconos/vida.png", "vida_0");
        Sprite antidote = ShopSubSprite("Assets/Sprites/Iconos/antidoto.png", "antidoto_1");
        Sprite crowns = ShopSubSprite("Assets/Sprites/Iconos/coronas.png", "coronas");
        TMP_FontAsset bold = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/GeorgiaBold_OS_SDF.asset");
        TMP_FontAsset regular = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Georgia_OS_SDF.asset");

        // Las filas se distinguen por tinte (seleccionada = marco dorado brillante), sin cambiar de sprite.
        rowNormalSprite = null;
        rowSelectedSprite = null;
        productIcons = new[] { potion, antidote };

        // Velo que oscurece el mapa y bloquea los clics sobre sus nodos.
        if (veil != null)
        {
            Image veilImage = veil.GetComponent<Image>();
            veilImage.color = new Color(ShopNavy.r, ShopNavy.g, ShopNavy.b, 0.8f);
            veilImage.raycastTarget = true;
        }

        // Medidas de referencia (1920x1080). La ventana se centra visualmente: el marco visible deja
        // ~24 px arriba y ~31 px abajo, de ahi el desplazamiento de -3,5 px.
        const float WinW = 960f, WinH = 772f;
        const float VisTop = WinH * 0.5f - 24f;
        RectTransform window = ShopRect("VentanaTienda", parent, new Vector2(0f, -3.5f), new Vector2(WinW, WinH));
        shopWindow = window.gameObject;
        ShopImage(window, windowSprite, 4.4f, Color.white, true);

        // Lista de productos (se crea antes que la pestana para que esta quede encima del borde).
        const float TabH = 64f, RowH = 120f, Gap = 10f, ListPad = 30f, SideX = 380f;
        float tabTop = VisTop - 36f;
        float panelTop = tabTop - TabH + 6f;
        float panelH = 2f * RowH + Gap + 2f * ListPad;
        RectTransform listPanel = ShopRect("PanelProductos", window, new Vector2(0f, panelTop - panelH * 0.5f), new Vector2(2f * SideX, panelH));
        ShopImage(listPanel, windowSprite, 7f, Color.white, false);

        productRows = new Button[2];
        productRowImages = new Image[2];
        productNames = new TMP_Text[2];
        productDescriptions = new TMP_Text[2];
        productPrices = new TMP_Text[2];
        Sprite[] icons = { potion, antidote };

        for (int i = 0; i < 2; i++)
        {
            float y = panelTop - ListPad - RowH * 0.5f - i * (RowH + Gap);
            RectTransform row = ShopRect("Fila_Producto_" + i, window, new Vector2(0f, y), new Vector2(676f, RowH));
            Image rowImage = ShopImage(row, windowSprite, 9f, Color.white, true);
            Button rowButton = row.gameObject.AddComponent<Button>();
            rowButton.targetGraphic = rowImage;
            rowButton.transition = Selectable.Transition.None;
            Navigation none = rowButton.navigation;
            none.mode = Navigation.Mode.None;
            rowButton.navigation = none;
            productRows[i] = rowButton;
            productRowImages[i] = rowImage;

            RectTransform frame = ShopRect("MarcoIcono", row, new Vector2(-262f, 0f), new Vector2(92f, 92f));
            ShopImage(frame, windowSprite, 11f, Color.white, false);
            ShopImage(ShopRect("Icono", row, new Vector2(-262f, 0f), new Vector2(68f, 68f)), icons[i], 0f, Color.white, false);

            productNames[i] = ShopText(row, "Nombre", new Vector2(-2f, 27f), new Vector2(392f, 40f), "", 32f, ShopIvory, TextAlignmentOptions.TopLeft, bold);
            productNames[i].textWrappingMode = TextWrappingModes.NoWrap;
            productDescriptions[i] = ShopText(row, "Descripcion", new Vector2(-2f, -18f), new Vector2(392f, 52f), "", 20f, ShopIvory, TextAlignmentOptions.TopLeft, regular);
            ShopImage(ShopRect("Corona", row, new Vector2(226f, 0f), new Vector2(48f, 48f)), crowns, 0f, Color.white, false);
            productPrices[i] = ShopText(row, "Precio", new Vector2(288f, 0f), new Vector2(64f, 44f), "", 34f, ShopGold, TextAlignmentOptions.Center, bold);
            productPrices[i].textWrappingMode = TextWrappingModes.NoWrap;
        }

        // Cabecera: TODO a la izquierda y Coronas a la derecha, a la misma altura y alineadas con los
        // bordes visibles del panel de productos (~362 px del centro).
        float tabY = tabTop - TabH * 0.5f;
        RectTransform tab = ShopRect("PestanaTodo", window, new Vector2(-262f, tabY), new Vector2(226f, TabH));
        ShopImage(tab, windowSprite, 10f, Color.white, false);
        TMP_Text tabLabel = ShopText(tab, "Texto", Vector2.zero, new Vector2(200f, 46f), "TODO", 30f, ShopGold, TextAlignmentOptions.Center, bold, 3f);
        tabLabel.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform chip = ShopRect("Coronas", window, new Vector2(262f, tabY), new Vector2(226f, TabH));
        ShopImage(chip, windowSprite, 10f, Color.white, false);
        ShopImage(ShopRect("Icono", chip, new Vector2(-66f, 0f), new Vector2(44f, 44f)), crowns, 0f, Color.white, false);
        shopCoinsText = ShopText(chip, "Cantidad", new Vector2(24f, 0f), new Vector2(120f, 46f), "", 32f, ShopIvory, TextAlignmentOptions.Center, bold);
        shopCoinsText.textWrappingMode = TextWrappingModes.NoWrap;

        // Mensaje de compra.
        float messageY = panelTop - panelH - 6f - 14f;
        shopMessageText = ShopText(window, "Mensaje", new Vector2(0f, messageY), new Vector2(2f * SideX, 30f), "", 22f, ShopGold, TextAlignmentOptions.Center, regular);
        shopMessageText.fontStyle = FontStyles.Italic;
        shopMessageText.textWrappingMode = TextWrappingModes.NoWrap;

        // Detalle del producto seleccionado: icono a la izquierda, nombre/descripcion y las dos
        // etiquetas (Disponible / Inventario) en el centro, boton COMPRAR a la derecha.
        float detailTop = messageY - 14f - 6f;
        const float DetailH = 244f, DetailPad = 34f;
        float detailY = detailTop - DetailH * 0.5f;
        RectTransform detail = ShopRect("PanelDetalle", window, new Vector2(0f, detailY), new Vector2(2f * SideX, DetailH));
        ShopImage(detail, windowSprite, 7f, Color.white, false);

        float innerTop = detailTop - DetailPad;
        float innerBottom = detailTop - DetailH + DetailPad;
        RectTransform bigFrame = ShopRect("MarcoIconoDetalle", window, new Vector2(-266f, detailY), new Vector2(132f, 132f));
        ShopImage(bigFrame, windowSprite, 8f, Color.white, false);
        detailIcon = ShopImage(ShopRect("IconoDetalle", window, new Vector2(-266f, detailY), new Vector2(92f, 92f)), potion, 0f, Color.white, false);

        const float CenterX = -42f, CenterW = 270f;
        detailName = ShopText(window, "NombreDetalle", new Vector2(CenterX, innerTop - 20f), new Vector2(CenterW, 40f), "", 30f, ShopIvory, TextAlignmentOptions.TopLeft, bold);
        detailName.textWrappingMode = TextWrappingModes.NoWrap;
        detailDescription = ShopText(window, "DescripcionDetalle", new Vector2(CenterX, innerTop - 40f - 34f), new Vector2(CenterW, 78f), "", 20f, ShopIvory, TextAlignmentOptions.TopLeft, regular);

        float pillY = innerBottom + 21f;
        RectTransform pillA = ShopRect("Disponible", window, new Vector2(CenterX - CenterW * 0.5f + 70f, pillY), new Vector2(140f, 46f));
        ShopImage(pillA, windowSprite, 14f, Color.white, false);
        detailAvailable = ShopText(pillA, "Texto", Vector2.zero, new Vector2(120f, 30f), "", 18f, ShopGold, TextAlignmentOptions.Center, bold);
        detailAvailable.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform pillB = ShopRect("Inventario", window, new Vector2(CenterX - CenterW * 0.5f + 70f + 150f, pillY), new Vector2(140f, 46f));
        ShopImage(pillB, windowSprite, 14f, new Color(0.62f, 0.62f, 0.68f, 1f), false);
        detailOwned = ShopText(pillB, "Texto", Vector2.zero, new Vector2(124f, 30f), "", 17f, ShopIvory, TextAlignmentOptions.Center, regular);
        detailOwned.textWrappingMode = TextWrappingModes.NoWrap;

        // Boton COMPRAR con el sprite de boton del juego: la parte visible mide ~0,93 x 0,42 del rectangulo
        // (como los botones de la pausa), asi que el raycast se recorta a la parte visible.
        RectTransform buy = ShopRect("BotonComprar", window, new Vector2(228f, detailY), new Vector2(252f, 150f));
        Image buyImage = ShopImage(buy, normal, 4.49f, Color.white, true);
        buyImage.raycastPadding = new Vector4(8f, 44f, 8f, 44f);
        detailBuyButton = buy.gameObject.AddComponent<Button>();
        detailBuyButton.targetGraphic = buyImage;
        detailBuyButton.transition = Selectable.Transition.SpriteSwap;
        SpriteState state = new SpriteState { highlightedSprite = hover, pressedSprite = hover, selectedSprite = normal, disabledSprite = disabled };
        detailBuyButton.spriteState = state;
        Navigation buyNav = detailBuyButton.navigation;
        buyNav.mode = Navigation.Mode.None;
        detailBuyButton.navigation = buyNav;
        TMP_Text buyLabel = ShopText(buy, "Texto", Vector2.zero, new Vector2(230f, 50f), "COMPRAR", 28f, ShopIvory, TextAlignmentOptions.Center, bold, 2f);
        buyLabel.textWrappingMode = TextWrappingModes.NoWrap;

        // Boton X (UI_Cerrar) de 48 x 48, dentro del marco (a ~10 px del borde) y con el area de clic
        // ampliada 12 px por lado (72 x 72).
        RectTransform close = ShopRect("BotonCerrar", window, new Vector2(WinW * 0.5f - 29f - 34f, WinH * 0.5f - 24f - 34f), new Vector2(48f, 48f));
        Image closeImage = ShopImage(close, closeSprite, 0f, Color.white, true);
        closeImage.raycastPadding = new Vector4(-12f, -12f, -12f, -12f);
        shopCloseButton = close.gameObject.AddComponent<Button>();
        shopCloseButton.targetGraphic = closeImage;
        shopCloseButton.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = shopCloseButton.colors;
        colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        shopCloseButton.colors = colors;
        Navigation closeNav = shopCloseButton.navigation;
        closeNav.mode = Navigation.Mode.None;
        shopCloseButton.navigation = closeNav;

        shopWindow.SetActive(false);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private RectTransform Box(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    // Panel redondeado (9-slice) con borde, como los del mapa.
    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color, Color frame)
    {
        RectTransform rect = Box(name, parent, min, max, color);

        if (art != null && art.roundedRect != null)
        {
            Image image = rect.GetComponent<Image>();
            image.sprite = art.roundedRect;
            image.type = Image.Type.Sliced;

            if (art.frame != null)
            {
                RectTransform border = Box("Borde", rect, Vector2.zero, Vector2.one, frame);
                Image borderImage = border.GetComponent<Image>();
                borderImage.sprite = art.frame;
                borderImage.type = Image.Type.Sliced;
            }
        }

        return rect;
    }

    private void Line(string name, Transform parent, float y, float x0, float x1)
    {
        RectTransform rect = Box(name, parent, new Vector2(x0, y), new Vector2(x1, y), new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.55f));
        rect.offsetMin = new Vector2(0f, -1f);
        rect.offsetMax = new Vector2(0f, 1f);
    }

    private void RowIcon(string name, Transform parent, float x, float y, Sprite sprite, Color color)
    {
        RectTransform rect = Box(name, parent, new Vector2(x, y), new Vector2(x, y), color);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(42f, 42f);
        rect.GetComponent<Image>().sprite = sprite;
    }

    private Text Label(string name, Transform parent, Vector2 min, Vector2 max, int size, TextAnchor anchor, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = new Vector2(8, 2); rect.offsetMax = new Vector2(-8, -2);
        Text text = go.GetComponent<Text>();
        text.font = font; text.fontSize = size; text.alignment = anchor;
        text.color = color; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Button MakeButton(string name, Transform parent, Vector2 min, Vector2 max, string label, int size)
    {
        RectTransform rect = Panel(name, parent, min, max, ButtonColor, GoldLine);
        Image image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.70f, 0.70f, 0.70f, 1f);
        colors.disabledColor = new Color(0.60f, 0.60f, 0.60f, 1f);
        button.colors = colors;

        Text text = Label("Texto", rect, Vector2.zero, Vector2.one, size, TextAnchor.MiddleCenter, TextColor);
        text.text = label;
        text.fontStyle = FontStyle.Bold;
        return button;
    }

    public void BuildUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasGo = new GameObject("Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();

        // Overlay: no depende de la camara y queda sobre el mapa.
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Velo que bloquea los clics sobre el mapa mientras el panel esta abierto.
        veil = Box("Velo", canvasGo.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.55f)).gameObject;

        RectTransform root = Panel("PanelTienda", canvasGo.transform, new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.92f), PanelColor, GoldLine);
        panel = root.gameObject;
        root.GetComponent<Image>().raycastTarget = true;

        titleText = Label("Titulo", root, new Vector2(0.05f, 0.91f), new Vector2(0.78f, 0.99f), 38, TextAnchor.MiddleLeft, GoldColor);
        titleText.text = "Tienda";
        titleText.fontStyle = FontStyle.Bold;
        closeButton = MakeButton("Cerrar", root, new Vector2(0.80f, 0.92f), new Vector2(0.97f, 0.985f), "Cerrar", 22);
        Line("SeparadorTitulo", root, 0.905f, 0.04f, 0.96f);

        Panel("FondoCoronas", root, new Vector2(0.28f, 0.835f), new Vector2(0.72f, 0.895f), RowColor, new Color(0.30f, 0.34f, 0.46f, 0.9f));
        coinsText = Label("Coronas", root, new Vector2(0.28f, 0.835f), new Vector2(0.72f, 0.895f), 28, TextAnchor.MiddleCenter, GoldColor);
        coinsText.fontStyle = FontStyle.Bold;

        Sprite[] icons = { art != null ? art.potionIcon : null, art != null ? art.antidoteIcon : null };
        Color[] iconColors = { new Color(0.95f, 0.50f, 0.58f, 1f), new Color(0.50f, 0.90f, 0.74f, 1f) };

        for (int i = 0; i < 2; i++)
        {
            float top = 0.815f - i * 0.115f;
            Panel("Fila_Producto_" + i, root, new Vector2(0.04f, top - 0.095f), new Vector2(0.96f, top), RowColor, new Color(0.30f, 0.34f, 0.46f, 0.7f));
            RowIcon("Icono_Producto_" + i, root, 0.075f, top - 0.0475f, icons[i], iconColors[i]);
            shopLabels[i] = Label("Producto_" + i, root, new Vector2(0.115f, top - 0.095f), new Vector2(0.70f, top), 24, TextAnchor.MiddleLeft, TextColor);
            buyButtons[i] = MakeButton("Comprar_" + i, root, new Vector2(0.73f, top - 0.083f), new Vector2(0.945f, top - 0.012f), "Comprar", 24);
        }

        messageText = Label("Mensaje", root, new Vector2(0.04f, 0.50f), new Vector2(0.96f, 0.58f), 22, TextAnchor.MiddleCenter, new Color(1f, 0.82f, 0.50f, 1f));
        messageText.fontStyle = FontStyle.Italic;

        Text team = Label("Equipo", root, new Vector2(0.04f, 0.425f), new Vector2(0.96f, 0.495f), 24, TextAnchor.MiddleLeft, GoldColor);
        team.text = "Equipo  (usar objetos no consume turno)";
        team.fontStyle = FontStyle.Bold;
        Line("SeparadorEquipo", root, 0.42f, 0.04f, 0.96f);

        for (int i = 0; i < MemberRows; i++)
        {
            float top = 0.405f - i * 0.098f;
            Panel("Fila_Miembro_" + i, root, new Vector2(0.04f, top - 0.088f), new Vector2(0.96f, top), RowColor, new Color(0.30f, 0.34f, 0.46f, 0.6f));
            memberLabels[i] = Label("Miembro_" + i, root, new Vector2(0.06f, top - 0.088f), new Vector2(0.50f, top), 22, TextAnchor.MiddleLeft, TextColor);
            potionButtons[i] = MakeButton("Pocion_" + i, root, new Vector2(0.52f, top - 0.080f), new Vector2(0.73f, top - 0.008f), "Poción", 20);
            antidoteButtons[i] = MakeButton("Antidoto_" + i, root, new Vector2(0.745f, top - 0.080f), new Vector2(0.945f, top - 0.008f), "Antídoto", 20);
        }

        panel.SetActive(false);
        veil.SetActive(false);

        EventSystem existing = null;
        foreach (EventSystem es in FindObjectsByType<EventSystem>())
            if (es.gameObject.scene == gameObject.scene) existing = es;

        if (existing == null)
        {
            GameObject esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            esGo.transform.SetParent(transform, false);
            esGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
