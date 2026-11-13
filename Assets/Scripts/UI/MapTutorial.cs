using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Tutorial interactivo del mapa que se juega justo despues del primer combate (Un mal dia para cobrar). Ensena, usando
// las ventanas reales del mapa: la Tienda (comprar y vender), el Inventario y las Reliquias (equipar desde la ficha).
// Oscurece la pantalla dejando iluminada la zona que se explica:
//  - pasos "Explicar": el resto queda bloqueado y se avanza con ESPACIO;
//  - pasos "Actuar": solo la zona iluminada responde a clics y el paso termina cuando el jugador hace lo pedido.
// ENTER salta el tutorial en cualquier momento. Solo se activa una vez por partida (GameSession.MapTutorialPending) y no
// cambia reglas: compra, venta y equipamiento usan los mismos servicios que el juego normal.
public class MapTutorial : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset regularFont;
    [SerializeField] private TMP_FontAsset boldFont;
    [SerializeField] private int sortingOrder = 900;
    [Header("Boton Omitir (sprites del menu)")]
    [SerializeField] private Sprite skipNormal;
    [SerializeField] private Sprite skipHover;

    private static readonly Color Dim = new Color(0.02f, 0.03f, 0.06f, 0.74f);
    private static readonly Color Gold = new Color(0.96f, 0.80f, 0.40f, 1f);
    private static readonly Color Ivory = new Color(0.93f, 0.90f, 0.82f, 1f);
    private static readonly Color Navy = new Color(0.07f, 0.10f, 0.16f, 1f);

    private MapShopController shop;
    private CharacterPanelController panel;

    private RectTransform root;
    private Canvas uiCanvas;
    private readonly Image[] dims = new Image[4];
    private readonly Image[] frame = new Image[4];
    private Image holeBlocker;
    private RectTransform card;
    private TMP_Text cardText;
    private TMP_Text cardHint;

    private Func<Rect?> currentArea;
    private bool showHole;
    private bool blockHole;
    private bool skipped;

    private void Start()
    {
        if (!GameSession.ConsumeMapTutorial())
        {
            enabled = false;
            return;
        }

        shop = FindAnyObjectByType<MapShopController>(FindObjectsInactive.Include);
        panel = FindAnyObjectByType<CharacterPanelController>(FindObjectsInactive.Include);

        if (shop == null || panel == null)
        {
            enabled = false;
            return;
        }

        BuildUi();
        StartCoroutine(Run());
    }

    // ---------------- Guion ----------------

    private IEnumerator Run()
    {
        // El mapa aparece desde negro tras la victoria: se espera a que se asiente.
        yield return new WaitForSecondsRealtime(1.6f);

        while (DialogueController.IsPlaying || shop.IsOpen || panel.IsOpen)
            yield return null;

        // Las compras de practica deben ser posibles aunque el jugador haya gastado las Coronas.
        int needed = ConsumableService.PotionPrice - GameSession.Coins;

        if (needed > 0)
            GameSession.AddCoins(needed);

        yield return Explain(null,
            "¡Victoria! Antes de seguir, practiquemos con la Tienda, el Inventario y las Reliquias. Usarás las ventanas reales del mapa.");

        // ---- Tienda: comprar ----
        yield return Act(() => Of(ShortcutButton("BotonTienda")),
            "Haz clic en el botón Tienda para abrirla.",
            () => shop.IsOpen && shop.ShopMode);

        yield return Explain(() => Of(shop.TutorialRect("tienda")),
            "Esta es la Tienda. Arriba ves tus Coronas y las pestañas Todo, Reliquias y Consumibles. Las reliquias a la venta cambian cada vez que completas un encuentro.");

        int coinsBeforeBuy = GameSession.Coins;
        yield return Act(() => Of(shop.TutorialRect("tienda")),
            "Selecciona la Poción de curación (o cualquier producto) y pulsa COMPRAR. El precio se descuenta de tus Coronas.",
            () => GameSession.Coins < coinsBeforeBuy);

        // ---- Tienda: vender ----
        yield return Act(() => Of(shop.TutorialRect("tabVender")),
            "Ahora abre la pestaña Vender.",
            () => shop.ShopTab == 3);

        yield return Explain(() => Of(shop.TutorialRect("tienda")),
            "En Vender entregas lo que te sobra por la mitad de su precio: consumibles y reliquias sin equipar. Las reliquias equipadas aparecen bloqueadas.");

        int coinsBeforeSell = GameSession.Coins;
        yield return Act(() => Of(shop.TutorialRect("tienda")),
            "Prueba a vender una unidad: selecciónala y pulsa VENDER.",
            () => GameSession.Coins > coinsBeforeSell || (GameSession.Potions + GameSession.Antidotes == 0 && RelicInventory.Items.Count == 0));

        yield return Act(() => Of(shop.TutorialRect("tienda")),
            "Cierra la Tienda con la X (o con ESC).",
            () => !shop.IsOpen);

        // ---- Inventario (reliquias y consumibles) ----
        yield return Act(() => Of(ShortcutButton("BotonInventario")),
            "Ahora haz clic en el botón Inventario.",
            () => panel.IsOpen && panel.InventoryOnly);

        yield return Explain(() => Of(panel.TutorialRect("inventario")),
            "Este es el Inventario: aquí están tus reliquias sin equipar y tus consumibles con su cantidad. Pasa el cursor sobre un objeto para leerlo.");

        yield return Explain(() => Of(panel.TutorialRect("inventario")),
            "Los consumibles se usan en combate sobre un aliado. La poción también levanta a un héroe caído.");

        yield return Act(() => Of(panel.TutorialRect("inventario")),
            "Ciérralo con la X (o con ESC).",
            () => !panel.IsOpen);

        // ---- Reliquias ----
        string relicName = GrantPracticeRelic();

        yield return Explain(null,
            relicName != null
                ? "Para practicar, el Altar te entrega una reliquia: " + relicName + ". Las reliquias se equipan desde la ficha de cada personaje."
                : "Las reliquias se equipan desde la ficha de cada personaje.");

        yield return Act(() => Of(HeroCard()),
            "Haz clic en un héroe de la barra inferior para abrir su ficha.",
            () => panel.IsOpen && !panel.InventoryOnly);

        yield return Explain(() => Of(panel.TutorialRect("ficha")),
            "Esta es la ficha: estadísticas, habilidades y dos casillas de reliquia. Las reliquias suman bonificaciones y, a veces, penalizaciones.");

        if (!panel.InventoryOpen)
            yield return Act(() => Of(panel.TutorialRect("equip")),
                "Haz clic en una casilla de reliquia para abrir el inventario.",
                () => panel.InventoryOpen);

        yield return Act(() => Union(Of(panel.TutorialRect("reliquia")), Of(panel.TutorialRect("equip"))),
            "Arrastra la reliquia del inventario hasta una casilla de reliquia para equiparla.",
            () => HasEquipped());

        yield return Explain(() => Union(Of(panel.TutorialRect("equip")), Of(panel.TutorialRect("inventario"))),
            "¡Equipada! Para quitarla, arrástrala de vuelta a la zona del inventario. Mientras esté equipada no se puede vender.");

        yield return Act(() => Of(panel.TutorialRect("ficha")),
            "Cierra la ficha con la X para terminar.",
            () => !panel.IsOpen);

        yield return Explain(null,
            "¡Listo! Ya sabes comprar, vender, usar el inventario y equipar reliquias. Ahora el mapa es tuyo: elige tu siguiente misión.");

        Hide();
        enabled = false;
    }

    private bool HasEquipped()
    {
        string hero = panel.CurrentHeroId;

        if (hero == null)
            return false;

        for (int i = 0; i < RelicInventory.SlotsPerHero; i++)
            if (RelicInventory.EquippedUid(hero, i) != 0)
                return true;

        return false;
    }

    // Reliquia sencilla (sin exclusividad ni evento) que cualquier heroe puede equipar. Devuelve su nombre.
    private static string GrantPracticeRelic()
    {
        RelicCatalog catalog = RelicCatalog.Instance;

        if (catalog == null)
            return null;

        RelicData best = null;
        int bestScore = int.MinValue;

        foreach (RelicData relic in catalog.Relics)
        {
            if (relic == null || relic.IsExclusive || relic.eventOnly || relic.uniqueOwnership)
                continue;

            // Prefiere la mas sencilla: de la tienda, sin penalizaciones y sin efecto especial.
            int score = (relic.availableInShop ? 4 : 0) + (relic.penalties.Count == 0 ? 2 : 0) + (relic.effect == RelicEffect.None ? 1 : 0);

            if (score > bestScore)
            {
                best = relic;
                bestScore = score;
            }
        }

        if (best == null)
        {
            foreach (RelicData relic in catalog.Relics)
            {
                if (relic != null && !relic.IsExclusive && !relic.eventOnly)
                {
                    best = relic;
                    break;
                }
            }
        }

        if (best == null || RelicInventory.Add(best.typeId) == 0)
            return null;

        return best.displayName;
    }

    // ---------------- Objetivos ----------------

    private static RectTransform ShortcutButton(string objectName)
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Exclude))
            if (button.name == objectName)
                return (RectTransform)button.transform;

        return null;
    }

    private static RectTransform HeroCard()
    {
        RectTransform best = null;

        // Tarjetas de la barra inferior del mapa ("Tarjeta_<nombre>"): la de mas a la izquierda.
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Exclude))
        {
            if (!button.name.StartsWith("Tarjeta_"))
                continue;

            RectTransform rect = (RectTransform)button.transform;

            if (best == null || rect.position.x < best.position.x)
                best = rect;
        }

        return best;
    }

    private static Rect? Of(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
            return null;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    private static Rect? Union(Rect? a, Rect? b)
    {
        if (a == null)
            return b;

        if (b == null)
            return a;

        return Rect.MinMaxRect(Mathf.Min(a.Value.xMin, b.Value.xMin), Mathf.Min(a.Value.yMin, b.Value.yMin),
            Mathf.Max(a.Value.xMax, b.Value.xMax), Mathf.Max(a.Value.yMax, b.Value.yMax));
    }

    // ---------------- Pasos ----------------

    private IEnumerator Explain(Func<Rect?> area, string text)
    {
        if (skipped)
            yield break;

        Show(area, text, true);
        cardHint.text = "ESPACIO: continuar     ENTER: saltar tutorial";
        float guard = Time.unscaledTime + 0.35f;

        while (!skipped)
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                Skip();
            else if (Time.unscaledTime >= guard && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                break;

            yield return null;
        }

        Hide();
        yield return null;
    }

    private IEnumerator Act(Func<Rect?> area, string text, Func<bool> done)
    {
        if (skipped)
            yield break;

        Show(area, text, false);
        cardHint.text = "ENTER: saltar tutorial";

        while (!skipped && !done())
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                Skip();

            yield return null;
        }

        Hide();
        yield return new WaitForSecondsRealtime(0.25f);
    }

    private void Skip()
    {
        skipped = true;
        Hide();
        enabled = false;
    }

    private void Show(Func<Rect?> area, string text, bool explain)
    {
        currentArea = area;
        showHole = true;
        blockHole = explain;
        cardText.text = text;
        root.gameObject.SetActive(true);
        Layout();
    }

    private void Hide()
    {
        currentArea = null;
        showHole = false;
        blockHole = false;

        if (root != null)
            root.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (root != null && root.gameObject.activeSelf)
            Layout();
    }

    // ---------------- Interfaz ----------------

    private void Layout()
    {
        Rect half = root.rect;
        float hw = half.width * 0.5f, hh = half.height * 0.5f;

        Rect hole = Rect.zero;
        Rect? screen = currentArea != null ? currentArea() : null;
        bool hasHole = showHole && screen != null && ToLocal(screen.Value, out hole);

        if (!hasHole)
        {
            // Mensaje general: se oscurece todo y se bloquea. En un paso de accion cuya zona aun no es visible (ventana que
            // se esta abriendo) no se bloquea nada, para que el jugador nunca se quede sin poder hacer clic.
            SetDims(currentArea == null || blockHole, 0, 0, 0, 0, hw, hh);
            SetFrame(false, hole);
            holeBlocker.gameObject.SetActive(false);
        }
        else
        {
            const float pad = 8f;
            hole = new Rect(hole.xMin - pad, hole.yMin - pad, hole.width + pad * 2f, hole.height + pad * 2f);
            SetDims(true, hole.xMin, hole.xMax, hole.yMin, hole.yMax, hw, hh);
            SetFrame(true, hole);
            holeBlocker.gameObject.SetActive(blockHole);

            if (blockHole)
            {
                RectTransform r = holeBlocker.rectTransform;
                r.anchoredPosition = hole.center;
                r.sizeDelta = hole.size;
            }
        }

        // Tarjeta arriba (franja libre sobre las ventanas); si la zona iluminada llega a esa franja, abajo sobre la barra de heroes.
        bool touchesTop = hasHole && hole.yMax > hh - 90f;
        card.anchoredPosition = new Vector2(0f, touchesTop ? -hh + 175f : hh - 63f);
    }

    private void SetDims(bool visible, float xMin, float xMax, float yMin, float yMax, float hw, float hh)
    {
        for (int i = 0; i < 4; i++)
            dims[i].gameObject.SetActive(visible);

        if (!visible)
            return;

        bool full = xMin == 0f && xMax == 0f && yMin == 0f && yMax == 0f;

        if (full)
        {
            Place(dims[0], -hw, hw, -hh, hh);

            for (int i = 1; i < 4; i++)
                dims[i].gameObject.SetActive(false);

            return;
        }

        Place(dims[0], -hw, hw, yMax, hh);
        Place(dims[1], -hw, hw, -hh, yMin);
        Place(dims[2], -hw, xMin, yMin, yMax);
        Place(dims[3], xMax, hw, yMin, yMax);
    }

    private void SetFrame(bool visible, Rect hole)
    {
        for (int i = 0; i < 4; i++)
            frame[i].gameObject.SetActive(visible);

        if (!visible)
            return;

        const float t = 3f;
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
        Color c = new Color(Gold.r, Gold.g, Gold.b, pulse);

        Place(frame[0], hole.xMin, hole.xMax, hole.yMax - t, hole.yMax);
        Place(frame[1], hole.xMin, hole.xMax, hole.yMin, hole.yMin + t);
        Place(frame[2], hole.xMin, hole.xMin + t, hole.yMin, hole.yMax);
        Place(frame[3], hole.xMax - t, hole.xMax, hole.yMin, hole.yMax);

        for (int i = 0; i < 4; i++)
            frame[i].color = c;
    }

    private static void Place(Image image, float xMin, float xMax, float yMin, float yMax)
    {
        RectTransform r = image.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
        r.sizeDelta = new Vector2(Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
    }

    private bool ToLocal(Rect screen, out Rect local)
    {
        local = Rect.zero;
        Camera cam = uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? uiCanvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(screen.xMin, screen.yMin), cam, out Vector2 a) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(screen.xMax, screen.yMax), cam, out Vector2 b))
            return false;

        local = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        return true;
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("TutorialMapaCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        uiCanvas = canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)canvasGo.transform;

        for (int i = 0; i < 4; i++)
        {
            dims[i] = NewImage("Oscuro_" + i, Dim, true);
            frame[i] = NewImage("Borde_" + i, Gold, false);
        }

        holeBlocker = NewImage("BloqueoZona", new Color(0f, 0f, 0f, 0f), true);

        // Tarjeta de texto: panel azul marino con borde dorado fino.
        GameObject cardGo = new GameObject("Tarjeta", typeof(RectTransform), typeof(Image));
        cardGo.transform.SetParent(root, false);
        card = (RectTransform)cardGo.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(1500f, 114f);
        Image cardImage = cardGo.GetComponent<Image>();
        cardImage.color = Gold;
        cardImage.raycastTarget = false;

        GameObject inner = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(card, false);
        RectTransform innerRect = (RectTransform)inner.transform;
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2f, 2f);
        innerRect.offsetMax = new Vector2(-2f, -2f);
        Image innerImage = inner.GetComponent<Image>();
        innerImage.color = Navy;
        innerImage.raycastTarget = false;

        cardText = NewText("Texto", card, Ivory, regularFont, 26f, TextAlignmentOptions.Center);
        cardText.rectTransform.sizeDelta = new Vector2(1420f, 80f);
        cardText.rectTransform.anchoredPosition = new Vector2(0f, 10f);
        cardText.textWrappingMode = TextWrappingModes.Normal;

        cardHint = NewText("Pista", card, Gold, boldFont, 19f, TextAlignmentOptions.BottomRight);
        cardHint.rectTransform.sizeDelta = new Vector2(1440f, 106f);
        cardHint.rectTransform.anchoredPosition = new Vector2(0f, 4f);

        // Omitir: boton visible (ENTER sigue funcionando), con el estilo del boton de la introduccion.
        if (skipNormal != null)
            SkipButtonFactory.Create(card, "OMITIR TUTORIAL", skipNormal, skipHover != null ? skipHover : skipNormal, boldFont, Skip,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-6f, -4f), 330f, 100f);

        root.gameObject.SetActive(false);
    }

    private Image NewImage(string name, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static TMP_Text NewText(string name, RectTransform parent, Color color, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();

        if (font != null)
            t.font = font;

        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.richText = false;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        return t;
    }
}
