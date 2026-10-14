using UnityEngine;
using UnityEngine.EventSystems;
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

    // Abre el panel: tienda (compra y uso) o inventario (solo uso).
    public void Open(bool asShop)
    {
        if (!CanOpen())
            return;

        isOpen = true;
        shopMode = asShop;
        message = "";
    }

    public void Close()
    {
        isOpen = false;
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

        if (panel.activeSelf != open)
            panel.SetActive(open);

        if (veil != null && veil.activeSelf != open)
            veil.SetActive(open);

        if (!open)
            return;

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

    // ------------------------------------------------------------
    // Construccion de la interfaz provisional
    // ------------------------------------------------------------

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
