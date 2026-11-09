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
public partial class MapShopController : MonoBehaviour
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
    [SerializeField] private Image detailIcon;
    [SerializeField] private Vector2 detailIconBasePos;
    [SerializeField] private Vector2[] detailIconSizes = new Vector2[2];
    [SerializeField] private Vector2[] detailIconOffsets = new Vector2[2];
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private TMP_Text detailAvailable;
    [SerializeField] private TMP_Text detailOwned;
    [SerializeField] private Button detailBuyButton;
    [SerializeField] private GameObject[] cardHoverFrame = new GameObject[2];
    [SerializeField] private ShopCardHover[] cardHover = new ShopCardHover[2];
    [SerializeField] private GameObject[] cardSoldOut = new GameObject[2];
    [SerializeField] private GameObject[] cardPriceGroup = new GameObject[2];
    [SerializeField] private Image[] cardIcon = new Image[2];

    // Ventana de inventario del mapa (solo consulta): cuadricula de 4 x 3 casillas y panel de detalle. Muestra
    // unicamente los consumibles que el jugador tiene en GameSession; usar objetos solo ocurre en combate.
    private const int InventorySlots = 12;

    [Header("Ventana de inventario")]
    [SerializeField] private GameObject inventoryWindow;
    [SerializeField] private Button inventoryCloseButton;
    [SerializeField] private Button[] invSlotButtons = new Button[InventorySlots];
    [SerializeField] private Image[] invSlotIcons = new Image[InventorySlots];
    [SerializeField] private TMP_Text[] invSlotQuantities = new TMP_Text[InventorySlots];
    [SerializeField] private GameObject[] invSlotSelection = new GameObject[InventorySlots];
    [SerializeField] private GameObject invEmptyMessage;
    [SerializeField] private GameObject invDetailContent;
    [SerializeField] private Image invDetailIcon;
    [SerializeField] private TMP_Text invDetailName;
    [SerializeField] private TMP_Text invDetailDescription;
    [SerializeField] private TMP_Text invDetailQuantity;
    [SerializeField] private Vector2[] invSlotIconSizes = new Vector2[2];
    [SerializeField] private Vector2[] invSlotIconOffsets = new Vector2[2];
    [SerializeField] private Vector2 invDetailIconBase;
    [SerializeField] private Vector2[] invDetailIconSizes = new Vector2[2];
    [SerializeField] private Vector2[] invDetailIconOffsets = new Vector2[2];

    private int invSelected = -1;
    private readonly int[] invSlotItem = new int[InventorySlots];

    private int selectedProduct;
    private int closedFrame = -1;

    // Animacion de entrada y salida, igual que la del menu de pausa y las Opciones del menu principal: fundido
    // y escala de 0,95 a 1 con smoothstep en 0,3 s, con tiempo no escalado. Afecta a la ventana (tienda o
    // inventario) y al velo del fondo.
    private const float OpenAnimationSeconds = 0.3f;
    private const float ClosedWindowScale = 0.95f;
    private float openProgress;      // velo: 0 cerrado, 1 abierto (lineal; la curva se aplica al dibujar)
    private float windowProgress;    // ventana: igual, pero vuelve a 0 al cambiar de tienda a inventario y viceversa
    private int lastKind;            // 1 tienda, 2 inventario, 3 panel antiguo (para animar tambien al cerrar)
    private CanvasGroup shopGroup;
    private CanvasGroup inventoryGroup;
    private CanvasGroup veilGroup;

    // Color del borde de un recuadro hecho con cuatro franjas ("Borde_*"). Sin franjas (estructura antigua),
    // cambia el color de la propia imagen.
    private static void SetBorderColor(Image box, Color color)
    {
        bool found = false;

        foreach (Transform child in box.transform)
        {
            if (!child.name.StartsWith("Borde_"))
                continue;

            Image strip = child.GetComponent<Image>();

            if (strip != null && strip.color != color)
                strip.color = color;

            found = true;
        }

        if (!found && box.color != color)
            box.color = color;
    }

    private static CanvasGroup GroupOf(GameObject go, ref CanvasGroup cache)
    {
        if (cache == null && go != null)
        {
            cache = go.GetComponent<CanvasGroup>();

            if (cache == null)
                cache = go.AddComponent<CanvasGroup>();
        }

        return cache;
    }

    // Aplica la curva smoothstep al fundido y a la escala de una ventana.
    private static void ApplyWindowAnimation(GameObject window, CanvasGroup group, float eased, bool interactable)
    {
        if (window == null || group == null)
            return;

        group.alpha = eased;
        group.interactable = interactable;
        window.transform.localScale = Vector3.one * Mathf.Lerp(ClosedWindowScale, 1f, eased);
    }

    // Con una ventana abierta, los botones Inventario y Tienda del HUD se dibujan por encima del velo y siguen
    // activos (sin recuadro ni hueco en el velo). Se consigue con un Canvas anidado que sobrescribe el orden de
    // dibujo del grupo de atajos, solo mientras la tienda o el inventario estan visibles.
    private const int ShortcutsSortingOrder = 150;
    private Transform shortcutGroup;
    private float nextShortcutSearch;

    private void SetShortcutsOnTop(bool onTop)
    {
        if (shortcutGroup == null)
        {
            if (!onTop || Time.unscaledTime < nextShortcutSearch)
                return;

            nextShortcutSearch = Time.unscaledTime + 1f;
            GameObject found = GameObject.Find("GrupoAtajos");

            if (found == null)
                return;

            shortcutGroup = found.transform;
        }

        Canvas nested = shortcutGroup.GetComponent<Canvas>();

        if (onTop)
        {
            if (nested == null)
            {
                nested = shortcutGroup.gameObject.AddComponent<Canvas>();
                shortcutGroup.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (!nested.overrideSorting || nested.sortingOrder != ShortcutsSortingOrder)
            {
                nested.overrideSorting = true;
                nested.sortingOrder = ShortcutsSortingOrder;
            }

            return;
        }

        if (nested != null)
        {
            GraphicRaycaster raycaster = shortcutGroup.GetComponent<GraphicRaycaster>();

            if (raycaster != null)
                Destroy(raycaster);

            Destroy(nested);
        }
    }


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
    // Lienzo y velo de la tienda: otras ventanas (misiones) se montan en el mismo para verse y animarse igual.
    public Transform WindowParent => veil != null ? veil.transform.parent : null;
    public Color VeilColor => veil != null && veil.GetComponent<Image>() != null ? veil.GetComponent<Image>().color : new Color(0.063f, 0.082f, 0.118f, 0.8f);

    public bool ClosedThisFrame => closedFrame == Time.frameCount;

    // Abre el panel: tienda (compra y uso) o inventario (solo uso).
    public void Open(bool asShop)
    {
        if (!CanOpen() || isOpen)
            return;

        isOpen = true;
        shopMode = asShop;
        message = "";
        relicMessage = "";
        relicDirty = true;
        invSelected = -1;   // al abrir el inventario se selecciona el primer objeto disponible
    }

    // Casilla del inventario (solo consulta): selecciona el objeto que contiene; las vacias no hacen nada.
    public void SelectInventorySlot(int slot)
    {
        if (slot >= 0 && slot < InventorySlots && invSlotItem[slot] >= 0)
            invSelected = invSlotItem[slot];
    }

    public void Close()
    {
        if (isOpen)
            closedFrame = Time.frameCount;

        isOpen = false;
    }

    // Botones de acceso rapido del HUD (Tienda e Inventario): sin nada abierto abren esa vista; con una vista
    // abierta cambian a la otra sin cerrar el velo; con la misma vista ya abierta la cierran.
    public void Toggle(bool asShop)
    {
        if (!CanOpen())
            return;

        if (!isOpen)
        {
            Open(asShop);
            return;
        }

        if (shopMode == asShop)
        {
            Close();
            return;
        }

        shopMode = asShop;
        message = "";
        invSelected = -1;
        windowProgress = 0f;   // la nueva ventana aparece con el mismo fundido; el velo se queda como esta
    }

    public void SelectProduct(int index)
    {
        selectedProduct = Mathf.Clamp(index, 0, 1);
    }

    // Boton COMPRAR de la ventana de tienda: compra una unidad del producto seleccionado.
    public void BuySelected()
    {
        if (relicUiBuilt)
        {
            BuySelectedRelic();
            return;
        }

        Buy((GameSession.Consumable)selectedProduct);
    }

    private void Start()
    {
        if (canvas == null)
            BuildUI();

        // Mapa abierto directamente (editor): sesion de desarrollo.
        GameSession.EnsureSession();

        WireButtons();
        BuildRelicShopUI();   // pestanas Consumibles / Reliquias
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

        if (inventoryCloseButton != null)
        {
            inventoryCloseButton.onClick.RemoveAllListeners();
            inventoryCloseButton.onClick.AddListener(Close);
        }

        for (int i = 0; i < invSlotButtons.Length; i++)
        {
            if (invSlotButtons[i] == null)
                continue;

            int slot = i;
            invSlotButtons[i].onClick.RemoveAllListeners();
            invSlotButtons[i].onClick.AddListener(() => SelectInventorySlot(slot));
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
        bool wantShopWindow = open && shopMode && shopWindow != null;
        bool wantInventory = open && !shopMode && inventoryWindow != null;
        bool wantPanel = open && !wantShopWindow && !wantInventory;

        if (open)
            lastKind = wantShopWindow ? 1 : (wantInventory ? 2 : 3);

        // Progreso de la animacion: sube al abrir y baja al cerrar. Mientras baja se sigue dibujando la
        // ultima ventana abierta; al llegar a 0 se oculta todo.
        float step = Time.unscaledDeltaTime / OpenAnimationSeconds;
        openProgress = Mathf.MoveTowards(openProgress, open ? 1f : 0f, step);
        windowProgress = Mathf.MoveTowards(windowProgress, open ? 1f : 0f, step);
        bool visible = open || openProgress > 0f;
        bool windowVisible = open || windowProgress > 0f;
        float veilEased = openProgress * openProgress * (3f - 2f * openProgress);
        float eased = windowProgress * windowProgress * (3f - 2f * windowProgress);

        bool showShopWindow = windowVisible && lastKind == 1 && shopWindow != null;
        bool showInventory = windowVisible && lastKind == 2 && inventoryWindow != null;
        bool showPanel = windowVisible && lastKind == 3;

        if (panel.activeSelf != showPanel)
            panel.SetActive(showPanel);

        if (shopWindow != null && shopWindow.activeSelf != showShopWindow)
            shopWindow.SetActive(showShopWindow);

        if (inventoryWindow != null && inventoryWindow.activeSelf != showInventory)
            inventoryWindow.SetActive(showInventory);

        if (veil != null && veil.activeSelf != visible)
            veil.SetActive(visible);

        if (veil != null)
        {
            CanvasGroup veilCg = GroupOf(veil, ref veilGroup);

            if (veilCg != null)
                veilCg.alpha = veilEased;
        }

        // Los atajos Inventario y Tienda del HUD quedan por encima del velo mientras la ventana esta visible.
        SetShortcutsOnTop(visible || CharacterPanelController.AnyVisible);

        // Los clics solo llegan a la ventana cuando esta abierta del todo (no mientras se cierra).
        if (showShopWindow)
            ApplyWindowAnimation(shopWindow, GroupOf(shopWindow, ref shopGroup), eased, open);

        if (showInventory)
            ApplyWindowAnimation(inventoryWindow, GroupOf(inventoryWindow, ref inventoryGroup), eased, open);

        if (!open)
            return;

        // Desde aqui, solo la ventana que se quiere ver ahora (puede ser distinta de la que se cierra).
        showShopWindow = wantShopWindow;
        showInventory = wantInventory;

        if (showShopWindow)
        {
            RefreshShopWindow();
            return;
        }

        if (showInventory)
        {
            RefreshInventoryWindow();
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

    // Inventario del mapa: las casillas se rellenan en orden con los consumibles que el jugador tiene
    // (GameSession); el resto queda vacio. La seleccion se conserva mientras el objeto siga disponible.
    private void RefreshInventoryWindow()
    {
        int[] items = new int[2];
        int count = 0;

        for (int i = 0; i < 2; i++)
            if (GameSession.GetCount((GameSession.Consumable)i) > 0)
                items[count++] = i;

        bool valid = false;

        for (int k = 0; k < count; k++)
            if (items[k] == invSelected)
                valid = true;

        if (!valid)
            invSelected = count > 0 ? items[0] : -1;

        for (int s = 0; s < InventorySlots; s++)
        {
            bool has = s < count;
            int type = has ? items[s] : -1;
            invSlotItem[s] = type;

            if (invSlotButtons[s] != null && invSlotButtons[s].interactable != has)
                invSlotButtons[s].interactable = has;

            if (invSlotIcons[s] != null)
            {
                if (invSlotIcons[s].enabled != has)
                    invSlotIcons[s].enabled = has;

                if (has)
                {
                    invSlotIcons[s].sprite = productIcons[type];
                    invSlotIcons[s].rectTransform.sizeDelta = invSlotIconSizes[type];
                    invSlotIcons[s].rectTransform.anchoredPosition = invSlotIconOffsets[type];
                }
            }

            if (invSlotQuantities[s] != null)
                SetText(invSlotQuantities[s], has ? GameSession.GetCount((GameSession.Consumable)type).ToString() : "");

            bool selected = has && type == invSelected;

            if (invSlotSelection[s] != null && invSlotSelection[s].activeSelf != selected)
                invSlotSelection[s].SetActive(selected);
        }

        if (invEmptyMessage != null && invEmptyMessage.activeSelf != (count == 0))
            invEmptyMessage.SetActive(count == 0);

        if (invDetailContent != null && invDetailContent.activeSelf != (count > 0))
            invDetailContent.SetActive(count > 0);

        if (count == 0)
            return;

        GameSession.Consumable current = (GameSession.Consumable)invSelected;

        if (invDetailIcon != null)
        {
            invDetailIcon.sprite = productIcons[invSelected];
            invDetailIcon.rectTransform.sizeDelta = invDetailIconSizes[invSelected];
            invDetailIcon.rectTransform.anchoredPosition = invDetailIconBase + invDetailIconOffsets[invSelected];
        }

        SetText(invDetailName, ConsumableService.Name(current));
        SetText(invDetailDescription, ProductDescription(current));
        // Solo las unidades que posee el jugador (sin el limite maximo); se refresca cada fotograma.
        SetText(invDetailQuantity, GameSession.GetCount(current).ToString());
    }

    // Datos de la ventana de tienda, siempre tomados de ConsumableService y GameSession (precios,
    // inventario del jugador y existencias reales de la tienda).
    private void RefreshShopWindow()
    {
        if (relicUiBuilt)
        {
            ApplyShopTab();
            RefreshRelicShop();
            return;
        }

        SetText(shopCoinsText, GameSession.Coins.ToString());
        SetText(shopMessageText, message);

        for (int i = 0; i < 2; i++)
        {
            GameSession.Consumable type = (GameSession.Consumable)i;
            bool selected = i == selectedProduct;

            // Marco de la tarjeta: con el cursor encima o seleccionada se muestra "hovertienda" en lugar del
            // marco normal; en reposo se conserva el marco normal discreto. Sin resplandores ni escala.
            bool hovered = cardHover != null && i < cardHover.Length && cardHover[i] != null && cardHover[i].Hovered;
            bool showHover = selected || hovered;

            // Borde fino: azul grisaceo en reposo, dorado envejecido con el cursor encima y dorado al estar
            // seleccionada. Sin resplandor ni escala.
            if (productRowImages[i] != null)
            {
                SetBorderColor(productRowImages[i], selected ? new Color(0.831f, 0.686f, 0.416f, 1f)
                    : hovered ? new Color(0.62f, 0.52f, 0.34f, 1f)
                    : new Color(0.239f, 0.290f, 0.361f, 1f));
            }

            if (cardHoverFrame != null && i < cardHoverFrame.Length && cardHoverFrame[i] != null && cardHoverFrame[i].activeSelf != showHover)
                cardHoverFrame[i].SetActive(showHover);

            bool soldOut = GameSession.GetShopStock(type) <= 0;

            if (cardSoldOut != null && i < cardSoldOut.Length && cardSoldOut[i] != null && cardSoldOut[i].activeSelf != soldOut)
                cardSoldOut[i].SetActive(soldOut);

            // Agotado: el texto AGOTADO sustituye al precio y el icono se atenua (sin superponerse).
            if (cardPriceGroup != null && i < cardPriceGroup.Length && cardPriceGroup[i] != null && cardPriceGroup[i].activeSelf == soldOut)
                cardPriceGroup[i].SetActive(!soldOut);

            if (cardIcon != null && i < cardIcon.Length && cardIcon[i] != null)
                cardIcon[i].color = soldOut ? new Color(0.38f, 0.38f, 0.42f, 1f) : Color.white;

            SetText(productNames[i], ConsumableService.Name(type));
            SetText(productPrices[i], ConsumableService.Price(type).ToString());
        }

        GameSession.Consumable current = (GameSession.Consumable)selectedProduct;
        int stock = GameSession.GetShopStock(current);

        if (detailIcon != null && selectedProduct < productIcons.Length)
        {
            detailIcon.sprite = productIcons[selectedProduct];

            if (detailIconSizes != null && selectedProduct < detailIconSizes.Length)
            {
                RectTransform iconRect = detailIcon.rectTransform;
                iconRect.sizeDelta = detailIconSizes[selectedProduct];
                iconRect.anchoredPosition = detailIconBasePos + detailIconOffsets[selectedProduct];
            }
        }

        SetText(detailName, ConsumableService.Name(current));
        SetText(detailDescription, ProductDescription(current));
        SetText(detailAvailable, "");
        SetText(detailOwned, "En inventario: " + GameSession.GetCount(current) +
                             "/" + GameSession.MaxPerItem);

        // Sin existencias no se puede comprar (el rechazo por Coronas o inventario lleno sigue
        // mostrando su mensaje al pulsar).
        if (detailBuyButton != null && detailBuyButton.interactable != (stock > 0))
            detailBuyButton.interactable = stock > 0;
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

    // Tamano y desplazamiento de un icono para que su CONTENIDO visible (pixeles con alfa) mida 'fit' px en su
    // lado mayor y quede centrado en el punto de colocacion, sea cual sea el margen transparente del sprite.
    // Respeta la proporcion del sprite (la misma escala en ambos ejes).
    private static void ShopIconPlacement(Sprite sprite, float fit, out Vector2 size, out Vector2 offset)
    {
        size = new Vector2(fit, fit);
        offset = Vector2.zero;

        if (sprite == null)
            return;

        string path = UnityEditor.AssetDatabase.GetAssetPath(sprite.texture);
        Texture2D file = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!file.LoadImage(System.IO.File.ReadAllBytes(path)))
        {
            Object.DestroyImmediate(file);
            return;
        }

        Rect r = sprite.rect;
        int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
        Color32[] px = file.GetPixels32();

        for (int y = (int)r.yMin; y < (int)r.yMax; y++)
        {
            for (int x = (int)r.xMin; x < (int)r.xMax; x++)
            {
                if (px[y * file.width + x].a <= 20)
                    continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        Object.DestroyImmediate(file);

        if (maxX < minX)
            return;

        float contentW = maxX - minX + 1, contentH = maxY - minY + 1;
        float scale = fit / Mathf.Max(contentW, contentH);
        size = new Vector2(r.width * scale, r.height * scale);

        // Centro del contenido respecto al centro del rectangulo del sprite (el pivote es el centro).
        Vector2 contentCenter = new Vector2((minX + maxX + 1) * 0.5f - r.xMin, (minY + maxY + 1) * 0.5f - r.yMin);
        Vector2 rectCenter = new Vector2(r.width * 0.5f, r.height * 0.5f);
        offset = -(contentCenter - rectCenter) * scale;
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
        Sprite marcoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/marco.png");
        Sprite cafeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/boton cafe.png");
        Sprite buySprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/compra.png");
        Sprite closeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Iconos/Ayuda/UI_Cerrar.png");
        Sprite ribbonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/titulos.png");
        Sprite potion = ShopSubSprite("Assets/Sprites/Iconos/curacion.png", "curacion_0");   // sustituye a "vida" (que se conserva en el proyecto)
        Sprite antidote = ShopSubSprite("Assets/Sprites/Iconos/antidoto.png", "antidoto_1");
        Sprite crowns = ShopSubSprite("Assets/Sprites/Iconos/coronas.png", "coronas");
        TMP_FontAsset bold = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherBold_SDF.asset");
        TMP_FontAsset regular = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherRegular_SDF.asset");

        productIcons = new[] { potion, antidote };
        Color navyFill = new Color(0.090f, 0.122f, 0.169f, 1f);   // #171F2B

        // Velo que oscurece el mapa y bloquea los clics sobre sus nodos.
        if (veil != null)
        {
            Image veilImage = veil.GetComponent<Image>();
            veilImage.color = new Color(ShopNavy.r, ShopNavy.g, ShopNavy.b, 0.8f);
            veilImage.raycastTarget = true;
        }

        // Medidas de referencia (1920x1080). La ventana se centra visualmente: el marco visible deja
        // ~24 px arriba y ~31 px abajo, de ahi el desplazamiento de -3,5 px.
        const float WinW = 980f, WinH = 800f;   // ancho ampliado (880 -> 980) para repartir bien la franja superior
        const float VisTop = WinH * 0.5f - 24f;
        RectTransform window = ShopRect("VentanaTienda", parent, new Vector2(0f, -3.5f), new Vector2(WinW, WinH));
        shopWindow = window.gameObject;
        ShopImage(window, windowSprite, 4.4f, Color.white, true);

        // Marco decorativo (sprite "marco", 9-slice): el rectangulo incluye los margenes transparentes
        // del sprite (149 px a los lados y 268 px arriba+abajo en la textura), asi que 'vis' es el
        // tamano visible del marco.
        RectTransform Marco(Transform p, string name, Vector2 pos, Vector2 vis, float m, Color tint)
        {
            RectTransform r = ShopRect(name, p, pos, new Vector2(vis.x + 149f / m, vis.y + 268f / m));
            ShopImage(r, marcoSprite, m, tint, false);
            return r;
        }

        RectTransform Fill(Transform p, string name, Vector2 pos, Vector2 size, bool raycast)
        {
            RectTransform r = ShopRect(name, p, pos, size);
            Image img = r.gameObject.AddComponent<Image>();
            img.color = navyFill;
            img.raycastTarget = raycast;
            return r;
        }

        // Zona anclada al borde superior de la ventana. Cada elemento del encabezado vive dentro de su
        // propia zona (centrado en ella), de modo que ninguna posicion depende de la de otro elemento.
        RectTransform Zone(string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            RectTransform z = ShopRect(name, window, Vector2.zero, size);
            z.anchorMin = z.anchorMax = anchor;
            z.pivot = new Vector2(anchor.x, 1f);
            z.sizeDelta = size;
            z.anchoredPosition = pos;
            return z;
        }

        // Franja superior con tres zonas independientes:
        //  - ZonaTitulo (antes ZonaTodo): ancla superior-centro, centrada respecto al ancho total de la ventana.
        //  - ZonaCoronas: ancla superior-derecha, espacio reservado solo para el contador (con aire
        //    a ambos lados: ~48 px hasta TODO y ~48 px hasta la X).
        //  - ZonaCerrar: ancla en la esquina superior-derecha, con el area de clic de la X (72 x 72).
        // TODO y Coronas comparten el mismo centro vertical (zonas con la misma altura y posicion).
        const float HeadH = 54f, HeadZoneH = 64f;
        float headTop = VisTop - 34f;
        float headY = headTop - HeadH * 0.5f;
        float zoneY = -(24f + 34f + HeadH * 0.5f - HeadZoneH * 0.5f);   // borde superior de las zonas, medido desde el de la ventana
        RectTransform zoneTodo = Zone("ZonaTodo", new Vector2(0.5f, 1f), new Vector2(0f, zoneY), new Vector2(240f, HeadZoneH));
        // Coronas se desplaza 50 px a la derecha (de -132,5 a -82,5) para dejar libre el rollo derecho del estandarte.
        RectTransform zoneCoins = Zone("ZonaCoronas", new Vector2(1f, 1f), new Vector2(-82.5f, zoneY), new Vector2(220f, HeadZoneH));
        // Misma disposicion que la X del menu PAUSA: el centro de la X queda 10 px por dentro del marco visible
        // (29 px de margen lateral y 24 px superior de la ventana), de modo que sobresale ~14 px de la esquina.
        // La zona (72 x 72) centra la X de 48 x 48 y conserva un area de clic comoda.
        RectTransform zoneClose = Zone("ZonaCerrar", new Vector2(1f, 1f), new Vector2(-3f, 2f), new Vector2(72f, 72f));

        // Titulo decorativo TIENDA: estandarte "titulos" (no interactivo), centrado en el ancho de la ventana y
        // superpuesto al borde superior. La textura (2172 x 724) se dibuja a escala uniforme 0,1845 (lado visible
        // de 380 px) para no deformar los rollos de los extremos. Su contenido empieza 41 px (textura) bajo el
        // borde superior del sprite y el panel de texto queda centrado 217 px bajo ese borde.
        const float RibbonScale = 380f / 2060f;
        float ribbonTopOffset = 41f * RibbonScale;                 // margen transparente superior del sprite (7,6 px)
        // El contenido visible arranca 24 px por encima del marco visible de la ventana (= borde del rectangulo).
        zoneTodo.gameObject.name = "ZonaTitulo";
        zoneTodo.sizeDelta = new Vector2(2172f * RibbonScale, 724f * RibbonScale);
        // Elevado 30 px: el borde superior del panel queda a la altura de la parte inferior del panel de texto
        // del estandarte, que sobresale mas por encima del marco (sigue dentro del Canvas y sin recorte).
        const float RibbonRaise = 30f;
        zoneTodo.anchoredPosition = new Vector2(0f, ribbonTopOffset + RibbonRaise);
        Image ribbonImage = zoneTodo.gameObject.AddComponent<Image>();
        ribbonImage.sprite = ribbonSprite;
        ribbonImage.preserveAspect = true;
        ribbonImage.raycastTarget = false;
        TMP_Text ribbonLabel = ShopText(zoneTodo, "Titulo", new Vector2(0f, zoneTodo.sizeDelta.y * 0.5f - 217f * RibbonScale), new Vector2(240f, 46f), "TIENDA", 30f, ShopIvory, TextAlignmentOptions.Center, bold, 3f);
        ribbonLabel.textWrappingMode = TextWrappingModes.NoWrap;
        zoneTodo.SetAsLastSibling();

        Fill(zoneCoins, "FondoCoronas", Vector2.zero, new Vector2(196f, HeadH - 6f), false);
        RectTransform chip = Marco(zoneCoins, "Coronas", Vector2.zero, new Vector2(200f, HeadH), 9f, Color.white);
        ShopImage(ShopRect("Icono", chip, new Vector2(-62f, 0f), new Vector2(42f, 42f)), crowns, 0f, Color.white, false);
        shopCoinsText = ShopText(chip, "Cantidad", new Vector2(22f, 0f), new Vector2(110f, 44f), "", 32f, ShopIvory, TextAlignmentOptions.Center, bold);
        shopCoinsText.textWrappingMode = TextWrappingModes.NoWrap;

        // Tarjetas verticales de producto, una junto a la otra y con las mismas dimensiones.
        const float CardW = 340f, CardH = 346f, CardGap = 40f;
        float cardTop = headTop - HeadH - 12f;
        float cardY = cardTop - CardH * 0.5f;

        productRows = new Button[2];
        productRowImages = new Image[2];
        productNames = new TMP_Text[2];
        productDescriptions = new TMP_Text[2];
        productPrices = new TMP_Text[2];
        cardHoverFrame = new GameObject[2];
        cardHover = new ShopCardHover[2];
        cardSoldOut = new GameObject[2];
        Sprite hoverSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/hovertienda.png");
        cardPriceGroup = new GameObject[2];
        cardIcon = new Image[2];
        Sprite[] icons = { potion, antidote };

        for (int i = 0; i < 2; i++)
        {
            float cx = (i == 0 ? -1f : 1f) * (CardW + CardGap) * 0.5f;
            RectTransform card = Fill(window, "Tarjeta_Producto_" + i, new Vector2(cx, cardY), new Vector2(CardW, CardH), true);
            Button cardButton = card.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = card.GetComponent<Image>();
            cardButton.transition = Selectable.Transition.None;
            Navigation none = cardButton.navigation;
            none.mode = Navigation.Mode.None;
            cardButton.navigation = none;
            productRows[i] = cardButton;

            // Marco normal (en reposo) y marco "hovertienda" (cursor encima o tarjeta seleccionada). El segundo
            // es un 9-slice a 1/4,5 que mide lo mismo que la tarjeta: sus esquinas no se deforman. El rectangulo
            // incluye los margenes transparentes del sprite (133 px a los lados y 244 px arriba+abajo).
            RectTransform border = Marco(card, "Marco", Vector2.zero, new Vector2(CardW, CardH), 8f, Color.white);
            productRowImages[i] = border.GetComponent<Image>();
            RectTransform hoverFrame = ShopRect("MarcoHover", card, Vector2.zero, new Vector2(CardW + 133f / 4.5f, CardH + 244f / 4.5f));
            ShopImage(hoverFrame, hoverSprite, 4.5f, Color.white, false);
            hoverFrame.gameObject.SetActive(false);
            cardHoverFrame[i] = hoverFrame.gameObject;
            cardHover[i] = card.gameObject.AddComponent<ShopCardHover>();

            // Los dos iconos se ajustan por su contenido visible (no por el rectangulo del sprite, que tiene
            // margenes distintos): misma altura visual (128 px) y centro del contenido en el mismo punto.
            ShopIconPlacement(icons[i], 128f, out Vector2 iconSize, out Vector2 iconOffset);
            cardIcon[i] = ShopImage(ShopRect("Icono", card, new Vector2(0f, 72f) + iconOffset, iconSize), icons[i], 0f, Color.white, false);

            // Nombre dentro del marco "boton cafe" (9-slice, proporcion original ~3,5:1).
            RectTransform nameFrame = ShopRect("MarcoNombre", card, new Vector2(0f, -52f), new Vector2(318f, 132f));
            ShopImage(nameFrame, cafeSprite, 6.5f, Color.white, false);
            productNames[i] = ShopText(nameFrame, "Nombre", Vector2.zero, new Vector2(250f, 40f), "", 25f, ShopIvory, TextAlignmentOptions.Center, bold);
            productNames[i].textWrappingMode = TextWrappingModes.NoWrap;

            // Icono de Coronas y precio debajo (agrupados: se ocultan si el producto esta agotado).
            RectTransform priceGroup = ShopRect("Precio", card, new Vector2(0f, -128f), new Vector2(CardW - 40f, 52f));
            ShopImage(ShopRect("Corona", priceGroup, new Vector2(-46f, 0f), new Vector2(44f, 44f)), crowns, 0f, Color.white, false);
            // Midline: centra por la linea de texto (no por los glifos), asi "35" y "20" quedan en la misma
            // linea aunque las cifras de Georgia tengan alturas distintas.
            productPrices[i] = ShopText(priceGroup, "Cantidad", new Vector2(26f, 0f), new Vector2(110f, 46f), "", 36f, ShopGold, TextAlignmentOptions.Midline, bold);
            productPrices[i].textWrappingMode = TextWrappingModes.NoWrap;
            cardPriceGroup[i] = priceGroup.gameObject;

            // AGOTADO ocupa la zona del precio (visible solo sin existencias); el icono queda atenuado.
            TMP_Text soldOutLabel = ShopText(card, "Agotado", new Vector2(0f, -128f), new Vector2(CardW - 40f, 52f), "AGOTADO", 32f, ShopGold, TextAlignmentOptions.Center, bold, 4f);
            soldOutLabel.textWrappingMode = TextWrappingModes.NoWrap;
            cardSoldOut[i] = soldOutLabel.gameObject;
            soldOutLabel.gameObject.SetActive(false);
        }

        // Mensaje de compra.
        float messageY = cardTop - CardH - 12f - 14f;
        shopMessageText = ShopText(window, "Mensaje", new Vector2(0f, messageY), new Vector2(2f * 380f, 28f), "", 22f, ShopGold, TextAlignmentOptions.Center, regular);
        shopMessageText.fontStyle = FontStyles.Italic;
        shopMessageText.textWrappingMode = TextWrappingModes.NoWrap;

        // Panel inferior de detalles: icono ampliado | nombre y descripcion | existencias, inventario y COMPRAR.
        float detailTop = messageY - 14f - 8f;
        const float DetailH = 232f, DetailPad = 36f;
        float detailY = detailTop - DetailH * 0.5f;
        RectTransform detail = ShopRect("PanelDetalle", window, new Vector2(0f, detailY), new Vector2(760f, DetailH));
        ShopImage(detail, windowSprite, 7f, Color.white, false);
        float innerTop = detailTop - DetailPad;
        float innerBottom = detailTop - DetailH + DetailPad;

        Fill(window, "FondoIconoDetalle", new Vector2(-284f, detailY), new Vector2(108f, 108f), false);
        Marco(window, "MarcoIconoDetalle", new Vector2(-284f, detailY), new Vector2(112f, 112f), 8f, Color.white);
        // Icono del panel inferior: cada producto tiene su tamano y desplazamiento (contenido visible de 84 px,
        // centrado en el marco); MapShopController los aplica al cambiar de producto.
        detailIconBasePos = new Vector2(-284f, detailY);
        detailIconSizes = new Vector2[2];
        detailIconOffsets = new Vector2[2];
        for (int d = 0; d < 2; d++)
            ShopIconPlacement(icons[d], 84f, out detailIconSizes[d], out detailIconOffsets[d]);

        detailIcon = ShopImage(ShopRect("IconoDetalle", window, detailIconBasePos + detailIconOffsets[0], detailIconSizes[0]), potion, 0f, Color.white, false);

        // Zona central mas ancha (308 px) y con aire entre nombre y descripcion; interlineado ampliado.
        // Texto: 24 px tras el icono y 20 px antes del divisor (zona de 302 px).
        const float CenterX = -53f, CenterW = 302f;
        detailName = ShopText(window, "NombreDetalle", new Vector2(CenterX, innerTop - 20f), new Vector2(CenterW, 40f), "", 28f, ShopIvory, TextAlignmentOptions.TopLeft, bold);
        detailName.textWrappingMode = TextWrappingModes.NoWrap;
        detailDescription = ShopText(window, "DescripcionDetalle", new Vector2(CenterX, innerTop - 52f - 46f), new Vector2(CenterW, 92f), "", 20f, ShopIvory, TextAlignmentOptions.TopLeft, regular);
        detailDescription.lineSpacing = 12f;

        // Divisor vertical entre la descripcion y las existencias (como en la referencia).
        RectTransform divider = ShopRect("Divisor", window, new Vector2(118f, detailY), new Vector2(2f, DetailH - 2f * DetailPad - 8f));
        ShopImage(divider, null, 0f, new Color(ShopGold.r, ShopGold.g, ShopGold.b, 0.45f), false);

        // Columna derecha: tres elementos con 10 px de separacion, todos centrados en RightX.
        const float RightX = 240f, RightW = 200f;
        detailAvailable = ShopText(window, "Disponibles", new Vector2(RightX, innerTop - 28f), new Vector2(RightW, 30f), "", 23f, ShopGold, TextAlignmentOptions.Center, bold);
        detailAvailable.textWrappingMode = TextWrappingModes.NoWrap;
        detailOwned = ShopText(window, "EnInventario", new Vector2(RightX, innerTop - 50f), new Vector2(RightW, 30f), "", 21f, ShopIvory, TextAlignmentOptions.Center, regular);
        detailOwned.textWrappingMode = TextWrappingModes.NoWrap;
        detailOwned.gameObject.SetActive(false);   // "En inventario" ya no se muestra: el boton COMPRAR queda centrado bajo las existencias

        // Boton COMPRAR con el sprite "compra" (proporcion original: 246 x 102 a escala 1/7,9).
        RectTransform buy = ShopRect("BotonComprar", window, new Vector2(RightX, innerTop - 80f), new Vector2(224f, 93f));
        Image buyImage = ShopImage(buy, buySprite, 0f, Color.white, true);
        buyImage.preserveAspect = false;
        buyImage.raycastPadding = new Vector4(11f, 16f, 11f, 15f);
        detailBuyButton = buy.gameObject.AddComponent<Button>();
        detailBuyButton.targetGraphic = buyImage;
        detailBuyButton.transition = Selectable.Transition.ColorTint;
        ColorBlock buyColors = detailBuyButton.colors;
        buyColors.normalColor = new Color(0.94f, 0.94f, 0.94f, 1f);
        buyColors.highlightedColor = Color.white;
        buyColors.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
        buyColors.selectedColor = buyColors.normalColor;
        buyColors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);
        buyColors.colorMultiplier = 1f;
        buyColors.fadeDuration = 0.08f;
        detailBuyButton.colors = buyColors;
        Navigation buyNav = detailBuyButton.navigation;
        buyNav.mode = Navigation.Mode.None;
        detailBuyButton.navigation = buyNav;
        TMP_Text buyLabel = ShopText(buy, "Texto", Vector2.zero, new Vector2(190f, 44f), "COMPRAR", 28f, ShopNavy, TextAlignmentOptions.Center, bold, 2f);
        buyLabel.textWrappingMode = TextWrappingModes.NoWrap;

        // Boton X (UI_Cerrar) de 48 x 48, dentro del marco (a ~10 px del borde) y con el area de clic
        // ampliada 12 px por lado (72 x 72). No tapa el contador de Coronas.
        // La X (48 x 48) ocupa el centro de ZonaCerrar (72 x 72, ancla en la esquina superior-derecha), igual
        // que en PAUSA: ligeramente superpuesta a la esquina y lejos del contador de Coronas.
        RectTransform close = ShopRect("BotonCerrar", zoneClose, Vector2.zero, new Vector2(48f, 48f));
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

    // Construye "VentanaInventario" (solo consulta) dentro del mismo Canvas: estandarte INVENTARIO, cuadricula de
    // 4 x 3 casillas cuadradas a la izquierda, panel de detalle a la derecha y X de cierre. Reutiliza los sprites
    // y las medidas de la tienda; no usa el boceto como imagen. Requiere que la tienda ya se haya construido
    // (productIcons y el velo).
    public void BuildInventoryWindowInEditor()
    {
        Transform parent = canvas.transform;
        Transform old = parent.Find("VentanaInventario");

        if (old != null)
            DestroyImmediate(old.gameObject);

        Sprite windowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/opciones 1.png");
        Sprite marcoSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/marco.png");
        Sprite cafeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/boton cafe.png");
        Sprite hoverSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/hovertienda.png");
        Sprite closeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Iconos/Ayuda/UI_Cerrar.png");
        Sprite ribbonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/titulos.png");
        Sprite potion = ShopSubSprite("Assets/Sprites/Iconos/curacion.png", "curacion_0");
        Sprite antidote = ShopSubSprite("Assets/Sprites/Iconos/antidoto.png", "antidoto_1");
        TMP_FontAsset bold = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherBold_SDF.asset");
        TMP_FontAsset regular = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherRegular_SDF.asset");
        Sprite[] icons = { potion, antidote };
        productIcons = icons;
        Color navyFill = new Color(0.063f, 0.082f, 0.118f, 1f);   // #10151E (fondo oscuro de las casillas)

        if (veil != null)
        {
            Image veilImage = veil.GetComponent<Image>();
            veilImage.color = new Color(ShopNavy.r, ShopNavy.g, ShopNavy.b, 0.8f);
            veilImage.raycastTarget = true;
        }

        // Ventana (mismo sprite y escala que la tienda). El marco visible deja 29 px laterales, 24 arriba y 31 abajo.
        const float WinW = 1100f, WinH = 610f;
        const float VisTop = WinH * 0.5f - 24f;
        RectTransform window = ShopRect("VentanaInventario", parent, new Vector2(0f, -3.5f), new Vector2(WinW, WinH));
        inventoryWindow = window.gameObject;
        ShopImage(window, windowSprite, 4.4f, Color.white, true);

        RectTransform Marco(Transform p, string name, Vector2 pos, Vector2 vis, float m, Color tint)
        {
            RectTransform r = ShopRect(name, p, pos, new Vector2(vis.x + 149f / m, vis.y + 268f / m));
            ShopImage(r, marcoSprite, m, tint, false);
            return r;
        }

        RectTransform Fill(Transform p, string name, Vector2 pos, Vector2 size, bool raycast)
        {
            RectTransform r = ShopRect(name, p, pos, size);
            Image img = r.gameObject.AddComponent<Image>();
            img.color = navyFill;
            img.raycastTarget = raycast;
            return r;
        }

        RectTransform Zone(string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            RectTransform z = ShopRect(name, window, Vector2.zero, size);
            z.anchorMin = z.anchorMax = anchor;
            z.pivot = new Vector2(anchor.x, 1f);
            z.sizeDelta = size;
            z.anchoredPosition = pos;
            return z;
        }

        // Paneles: cuadricula a la izquierda y detalle a la derecha, con la misma altura.
        const float SlotSize = 124f, SlotGap = 14f, GridPad = 26f, PanelGap = 24f, SideMargin = 34f;
        float gridW = 4f * SlotSize + 3f * SlotGap, gridH = 3f * SlotSize + 2f * SlotGap;
        float leftW = gridW + 2f * GridPad, panelH = gridH + 2f * GridPad;
        float innerW = (WinW - 58f) - 2f * SideMargin;       // ancho visible menos los margenes laterales
        float rightW = innerW - leftW - PanelGap;
        float panelTop = VisTop - 70f;                         // por debajo de los rollos del estandarte
        float panelCenterY = panelTop - panelH * 0.5f;
        float leftX = -(WinW - 58f) * 0.5f + SideMargin + leftW * 0.5f;
        float rightX = leftX + leftW * 0.5f + PanelGap + rightW * 0.5f;

        // Panel de la cuadricula sin marco dorado (la imagen se crea desactivada; el rectangulo conserva su tamano).
        ShopImage(ShopRect("PanelCuadricula", window, new Vector2(leftX, panelCenterY), new Vector2(leftW, panelH)), windowSprite, 7f, Color.white, false).enabled = false;
        ShopImage(ShopRect("PanelDetalle", window, new Vector2(rightX, panelCenterY), new Vector2(rightW, panelH)), windowSprite, 7f, Color.white, false);

        // Cuadricula 4 x 3: casillas cuadradas e iguales; marco dorado discreto y seleccion con "hovertienda".
        invSlotButtons = new Button[InventorySlots];
        invSlotIcons = new Image[InventorySlots];
        invSlotQuantities = new TMP_Text[InventorySlots];
        invSlotSelection = new GameObject[InventorySlots];
        invSlotIconSizes = new Vector2[2];
        invSlotIconOffsets = new Vector2[2];
        invDetailIconSizes = new Vector2[2];
        invDetailIconOffsets = new Vector2[2];

        for (int t = 0; t < 2; t++)
        {
            ShopIconPlacement(icons[t], 84f, out invSlotIconSizes[t], out invSlotIconOffsets[t]);
            ShopIconPlacement(icons[t], 130f, out invDetailIconSizes[t], out invDetailIconOffsets[t]);
        }

        float gridLeft = leftX - gridW * 0.5f, gridTop = panelCenterY + gridH * 0.5f;

        for (int s = 0; s < InventorySlots; s++)
        {
            int col = s % 4, row = s / 4;
            Vector2 center = new Vector2(gridLeft + SlotSize * 0.5f + col * (SlotSize + SlotGap), gridTop - SlotSize * 0.5f - row * (SlotSize + SlotGap));
            RectTransform slot = Fill(window, "Casilla_" + s, center, new Vector2(SlotSize, SlotSize), true);
            Button slotButton = slot.gameObject.AddComponent<Button>();
            slotButton.targetGraphic = slot.GetComponent<Image>();
            slotButton.transition = Selectable.Transition.None;
            Navigation none = slotButton.navigation;
            none.mode = Navigation.Mode.None;
            slotButton.navigation = none;
            invSlotButtons[s] = slotButton;

            Marco(slot, "Marco", Vector2.zero, new Vector2(SlotSize, SlotSize), 12f, new Color(0.62f, 0.52f, 0.40f, 1f));

            RectTransform selection = ShopRect("Seleccion", slot, Vector2.zero, new Vector2(SlotSize + 133f / 6f, SlotSize + 244f / 6f));
            ShopImage(selection, hoverSprite, 6f, Color.white, false);
            selection.gameObject.SetActive(false);
            invSlotSelection[s] = selection.gameObject;

            Image icon = ShopImage(ShopRect("Icono", slot, Vector2.zero, new Vector2(84f, 84f)), potion, 0f, Color.white, false);
            icon.enabled = false;
            invSlotIcons[s] = icon;

            TMP_Text qty = ShopText(slot, "Cantidad", new Vector2(SlotSize * 0.5f - 24f, -SlotSize * 0.5f + 20f), new Vector2(44f, 32f), "", 26f, ShopIvory, TextAlignmentOptions.Center, bold);
            qty.textWrappingMode = TextWrappingModes.NoWrap;
            invSlotQuantities[s] = qty;
        }

        // Mensaje discreto cuando no hay objetos.
        TMP_Text empty = ShopText(window, "InventarioVacio", new Vector2(leftX, panelCenterY), new Vector2(leftW - 80f, 40f), "Inventario vacío", 26f, ShopIvory, TextAlignmentOptions.Center, regular);
        empty.fontStyle = FontStyles.Italic;
        empty.textWrappingMode = TextWrappingModes.NoWrap;
        empty.color = new Color(ShopIvory.r, ShopIvory.g, ShopIvory.b, 0.7f);
        invEmptyMessage = empty.gameObject;
        invEmptyMessage.SetActive(false);

        // Detalle (vertical): icono ampliado, nombre en el marco cafe, descripcion y cantidad X/5.
        RectTransform detail = ShopRect("Contenido", window, new Vector2(rightX, panelCenterY), new Vector2(rightW, panelH));
        invDetailContent = detail.gameObject;
        // Distribucion vertical (origen en el centro del panel de detalle, alto 452): margen 24, icono 130, hueco 18,
        // marco del nombre (visible ~66), hueco 16, descripcion 112, hueco 16, cantidad 46 y margen 24.
        float top = panelH * 0.5f;
        invDetailIconBase = new Vector2(0f, top - 24f - 65f);
        invDetailIcon = ShopImage(ShopRect("Icono", detail, invDetailIconBase + invDetailIconOffsets[0], invDetailIconSizes[0]), potion, 0f, Color.white, false);

        RectTransform nameFrame = ShopRect("MarcoNombre", detail, new Vector2(0f, top - 24f - 130f - 18f - 33f), new Vector2(rightW - 40f, 128f));
        ShopImage(nameFrame, cafeSprite, 6.5f, Color.white, false);
        invDetailName = ShopText(nameFrame, "Nombre", Vector2.zero, new Vector2(rightW - 100f, 40f), "", 25f, ShopIvory, TextAlignmentOptions.Center, bold);
        invDetailName.textWrappingMode = TextWrappingModes.NoWrap;

        float descCenter = top - 24f - 130f - 18f - 66f - 16f - 56f;
        RectTransform descBox = ShopRect("MarcoDescripcion", detail, new Vector2(0f, descCenter), new Vector2(rightW - 40f, 112f));
        ShopImage(descBox, windowSprite, 10f, Color.white, false);
        invDetailDescription = ShopText(detail, "Descripcion", new Vector2(0f, descCenter), new Vector2(rightW - 90f, 92f), "", 20f, ShopIvory, TextAlignmentOptions.Center, regular);
        invDetailDescription.lineSpacing = 8f;

        float qtyCenter = descCenter - 56f - 16f - 23f;
        Fill(detail, "FondoCantidad", new Vector2(0f, qtyCenter), new Vector2(rightW - 48f, 40f), false);
        Marco(detail, "MarcoCantidad", new Vector2(0f, qtyCenter), new Vector2(rightW - 40f, 46f), 9f, Color.white);
        TMP_Text qtyLabel = ShopText(detail, "EtiquetaCantidad", new Vector2(-(rightW - 40f) * 0.5f + 94f, qtyCenter), new Vector2(130f, 36f), "Cantidad:", 22f, ShopIvory, TextAlignmentOptions.Left, regular);
        qtyLabel.textWrappingMode = TextWrappingModes.NoWrap;
        invDetailQuantity = ShopText(detail, "Cantidad", new Vector2((rightW - 40f) * 0.5f - 76f, qtyCenter), new Vector2(90f, 36f), "", 26f, ShopGold, TextAlignmentOptions.Right, bold);
        invDetailQuantity.textWrappingMode = TextWrappingModes.NoWrap;

        // Estandarte INVENTARIO (decorativo, centrado y superpuesto al borde superior como en la tienda).
        const float RibbonScale = 440f / 2060f;
        RectTransform ribbon = Zone("ZonaTitulo", new Vector2(0.5f, 1f), new Vector2(0f, 48f), new Vector2(2172f * RibbonScale, 724f * RibbonScale));
        Image ribbonImage = ribbon.gameObject.AddComponent<Image>();
        ribbonImage.sprite = ribbonSprite;
        ribbonImage.preserveAspect = true;
        ribbonImage.raycastTarget = false;
        TMP_Text ribbonLabel = ShopText(ribbon, "Titulo", new Vector2(0f, ribbon.sizeDelta.y * 0.5f - 217f * RibbonScale), new Vector2(290f, 50f), "INVENTARIO", 28f, ShopIvory, TextAlignmentOptions.Center, bold, 2f);
        ribbonLabel.textWrappingMode = TextWrappingModes.NoWrap;

        // X (UI_Cerrar): misma posicion visual que en PAUSA y en la tienda (zona de clic de 72 x 72).
        RectTransform zoneClose = Zone("ZonaCerrar", new Vector2(1f, 1f), new Vector2(-3f, 2f), new Vector2(72f, 72f));
        RectTransform close = ShopRect("BotonCerrar", zoneClose, Vector2.zero, new Vector2(48f, 48f));
        Image closeImage = ShopImage(close, closeSprite, 0f, Color.white, true);
        inventoryCloseButton = close.gameObject.AddComponent<Button>();
        inventoryCloseButton.targetGraphic = closeImage;
        inventoryCloseButton.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = inventoryCloseButton.colors;
        colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = colors.normalColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        inventoryCloseButton.colors = colors;
        Navigation closeNav = inventoryCloseButton.navigation;
        closeNav.mode = Navigation.Mode.None;
        inventoryCloseButton.navigation = closeNav;

        ribbon.SetAsLastSibling();
        inventoryWindow.SetActive(false);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    // Rehace solo el aspecto del panel de detalle del inventario (marco fino, icono, nombre, descripcion y
    // cantidad) sin tocar la cuadricula, el estandarte ni la X. Los datos se siguen refrescando igual.
    public void RebuildInventoryDetailInEditor()
    {
        Transform window = inventoryWindow.transform;
        Transform panel = window.Find("PanelDetalle");
        Transform oldContent = window.Find("Contenido");
        int contentIndex = oldContent.GetSiblingIndex();
        RectTransform panelRect = (RectTransform)panel;
        float rightW = panelRect.sizeDelta.x, panelH = panelRect.sizeDelta.y;
        Vector2 center = panelRect.anchoredPosition;

        Sprite potion = ShopSubSprite("Assets/Sprites/Iconos/curacion.png", "curacion_0");
        Sprite antidote = ShopSubSprite("Assets/Sprites/Iconos/antidoto.png", "antidoto_1");
        TMP_FontAsset bold = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherBold_SDF.asset");
        TMP_FontAsset regular = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherRegular_SDF.asset");
        Sprite[] icons = { potion, antidote };

        Color mainFill = new Color(0.063f, 0.082f, 0.118f, 1f);       // #10151E
        Color boxFill = new Color(0.090f, 0.122f, 0.169f, 1f);        // #171F2B
        Color boxLine = new Color(0.239f, 0.290f, 0.361f, 1f);        // azul grisaceo discreto
        Color brownFill = new Color(0.247f, 0.157f, 0.098f, 1f);      // cafe oscuro

        Image PlainRect(Transform p, string name, Vector2 pos, Vector2 size, Color color)
        {
            RectTransform r = ShopRect(name, p, pos, size);
            Image img = r.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // Caja con borde de 'line' px: el borde es la propia imagen y el relleno un hijo inset.
        RectTransform ThinBox(Transform p, string name, Vector2 pos, Vector2 size, Color fill, Color border, float line)
        {
            Image outer = PlainRect(p, name, pos, size, border);
            Image inner = PlainRect(outer.transform, "Relleno", Vector2.zero, size - new Vector2(2f * line, 2f * line), fill);
            return (RectTransform)outer.transform;
        }

        // Panel principal: marco dorado fino solo en el contorno exterior.
        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = null;
        panelImage.type = Image.Type.Simple;
        panelImage.color = ShopGold;
        panelImage.raycastTarget = false;

        for (int i = panel.childCount - 1; i >= 0; i--)
            DestroyImmediate(panel.GetChild(i).gameObject);

        PlainRect(panel, "Relleno", Vector2.zero, panelRect.sizeDelta - new Vector2(4f, 4f), mainFill);

        DestroyImmediate(oldContent.gameObject);
        RectTransform detail = ShopRect("Contenido", window, center, panelRect.sizeDelta);
        detail.SetSiblingIndex(contentIndex);
        invDetailContent = detail.gameObject;

        float top = panelH * 0.5f;
        float boxW = rightW - 48f;

        // Icono centrado arriba (contenido visible de 140 px).
        invDetailIconSizes = new Vector2[2];
        invDetailIconOffsets = new Vector2[2];

        for (int t = 0; t < 2; t++)
            ShopIconPlacement(icons[t], 140f, out invDetailIconSizes[t], out invDetailIconOffsets[t]);

        invDetailIconBase = new Vector2(0f, top - 22f - 70f);
        invDetailIcon = ShopImage(ShopRect("Icono", detail, invDetailIconBase + invDetailIconOffsets[0], invDetailIconSizes[0]), potion, 0f, Color.white, false);

        // Nombre: panel cafe oscuro con borde dorado fino.
        RectTransform nameBox = ThinBox(detail, "MarcoNombre", new Vector2(0f, 24f), new Vector2(boxW, 48f), brownFill, ShopGold, 1.5f);
        invDetailName = ShopText(nameBox, "Nombre", Vector2.zero, new Vector2(boxW - 24f, 40f), "", 23f, ShopIvory, TextAlignmentOptions.Center, bold);
        invDetailName.textWrappingMode = TextWrappingModes.NoWrap;

        // Descripcion: panel azul oscuro distinto del fondo, borde azul grisaceo.
        RectTransform descBox = ThinBox(detail, "MarcoDescripcion", new Vector2(0f, -76f), new Vector2(boxW, 120f), boxFill, boxLine, 1.5f);
        invDetailDescription = ShopText(descBox, "Descripcion", Vector2.zero, new Vector2(boxW - 40f, 104f), "", 20f, ShopIvory, TextAlignmentOptions.Center, regular);
        invDetailDescription.lineSpacing = 10f;

        // Cantidad: etiqueta a la izquierda y numero a la derecha.
        RectTransform qtyBox = ThinBox(detail, "MarcoCantidad", new Vector2(0f, -176f), new Vector2(boxW, 48f), boxFill, boxLine, 1.5f);
        TMP_Text qtyLabel = ShopText(qtyBox, "EtiquetaCantidad", new Vector2(-boxW * 0.5f + 20f + 70f, 0f), new Vector2(140f, 36f), "Cantidad:", 22f, ShopIvory, TextAlignmentOptions.Left, regular);
        qtyLabel.textWrappingMode = TextWrappingModes.NoWrap;
        invDetailQuantity = ShopText(qtyBox, "Cantidad", new Vector2(boxW * 0.5f - 20f - 45f, 0f), new Vector2(90f, 36f), "", 26f, ShopIvory, TextAlignmentOptions.Right, bold);
        invDetailQuantity.textWrappingMode = TextWrappingModes.NoWrap;

        FlattenLayeredBorders(window);   // bordes sin capas superpuestas (evita el brillo amarillo al aparecer)
        UnityEditor.EditorUtility.SetDirty(this);
    }

    // Aligera el aspecto de la ventana de tienda (mismo estilo que el detalle del inventario): bordes finos en
    // las tarjetas, nombre en panel cafe sencillo y panel inferior con divisiones azul grisaceas. No cambia
    // dimensiones, posiciones, textos ni logica; solo sustituye los marcos gruesos por rectangulos UI.
    public void RestyleShopWindowInEditor()
    {
        Transform window = shopWindow.transform;
        Color line = new Color(0.239f, 0.290f, 0.361f, 1f);          // azul grisaceo
        Color agedGold = new Color(0.62f, 0.52f, 0.34f, 1f);         // dorado envejecido
        Color brown = new Color(0.247f, 0.157f, 0.098f, 1f);
        Color cardFill = new Color(0.075f, 0.098f, 0.141f, 1f);

        // Convierte un Image existente en caja de borde fino: la imagen es el borde y "Relleno" el interior.
        void Thin(Image img, Vector2 size, Color border, Color fill, float width)
        {
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = border;
            img.raycastTarget = false;
            img.enabled = true;
            img.rectTransform.sizeDelta = size;

            Transform old = img.transform.Find("Relleno");
            if (old != null)
                DestroyImmediate(old.gameObject);

            RectTransform inner = ShopRect("Relleno", img.transform, Vector2.zero, size - new Vector2(2f * width, 2f * width));
            Image fillImage = inner.gameObject.AddComponent<Image>();
            fillImage.color = fill;
            fillImage.raycastTarget = false;
            inner.SetAsFirstSibling();
        }

        for (int i = 0; i < 2; i++)
        {
            Transform card = window.Find("Tarjeta_Producto_" + i);
            Vector2 cardSize = ((RectTransform)card).sizeDelta;

            Thin(productRowImages[i], cardSize, line, cardFill, 1.5f);

            Transform hover = card.Find("MarcoHover");
            if (hover != null)
                DestroyImmediate(hover.gameObject);
            cardHoverFrame[i] = null;

            Image nameFrame = card.Find("MarcoNombre").GetComponent<Image>();
            Thin(nameFrame, new Vector2(304f, 52f), agedGold, brown, 1.5f);
            nameFrame.rectTransform.anchoredPosition = new Vector2(0f, -52f);
        }

        Image panelImage = window.Find("PanelDetalle").GetComponent<Image>();
        Thin(panelImage, panelImage.rectTransform.sizeDelta, line, new Color(0.075f, 0.098f, 0.141f, 1f), 1.5f);

        Transform iconBack = window.Find("FondoIconoDetalle");
        if (iconBack != null)
            DestroyImmediate(iconBack.gameObject);

        Image iconFrame = window.Find("MarcoIconoDetalle").GetComponent<Image>();
        Thin(iconFrame, new Vector2(112f, 112f), line, cardFill, 1.5f);

        Image divider = window.Find("Divisor").GetComponent<Image>();
        divider.color = line;

        FlattenLayeredBorders(window);   // bordes sin capas superpuestas (evita el brillo amarillo al aparecer)

        UnityEditor.EditorUtility.SetDirty(this);
    }

    // Los recuadros con borde fino estaban hechos con dos imagenes superpuestas (el borde a tamano completo y el
    // relleno encima). Con el fundido de apertura el borde asomaba por debajo del relleno y la ventana se veia
    // "iluminada" de amarillo. Esto convierte cada recuadro: la imagen pasa a ser el relleno y el borde son cuatro
    // franjas finas sin solapar el interior.
    public void FlattenBordersInEditor()
    {
        int converted = 0;

        if (shopWindow != null)
            converted += FlattenLayeredBorders(shopWindow.transform);

        if (inventoryWindow != null)
            converted += FlattenLayeredBorders(inventoryWindow.transform);

        Debug.Log("Bordes planos: " + converted + " recuadros convertidos.");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private static int FlattenLayeredBorders(Transform root)
    {
        int count = 0;

        foreach (Image outer in root.GetComponentsInChildren<Image>(true))
        {
            // Los rellenos ya convertidos se destruyen durante el recorrido.
            if (outer == null)
                continue;

            Transform fillTransform = outer.transform.Find("Relleno");

            if (fillTransform == null)
                continue;

            Image fill = fillTransform.GetComponent<Image>();

            if (fill == null)
                continue;

            RectTransform outerRect = outer.rectTransform;
            RectTransform fillRect = fill.rectTransform;
            float width = Mathf.Max(1f, (outerRect.rect.width - fillRect.rect.width) * 0.5f);
            Color borderColor = outer.color;

            outer.color = fill.color;
            DestroyImmediate(fillTransform.gameObject);

            string[] names = { "Borde_Sup", "Borde_Inf", "Borde_Izq", "Borde_Der" };

            for (int i = 0; i < 4; i++)
            {
                Transform old = outer.transform.Find(names[i]);

                if (old != null)
                    DestroyImmediate(old.gameObject);

                RectTransform strip = ShopRect(names[i], outer.transform, Vector2.zero, Vector2.zero);
                bool horizontal = i < 2;
                strip.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0f);
                strip.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 1f);
                strip.pivot = horizontal ? new Vector2(0.5f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0.5f);
                strip.anchoredPosition = Vector2.zero;
                strip.sizeDelta = horizontal ? new Vector2(0f, width) : new Vector2(width, -2f * width);
                Image stripImage = strip.gameObject.AddComponent<Image>();
                stripImage.color = borderColor;
                stripImage.raycastTarget = false;
            }

            count++;
        }

        return count;
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

        font = GameFonts.Regular;

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
