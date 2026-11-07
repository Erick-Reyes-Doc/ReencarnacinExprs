using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Ventanas de personajes del mapa: ficha completa (Estado A) y ficha compacta + inventario (Estado B). Solo presenta y
// encamina: los datos salen de HeroProfile / CharacterStats (misma formula que el combate), el equipamiento vive en
// RelicInventory (que valida cada cambio) y los consumibles siguen en GameSession (aqui solo se muestran, no se
// duplican ni se arrastran). Se construye al empezar (Awake) bajo este objeto con los sprites del propio mapa.
public class CharacterPanelController : MonoBehaviour
{
    [Header("Personajes")]
    [SerializeField] private HeroProfile[] heroes = new HeroProfile[0];

    [Header("Estilo (sprites y fuente del mapa)")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Sprite windowSprite;
    [SerializeField] private Sprite frameSprite;
    [SerializeField] private Sprite frameHoverSprite;
    [SerializeField] private Sprite closeIcon;
    [SerializeField] private AbilityIconSet iconSet;   // iconos de habilidades (el mismo del HUD de combate)
    [SerializeField] private Sprite inventoryIcon;     // cofre del boton Inventario
    [SerializeField] private Sprite portraitBackground;   // fondo del retrato grande (Backgrounds/personajes)
    [SerializeField] private Sprite relicsBackground;     // fondo de la seccion de reliquias (Backgrounds/Reliquias)
    [SerializeField] private Sprite[] statIcons = new Sprite[6];        // vida, dano, proteccion, evasion, velocidad, critico (null = rombo)
    [SerializeField] private Sprite[] consumableIcons = new Sprite[2];  // pocion, antidoto

    [Header("Animacion")]
    [SerializeField] private float openSeconds = 0.25f;
    [SerializeField] private float layoutSeconds = 0.25f;

    // Paleta del proyecto.
    private static readonly Color Dark = new Color32(0x10, 0x15, 0x1E, 0xFF);
    private static readonly Color Panel = new Color32(0x17, 0x1F, 0x2B, 0xFF);
    private static readonly Color Gold = new Color32(0xB7, 0x95, 0x5B, 0xFF);
    private static readonly Color GoldBright = new Color32(0xD4, 0xAF, 0x6A, 0xFF);
    private static readonly Color Ivory = new Color32(0xE8, 0xDC, 0xC6, 0xFF);
    private static readonly Color Good = new Color32(0x7F, 0xD0, 0x8F, 0xFF);
    private static readonly Color Bad = new Color32(0xE5, 0x6B, 0x5E, 0xFF);
    // Paleta de la tienda (mismos valores): bordes finos azul grisaceo en reposo y dorado al seleccionar.
    private static readonly Color FilterIdleBorder = new Color(0.239f, 0.290f, 0.361f, 1f);
    private static readonly Color ShopGold = new Color(0.831f, 0.686f, 0.416f, 1f);
    private static readonly Color Muted = new Color32(0x7A, 0x80, 0x8C, 0xFF);

    // Medidas (referencia 1920x1080). Estado B reparte el ancho util ~44 % ficha / 56 % inventario.
    private const float WindowH = 860f;
    private const float WideW = 1500f;       // Estado A
    private const float CompactW = 805f;     // Estado B (ficha, ~44 %)
    private const float InventoryW = 1025f;  // Estado B (inventario, ~56 %)
    private const float PanelGap = 10f;      // separacion entre ambas ventanas
    private const float EdgeMargin = 40f;
    private const int StatRows = 6;
    private const int Columns = 4;
    private const float SlotSize = 180f;
    private const float SlotGap = 20f;
    private const int MinInventorySlots = 12;

    // Estado.
    private bool open;
    private bool inventoryOpen;
    private float soloT;          // 0 = ficha + inventario, 1 = solo inventario centrado (animado)
    private bool inventoryOnly;   // inventario abierto desde el HUD sin ficha: ventana unica y centrada
    private int heroIndex;
    private int filter;       // 0 todo, 1 reliquias, 2 consumibles
    private bool dirty = true;
    private int closedFrame = -1;
    private float openT;      // 0..1 aparicion
    private float layoutT;    // 0 = Estado A, 1 = Estado B

    // Arrastre.
    private RelicSlotView dragSource;
    private Image ghost;
    private int previewSlot = -1;
    private int previewUid;
    private RelicSlotView hovered;
    private string statusMessage = "";
    private float statusUntil;

    // Jerarquia.
    private GameObject root;
    private RectTransform rootRect;
    private CanvasGroup rootGroup;
    private RectTransform veil;
    private RectTransform charWindow;
    private CanvasGroup charGroup;
    private RectTransform invWindow;
    private CanvasGroup invGroup;
    private Image headPortrait, bigPortrait;
    private TMP_Text nameText, levelText, xpText, statusText, relicsTitle;
    private RectTransform xpFill;
    private TMP_Text[] statLabels = new TMP_Text[StatRows];
    private TMP_Text[] statValues = new TMP_Text[StatRows];
    private TMP_Text[] statDeltas = new TMP_Text[StatRows];
    private RectTransform abilitiesGroup;
    private CanvasGroup abilitiesCanvas;
    private Image[] abilityIcons = new Image[4];
    private TMP_Text[] abilityNames = new TMP_Text[4];
    private TipZone[] abilityTips = new TipZone[4];
    private RelicSlotView[] equipSlots = new RelicSlotView[RelicInventory.SlotsPerHero];
    private TMP_Text[] equipNames = new TMP_Text[RelicInventory.SlotsPerHero];
    private TMP_Text[] equipSummaries = new TMP_Text[RelicInventory.SlotsPerHero];
    private readonly List<RelicSlotView> invSlots = new List<RelicSlotView>();
    private RectTransform invContent;
    private ScrollRect invScroll;
    private Image[] tabFrames;
    private Image[] filterFrames;
    private Image[] filterFills;

    // Tooltip.
    private RectTransform tip;
    private TMP_Text tipTitle, tipBody;
    private bool tipVisible;

    // Ventana A <-> B: cada elemento anima entre su posicion/tamano de la ficha completa y de la compacta.
    private struct Placement
    {
        public RectTransform rect;
        public Vector2 posA, sizeA, posB, sizeB;
    }

    private readonly List<Placement> placements = new List<Placement>();
    private TMP_Text nameLabel;

    // true mientras alguna ventana de personajes / inventario esta visible (la tienda lo usa para dejar los atajos del HUD activos).
    public static bool AnyVisible { get; private set; }

    public bool IsOpen => open;
    public bool InventoryOnly => open && inventoryOnly;   // inventario abierto desde el HUD, sin ficha
    public bool InventoryOpen => open && inventoryOpen;
    public bool ClosedThisFrame => closedFrame == Time.frameCount;
    public HeroProfile[] Heroes => heroes;
    public string CurrentHeroId => heroes != null && heroIndex >= 0 && heroIndex < heroes.Length && heroes[heroIndex] != null ? heroes[heroIndex].unitId : null;

    public HeroProfile Profile(string unitId)
    {
        foreach (HeroProfile hero in heroes)
            if (hero != null && hero.unitId == unitId)
                return hero;

        return null;
    }

    public Sprite PortraitOf(HeroProfile hero)
    {
        return hero != null ? HeroPortraits.Get(hero.bodySprite) : null;
    }

    // ------------------------------------------------------------
    // Apertura
    // ------------------------------------------------------------

    public bool Open(string unitId)
    {
        if (open || root == null)
            return false;

        for (int i = 0; i < heroes.Length; i++)
        {
            if (heroes[i] != null && heroes[i].unitId == unitId)
            {
                heroIndex = i;
                break;
            }
        }

        open = true;
        inventoryOnly = false;
        soloT = 0f;
        inventoryOpen = false;
        layoutT = 0f;
        previewSlot = -1;
        statusMessage = "";
        dirty = true;
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        return true;
    }

    // Boton Inventario del HUD sin ninguna ficha abierta: solo el inventario, centrado en la pantalla.
    public bool OpenInventory()
    {
        if (open || root == null)
            return false;

        open = true;
        inventoryOnly = true;
        soloT = 1f;
        inventoryOpen = true;
        layoutT = 1f;
        previewSlot = -1;
        statusMessage = "";
        dirty = true;
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        return true;
    }

    public void Close()
    {
        if (!open)
            return;

        CancelDrag();
        HideTip();
        open = false;
        inventoryOpen = false;
        closedFrame = Time.frameCount;
    }

    // X de la ficha: con el inventario abierto solo se cierra la ficha y el inventario queda centrado.
    private void CloseCharacterWindow()
    {
        if (inventoryOpen && !inventoryOnly)
        {
            CancelDrag();
            HideTip();
            previewSlot = -1;
            inventoryOnly = true;
            return;
        }

        Close();
    }

    // Clic en una casilla de reliquia equipada: abre el inventario a la derecha (si ya esta abierto no hace nada).
    internal void OnEquipSlotClicked()
    {
        if (open && !inventoryOpen && !inventoryOnly)
            ToggleInventory();
    }

    // Boton Inventario con la ficha abierta: pasa entre Estado A y Estado B.
    public void ToggleInventory()
    {
        if (!open)
            return;

        if (inventoryOnly)
        {
            Close();   // sin ficha detras, cerrar el inventario cierra la ventana
            return;
        }

        CancelDrag();
        HideTip();
        inventoryOpen = !inventoryOpen;
        previewSlot = -1;
        dirty = true;
    }

    private void SelectHero(int index)
    {
        if (index < 0 || index >= heroes.Length || index == heroIndex)
            return;

        CancelDrag();
        HideTip();
        heroIndex = index;   // el inventario conserva su estado (abierto/cerrado)
        previewSlot = -1;
        dirty = true;
    }

    private void SetFilter(int value)
    {
        if (filter == value)
            return;

        filter = value;

        if (invScroll != null)
            invScroll.verticalNormalizedPosition = 1f;

        dirty = true;
    }

    // ------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------

    private void Awake()
    {
        Build();
    }

    private void OnEnable()
    {
        RelicInventory.Changed += OnRelicsChanged;
    }

    private void OnDisable()
    {
        RelicInventory.Changed -= OnRelicsChanged;
        AnyVisible = false;
    }

    private void OnRelicsChanged()
    {
        dirty = true;
    }

    private void Update()
    {
        if (root == null)
            return;

        if (open && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (dragSource != null)
                CancelDrag();
            else if (inventoryOpen)
                ToggleInventory();
            else
                Close();
        }

        // Fundido de aparicion y de cambio de estado (tiempo no escalado).
        openT = Mathf.MoveTowards(openT, open ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, openSeconds));
        layoutT = Mathf.MoveTowards(layoutT, inventoryOpen ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, layoutSeconds));
        soloT = Mathf.MoveTowards(soloT, inventoryOnly ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, layoutSeconds));

        bool visible = openT > 0f;
        AnyVisible = visible && inventoryOnly;   // solo el inventario suelto deja activos los atajos del HUD

        if (root.activeSelf != visible)
            root.SetActive(visible);

        if (!visible)
        {
            inventoryOnly = false;
            soloT = 0f;
            return;
        }

        float eased = Smooth(openT);
        rootGroup.alpha = eased;
        rootGroup.interactable = open;
        rootGroup.blocksRaycasts = open;

        ApplyLayout(Smooth(layoutT), eased);

        if (statusMessage != "" && Time.unscaledTime > statusUntil)
        {
            statusMessage = "";
            dirty = true;
        }

        if (dragSource != null)
            UpdateDragHover();

        if (dirty)
        {
            dirty = false;
            RefreshAll();
        }

        if (tipVisible)
            PlaceTip();
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // Estado A = ficha ancha y centrada; Estado B = ficha compacta a la izquierda e inventario a la derecha, sin
    // solaparse. Los elementos de la ficha se mueven entre sus dos posiciones; las habilidades se ocultan en B y las
    // casillas de reliquias siempre permanecen visibles.
    private void ApplyLayout(float t, float appear)
    {
        float charW = Mathf.Lerp(WideW, CompactW, t);
        float charX = Mathf.Lerp(0f, -(960f - EdgeMargin - CompactW * 0.5f), t);
        charWindow.sizeDelta = new Vector2(charW, WindowH);
        charWindow.anchoredPosition = new Vector2(charX, 0f);
        charWindow.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, appear);

        // Al cerrar la ficha con el inventario abierto, el inventario se desliza al centro y la ficha se desvanece.
        float solo = Smooth(soloT);
        float invX = Mathf.Lerp(charX + charW * 0.5f + PanelGap + InventoryW * 0.5f, 0f, solo);
        charGroup.alpha = 1f - solo;
        charGroup.blocksRaycasts = solo < 0.5f;
        charGroup.interactable = solo < 0.5f;
        invWindow.sizeDelta = new Vector2(InventoryW, WindowH);
        invWindow.anchoredPosition = new Vector2(invX, 0f);
        invWindow.localScale = Vector3.one;
        invGroup.alpha = t * appear;
        invGroup.interactable = t > 0.9f && open;
        invGroup.blocksRaycasts = t > 0.5f && open;

        bool showInventory = t > 0.001f;

        if (invWindow.gameObject.activeSelf != showInventory)
            invWindow.gameObject.SetActive(showInventory);

        foreach (Placement p in placements)
        {
            p.rect.anchoredPosition = Vector2.Lerp(p.posA, p.posB, t);
            p.rect.sizeDelta = Vector2.Lerp(p.sizeA, p.sizeB, t);
        }

        nameLabel.fontSize = Mathf.Lerp(54f, 42f, t);
        abilitiesCanvas.alpha = 1f - Mathf.Clamp01(t * 2.5f);
        abilitiesCanvas.blocksRaycasts = t < 0.4f;
        abilitiesGroup.gameObject.SetActive(t < 0.99f);

            }

    // ------------------------------------------------------------
    // Presentacion
    // ------------------------------------------------------------

    private HeroProfile Hero => heroes != null && heroIndex >= 0 && heroIndex < heroes.Length ? heroes[heroIndex] : null;

    private void RefreshAll()
    {
        HeroProfile hero = Hero;

        if (hero == null)
            return;

        HeroStats stats = CharacterStats.Current(hero);

        // Cabecera y retrato.
        headPortrait.sprite = HeroPortraits.Get(hero.bodySprite);
        headPortrait.color = hero.tint;
        bigPortrait.sprite = hero.bodySprite;
        bigPortrait.color = hero.tint;
        bigPortrait.enabled = hero.bodySprite != null;
        nameText.text = hero.displayName.ToUpperInvariant();
        levelText.text = "Nivel " + stats.level;

        float xpRatio = stats.experienceRequired > 0 ? Mathf.Clamp01((float)stats.experience / stats.experienceRequired) : 1f;
        xpFill.anchorMax = new Vector2(xpRatio, 1f);
        xpText.text = stats.experienceRequired > 0 ? stats.experience + " / " + stats.experienceRequired + " XP" : "Nivel máximo";

        RefreshStats(hero, stats);
        RefreshAbilities(hero);
        RefreshEquipSlots(hero);
        RefreshInventory();

        for (int i = 0; i < tabFrames.Length; i++)
            tabFrames[i].color = i == heroIndex ? GoldBright : new Color(Gold.r, Gold.g, Gold.b, 0.45f);

        for (int i = 0; i < filterFrames.Length; i++)
        {
            bool active = i == filter;
            filterFrames[i].color = active ? ShopGold : FilterIdleBorder;
            filterFills[i].color = active ? new Color(0.150f, 0.200f, 0.285f, 1f) : Panel;
        }

        statusText.text = statusMessage;
        statusText.color = Bad;

        if (hovered != null && !hovered.gameObject.activeInHierarchy)
            hovered = null;

        RefreshHoverTip();
    }

    // Seis estadisticas de combate, siempre con valores reales: Vida, Daño (min-max), Protección %, Evasión %,
    // Velocidad y Crítico %. Con una reliquia en arrastre valido se muestra el valor resultante (▲ / ▼).
    private static string DamageText(HeroStats s) { return s.damageMin + "–" + s.damageMax; }
    private static string Percent(float value) { return value.ToString("0.#") + " %"; }

    // Cifras de ancho fijo (mspace): Georgia usa numeros de ancho variable, y asi las columnas de valores se veian desiguales.
    private static string Tab(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"\d+","<mspace=0.62em>$0</mspace>");
    }

    private void RefreshStats(HeroProfile hero, HeroStats stats)
    {
        HeroStats after = stats;
        bool preview = previewSlot >= 0;

        if (preview)
            after = CharacterStats.Compute(hero, RelicInventory.PreviewMods(hero.unitId, previewSlot, previewUid));

        string[] labels = { "Vida", "Daño", "Protección", "Evasión", "Velocidad", "Crítico" };
        string[] values =
        {
            stats.health + " / " + stats.maxHealth,
            DamageText(stats),
            Percent(stats.protection),
            Percent(stats.evasion),
            stats.speed.ToString(),
            Percent(stats.crit)
        };
        string[] valuesAfter =
        {
            after.maxHealth.ToString(),
            DamageText(after),
            Percent(after.protection),
            Percent(after.evasion),
            after.speed.ToString(),
            Percent(after.crit)
        };
        // Para comparar el daño se usa la suma de minimo y maximo.
        float[] now = { stats.maxHealth, stats.damageMin + stats.damageMax, stats.protection, stats.evasion, stats.speed, stats.crit };
        float[] next = { after.maxHealth, after.damageMin + after.damageMax, after.protection, after.evasion, after.speed, after.crit };

        for (int i = 0; i < StatRows; i++)
        {
            statLabels[i].text = labels[i];
            // Todos los valores empiezan en la misma posicion (alineados a la izquierda), tambien en escenas ya construidas.
            statValues[i].alignment = TextAlignmentOptions.MidlineLeft;
            statValues[i].rectTransform.anchoredPosition = new Vector2(240f, statValues[i].rectTransform.anchoredPosition.y);
            statValues[i].rectTransform.sizeDelta = new Vector2(110f, statValues[i].rectTransform.sizeDelta.y);
            statValues[i].text = Tab(values[i]);

            bool changes = preview && !Mathf.Approximately(now[i], next[i]);
            statDeltas[i].gameObject.SetActive(changes);

            if (changes)
            {
                bool better = next[i] > now[i];
                statDeltas[i].text = (better ? "▲ " : "▼ ") + Tab(valuesAfter[i]);
                statDeltas[i].color = better ? Good : Bad;
            }
        }
    }

    private void RefreshAbilities(HeroProfile hero)
    {
        bool liberated = GameSession.TryGetMember(hero.unitId, out GameSession.PartyMemberState state) && state.transformed;
        AbilityData[] set = liberated && hero.transformedAbilities != null && hero.transformedAbilities.Length > 0
            ? hero.transformedAbilities
            : hero.abilities;

        for (int i = 0; i < abilityIcons.Length; i++)
        {
            AbilityData ability = set != null && i < set.Length ? set[i] : null;
            bool show = ability != null;
            abilityTips[i].gameObject.SetActive(show);

            if (!show)
                continue;

            Sprite icon = iconSet != null ? iconSet.Get(ability) : ability.abilityIcon;
            abilityIcons[i].sprite = icon;
            abilityIcons[i].enabled = icon != null;
            abilityNames[i].text = ability.abilityName;

            string body = string.IsNullOrEmpty(ability.description) ? "" : ability.description;

            if (ability.basePower > 0)
                body += (body != "" ? "\n" : "") + "<color=#D4AF6A>Poder base:</color> " + ability.basePower;

            if (ability.cooldown > 0)
                body += (body != "" ? "\n" : "") + "<color=#D4AF6A>Enfriamiento:</color> " + ability.cooldown + " turnos";

            abilityTips[i].title = ability.abilityName;
            abilityTips[i].body = body;
        }
    }

    private void RefreshEquipSlots(HeroProfile hero)
    {
        for (int i = 0; i < equipSlots.Length; i++)
        {
            int uid = RelicInventory.EquippedUid(hero.unitId, i);
            equipSlots[i].Set(uid, uid != 0 ? RelicInventory.DataOf(uid) : null);
        }
    }

    // Una sola cuadricula con filtros: reliquias libres (arrastrables) y consumibles de GameSession (solo lectura).
    private void RefreshInventory()
    {
        List<KeyValuePair<int, int>> entries = new List<KeyValuePair<int, int>>();   // (uid de reliquia, consumible) ; -1 = no aplica

        if (filter != 2)
            foreach (RelicInventory.Item item in RelicInventory.Items)
                if (!RelicInventory.IsEquipped(item.uid))
                    entries.Add(new KeyValuePair<int, int>(item.uid, -1));

        if (filter != 1)
            for (int c = 0; c < 2; c++)
                if (GameSession.GetCount((GameSession.Consumable)c) > 0)
                    entries.Add(new KeyValuePair<int, int>(0, c));

        int rows = Mathf.Max(MinInventorySlots / Columns, Mathf.CeilToInt(entries.Count / (float)Columns));
        EnsureInventorySlots(rows * Columns);

        for (int i = 0; i < invSlots.Count; i++)
        {
            bool inRange = i < rows * Columns;
            invSlots[i].gameObject.SetActive(inRange);

            if (!inRange)
                continue;

            if (i < entries.Count && entries[i].Key != 0)
            {
                invSlots[i].Set(entries[i].Key, RelicInventory.DataOf(entries[i].Key));
            }
            else if (i < entries.Count)
            {
                int c = entries[i].Value;
                invSlots[i].Set(0, null);
                invSlots[i].SetConsumable(c, c < consumableIcons.Length ? consumableIcons[c] : null, "x" + GameSession.GetCount((GameSession.Consumable)c));
            }
            else
            {
                invSlots[i].Set(0, null);
            }
        }

        invContent.sizeDelta = new Vector2(0f, rows * (SlotSize + SlotGap));
    }

    private void EnsureInventorySlots(int count)
    {
        while (invSlots.Count < count)
        {
            int i = invSlots.Count;
            RectTransform slotRect = TopLeft("Casilla_" + i, invContent, 35f + (i % Columns) * (SlotSize + SlotGap), -(i / Columns) * (SlotSize + SlotGap), SlotSize, SlotSize);
            invSlots.Add(MakeSlot(slotRect, RelicSlotView.SlotKind.Inventory, i, 34f));
        }
    }

    private string ExclusiveName(RelicData data)
    {
        HeroProfile hero = Profile(data.exclusiveHeroId);
        return hero != null ? hero.displayName : data.exclusiveHeroId;
    }

    private static string Summary(RelicData data)
    {
        string text = "";

        foreach (RelicModifier m in data.bonuses)
            text += "<color=#7FD08F>+" + m.amount + " " + RelicData.StatName(m.stat) + "</color> ";

        foreach (RelicModifier m in data.penalties)
            text += "<color=#E56B5E>−" + m.amount + " " + RelicData.StatName(m.stat) + "</color> ";

        return text.Trim();
    }

    // ------------------------------------------------------------
    // Tooltip
    // ------------------------------------------------------------

    internal void ShowTip(string title, string body)
    {
        if (tip == null || dragSource != null)
            return;

        float width = 380f;
        tipTitle.text = title;
        tipBody.text = body;
        float titleH = tipTitle.GetPreferredValues(title, width - 32f, 0f).y;
        float bodyH = string.IsNullOrEmpty(body) ? 0f : tipBody.GetPreferredValues(body, width - 32f, 0f).y;
        tipTitle.rectTransform.sizeDelta = new Vector2(width - 32f, titleH);
        tipTitle.rectTransform.anchoredPosition = new Vector2(16f, -12f);
        tipBody.rectTransform.sizeDelta = new Vector2(width - 32f, bodyH);
        tipBody.rectTransform.anchoredPosition = new Vector2(16f, -12f - titleH - (bodyH > 0f ? 6f : 0f));
        tip.sizeDelta = new Vector2(width, titleH + bodyH + (bodyH > 0f ? 6f : 0f) + 26f);
        tipVisible = true;
        tip.gameObject.SetActive(true);
        tip.SetAsLastSibling();
        PlaceTip();
    }

    internal void HideTip()
    {
        tipVisible = false;

        if (tip != null)
            tip.gameObject.SetActive(false);
    }

    // Sigue al cursor sin salirse de la pantalla.
    private void PlaceTip()
    {
        if (Mouse.current == null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        canvas = canvas != null ? canvas.rootCanvas : null;
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, Mouse.current.position.ReadValue(), cam, out Vector2 local))
            return;

        Rect bounds = rootRect.rect;
        Vector2 size = tip.sizeDelta;
        Vector2 position = local + new Vector2(28f, -28f);

        if (position.x + size.x > bounds.xMax - 10f)
            position.x = local.x - 28f - size.x;

        if (position.y - size.y < bounds.yMin + 10f)
            position.y = local.y + 28f + size.y;

        position.x = Mathf.Clamp(position.x, bounds.xMin + 10f, bounds.xMax - size.x - 10f);
        position.y = Mathf.Clamp(position.y, bounds.yMin + size.y + 10f, bounds.yMax - 10f);
        tip.anchoredPosition = position;
    }

    private void RefreshHoverTip()
    {
        if (hovered == null || dragSource != null || !open)
        {
            if (hovered == null)
                HideTip();

            return;
        }

        if (hovered.Uid != 0)
        {
            RelicData data = RelicInventory.DataOf(hovered.Uid);

            if (data == null)
            {
                HideTip();
                return;
            }

            ShowTip("<color=#" + ColorUtility.ToHtmlStringRGB(RelicData.RarityColor(data.rarity)) + ">" + data.displayName + "</color>", RelicBody(hovered.Uid, data));
        }
        else if (hovered.ConsumableId >= 0)
        {
            GameSession.Consumable type = (GameSession.Consumable)hovered.ConsumableId;
            ShowTip(ConsumableService.Name(type), ConsumableService.Description(type) +
                "\n<color=#D4AF6A>Cantidad:</color> " + GameSession.GetCount(type) + " / " + GameSession.MaxPerItem +
                "\n<color=#B9B2A3>Se usa en combate o desde la tienda del mapa.</color>");
        }
        else
        {
            HideTip();
        }
    }

    private string RelicBody(int uid, RelicData data)
    {
        string text = "<color=#" + ColorUtility.ToHtmlStringRGB(RelicData.RarityColor(data.rarity)) + ">" + RelicData.RarityName(data.rarity) + "</color>";

        if (data.IsExclusive)
            text += "  ·  Exclusiva de " + ExclusiveName(data);

        if (!string.IsNullOrEmpty(data.description))
            text += "\n" + data.description;

        foreach (RelicModifier m in data.bonuses)
            text += "\n<color=#7FD08F>+" + m.amount + RelicData.Suffix(m.stat) + " " + RelicData.StatName(m.stat) + "</color>";

        foreach (RelicModifier m in data.penalties)
            text += "\n<color=#E56B5E>−" + m.amount + RelicData.Suffix(m.stat) + " " + RelicData.StatName(m.stat) + "</color>";

        if (RelicInventory.IsEquipped(uid, out string owner, out _))
        {
            HeroProfile ownerHero = Profile(owner);
            text += "\n<color=#B9B2A3>Equipada por " + (ownerHero != null ? ownerHero.displayName : owner) + ".</color>";
        }

        return text;
    }

    // ------------------------------------------------------------
    // Arrastrar y soltar
    // ------------------------------------------------------------

    internal void Hover(RelicSlotView view, bool enter)
    {
        if (dragSource != null)
            return;

        hovered = enter ? view : null;
        RefreshHoverTip();
    }

    internal void BeginDrag(RelicSlotView source, PointerEventData e)
    {
        if (source == null || source.Uid == 0 || GameSession.InCombat)
        {
            if (GameSession.InCombat)
                Say("No se pueden cambiar reliquias durante un combate.");

            return;
        }

        HideTip();
        dragSource = source;
        RelicData data = RelicInventory.DataOf(source.Uid);
        ghost.sprite = data != null ? data.icon : null;
        ghost.enabled = ghost.sprite != null;
        ghost.rectTransform.SetAsLastSibling();
        ghost.gameObject.SetActive(true);
        source.SetDragging(true);
        dirty = true;
        MoveGhost(e);
    }

    internal void Drag(PointerEventData e)
    {
        if (dragSource != null)
            MoveGhost(e);
    }

    internal void EndDrag()
    {
        // Un arrastre que no termino en un destino valido no cambia nada: el icono vuelve a su sitio.
        CancelDrag();
    }

    private void MoveGhost(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, e.position, e.pressEventCamera, out Vector2 local);
        ghost.rectTransform.localPosition = new Vector3(local.x, local.y, 0f);
    }

    private void CancelDrag()
    {
        if (dragSource != null)
            dragSource.SetDragging(false);

        dragSource = null;

        if (ghost != null)
            ghost.gameObject.SetActive(false);

        foreach (RelicSlotView slot in equipSlots)
            if (slot != null)
                slot.SetHighlight(RelicSlotView.Highlight.None);

        if (previewSlot >= 0)
        {
            previewSlot = -1;
            dirty = true;
        }
    }

    // Durante el arrastre: resalta la casilla de equipo bajo el cursor (dorado = valida, rojo = no compatible) y
    // muestra la comparacion de estadisticas.
    private void UpdateDragHover()
    {
        RelicSlotView target = null;
        Vector2 position = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);

        foreach (RaycastResult hit in hits)
        {
            RelicSlotView view = hit.gameObject.GetComponent<RelicSlotView>();

            if (view != null && view.Kind == RelicSlotView.SlotKind.Equip)
            {
                target = view;
                break;
            }
        }

        foreach (RelicSlotView slot in equipSlots)
            slot.SetHighlight(RelicSlotView.Highlight.None);

        int newSlot = -1;
        int newUid = 0;

        if (target != null && Hero != null)
        {
            bool valid = RelicInventory.CanEquip(dragSource.Uid, Hero.unitId, target.Index, out string reason);
            target.SetHighlight(valid ? RelicSlotView.Highlight.Valid : RelicSlotView.Highlight.Invalid);

            if (valid)
            {
                newSlot = target.Index;
                newUid = dragSource.Uid;
            }
            else if (statusMessage != reason)
            {
                Say(reason);
            }
        }

        if (newSlot != previewSlot || newUid != previewUid)
        {
            previewSlot = newSlot;
            previewUid = newUid;
            dirty = true;
        }
    }

    // Soltar sobre una casilla de equipo.
    internal void DropOnEquip(RelicSlotView target)
    {
        RelicSlotView source = dragSource;

        if (source == null || Hero == null)
            return;

        if (!RelicInventory.TryEquip(source.Uid, Hero.unitId, target.Index, out string reason))
        {
            Say(reason);   // no valida: nada cambia y el icono vuelve a su sitio
            return;
        }

        target.Flash();
        AfterEquipmentChange();
    }

    // Soltar sobre el inventario: quita la reliquia si venia de una casilla de equipo.
    internal void DropOnInventory()
    {
        RelicSlotView source = dragSource;

        if (source == null || Hero == null || source.Kind != RelicSlotView.SlotKind.Equip)
            return;

        if (RelicInventory.TryUnequip(Hero.unitId, source.Index))
            AfterEquipmentChange();
        else
            Say("No se puede quitar la reliquia ahora.");
    }

    private void AfterEquipmentChange()
    {
        // La vida maxima de la foto del equipo sigue a las reliquias (sin curar: la vida actual solo se recorta).
        foreach (HeroProfile hero in heroes)
            if (hero != null)
                GameSession.TryUpdateMemberMaxHealth(hero.unitId, CharacterStats.Current(hero).maxHealth);

        dirty = true;
    }

    private void Say(string message)
    {
        statusMessage = message;
        statusUntil = Time.unscaledTime + 3f;
        dirty = true;
    }

    // ------------------------------------------------------------
    // Construccion de la interfaz
    // ------------------------------------------------------------

    private void Build()
    {
        RectTransform self = (RectTransform)transform;
        self.anchorMin = Vector2.zero;
        self.anchorMax = Vector2.one;
        self.offsetMin = self.offsetMax = Vector2.zero;

        root = Rect("Personajes", self, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f)).gameObject;
        rootRect = (RectTransform)root.transform;
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        rootGroup = root.AddComponent<CanvasGroup>();

        // Velo oscuro: bloquea el mapa mientras hay una ficha abierta.
        veil = Rect("Velo", rootRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        veil.offsetMin = veil.offsetMax = Vector2.zero;
        Img(veil, null, new Color(Dark.r, Dark.g, Dark.b, 0.55f), false, 1f, true);

        BuildCharacterWindow(rootRect);
        BuildInventoryWindow(rootRect);

        RectTransform ghostRect = Rect("IconoArrastre", rootRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        ghostRect.sizeDelta = new Vector2(104f, 104f);
        ghost = Img(ghostRect, null, new Color(1f, 1f, 1f, 0.78f), false, 1f, false);   // ligera transparencia
        ghost.preserveAspect = true;
        ghost.gameObject.SetActive(false);

        BuildTooltip(rootRect);
        root.SetActive(false);
    }

    private void BuildTooltip(RectTransform parent)
    {
        tip = Rect("Tooltip", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f));
        tip.sizeDelta = new Vector2(380f, 100f);
        Img(tip, null, Gold, false, 1f, false);
        RectTransform inner = Rect("Fondo", tip, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        inner.offsetMin = new Vector2(2f, 2f);
        inner.offsetMax = new Vector2(-2f, -2f);
        Img(inner, null, new Color(Dark.r, Dark.g, Dark.b, 0.97f), false, 1f, false);

        tipTitle = Txt(TopLeft("Titulo", tip, 16f, -12f, 348f, 30f), "", 26f, Ivory, TextAlignmentOptions.TopLeft);
        tipTitle.textWrappingMode = TextWrappingModes.Normal;
        tipBody = Txt(TopLeft("Texto", tip, 16f, -48f, 348f, 60f), "", 21f, Ivory, TextAlignmentOptions.TopLeft);
        tipBody.textWrappingMode = TextWrappingModes.Normal;
        tip.gameObject.SetActive(false);
    }

    // Registra un elemento de la ficha con su posicion y tamano en Estado A y en Estado B (esquina superior izquierda).
    private RectTransform Place(RectTransform rect, float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh)
    {
        rect.anchoredPosition = new Vector2(ax, ay);
        rect.sizeDelta = new Vector2(aw, ah);
        placements.Add(new Placement
        {
            rect = rect,
            posA = new Vector2(ax, ay),
            sizeA = new Vector2(aw, ah),
            posB = new Vector2(bx, by),
            sizeB = new Vector2(bw, bh)
        });
        return rect;
    }

    private RectTransform FramedBox(string name, Transform parent, float border, Color frame, Color fill)
    {
        RectTransform box = Rect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        Img(box, null, frame, false, 1f, false);
        RectTransform inner = Rect("Fondo", box, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        inner.offsetMin = new Vector2(border, border);
        inner.offsetMax = new Vector2(-border, -border);
        Img(inner, null, fill, false, 1f, false);
        return box;
    }

    private void BuildCharacterWindow(RectTransform parent)
    {
        charWindow = Window("FichaPersonaje", parent, out charGroup);
        charWindow.sizeDelta = new Vector2(WideW, WindowH);
        BuildCloseButton(charWindow, CloseCharacterWindow);

        // ---- Cabecera (compacta: retrato, nombre, nivel y XP; selector en una sola fila) ----
        RectTransform headBox = FramedBox("RetratoCabecera", charWindow, 3f, Gold, Dark);
        Place(headBox, 70f, -48f, 124f, 124f, 62f, -50f, 96f, 96f);
        RectTransform headFace = Rect("Imagen", headBox, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        headFace.offsetMin = new Vector2(6f, 6f);
        headFace.offsetMax = new Vector2(-6f, -6f);
        headPortrait = Img(headFace, null, Color.white, false, 1f, false);
        headPortrait.preserveAspect = true;

        RectTransform nameRect = Place(TopLeft("Nombre", charWindow, 0f, 0f, 560f, 64f), 222f, -52f, 560f, 64f, 174f, -46f, 320f, 52f);
        nameText = Txt(nameRect, "", 54f, Ivory, TextAlignmentOptions.MidlineLeft);
        nameLabel = nameText;

        RectTransform pill = FramedBox("Nivel", charWindow, 2f, Gold, Panel);
        Place(pill, 222f, -122f, 170f, 40f, 174f, -102f, 140f, 34f);
        levelText = Txt(Rect("Texto", pill, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f)), "", 24f, GoldBright, TextAlignmentOptions.Midline);
        levelText.rectTransform.offsetMin = levelText.rectTransform.offsetMax = Vector2.zero;

        RectTransform xp = FramedBox("BarraXP", charWindow, 2f, Gold, Panel);
        Place(xp, 404f, -122f, 540f, 40f, 326f, -102f, 310f, 34f);
        xpFill = Rect("Relleno", xp, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f));
        xpFill.offsetMin = new Vector2(2f, 2f);
        xpFill.offsetMax = new Vector2(-2f, -2f);
        Img(xpFill, null, new Color(GoldBright.r, GoldBright.g, GoldBright.b, 0.5f), false, 1f, false);
        xpText = Txt(Rect("Texto", xp, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f)), "", 20f, Ivory, TextAlignmentOptions.Midline);
        xpText.rectTransform.offsetMin = xpText.rectTransform.offsetMax = Vector2.zero;

        // Separador de la cabecera.
        RectTransform separator = Place(TopLeft("Separador", charWindow, 0f, 0f, 1f, 1f), 50f, -212f, 1400f, 2f, 56f, -222f, 693f, 2f);
        Img(separator, null, new Color(Gold.r, Gold.g, Gold.b, 0.55f), false, 1f, false);

        // Selector de personajes: una sola fila de retratos pequenos; el nombre sale en un tooltip.
        RectTransform tabs = Place(TopLeft("Selector", charWindow, 0f, 0f, 276f, 60f), 1040f, -88f, 276f, 60f, 264f, -152f, 276f, 60f);
        tabFrames = new Image[heroes.Length];

        for (int i = 0; i < heroes.Length; i++)
        {
            RectTransform tab = TopLeft("Selector_" + i, tabs, i * 72f, 0f, 60f, 60f);
            tabFrames[i] = Img(tab, null, Gold, false, 1f, true);
            RectTransform tabInner = Rect("Imagen", tab, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            tabInner.offsetMin = new Vector2(3f, 3f);
            tabInner.offsetMax = new Vector2(-3f, -3f);
            Img(tabInner, null, Dark, false, 1f, false);
            RectTransform tabPortrait = Rect("Retrato", tabInner, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            tabPortrait.offsetMin = new Vector2(2f, 2f);
            tabPortrait.offsetMax = new Vector2(-2f, -2f);
            Image face = Img(tabPortrait, heroes[i] != null ? HeroPortraits.Get(heroes[i].bodySprite) : null, heroes[i] != null ? heroes[i].tint : Color.white, false, 1f, false);
            face.preserveAspect = true;
            tab.gameObject.AddComponent<TipZone>().Setup(this, heroes[i] != null ? heroes[i].displayName : "", "");

            Button button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = tabFrames[i];
            button.transition = Selectable.Transition.None;
            int index = i;
            button.onClick.AddListener(() => SelectHero(index));
        }

        // ---- Retrato grande (escala entera lo bastante grande para el pixel art; fondo sutil) ----
        RectTransform portraitBox = FramedBox("Retrato", charWindow, 3f, Gold, new Color32(0x14, 0x1C, 0x29, 0xFF));
        Place(portraitBox, 60f, -236f, 340f, 574f, 68f, -240f, 226f, 340f);
        RectTransform scene = Rect("Escena", portraitBox, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        scene.offsetMin = new Vector2(3f, 3f);
        scene.offsetMax = new Vector2(-3f, -3f);
        scene.gameObject.AddComponent<RectMask2D>();
        if (portraitBackground != null)
        {
            RectTransform stage = Rect("Escenario", scene, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = new Vector2(340f, 574f);
            Image stageImage = Img(stage, portraitBackground, Color.white, false, 1f, false);
            stageImage.preserveAspect = false;
            AspectRatioFitter fit = stage.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = portraitBackground.rect.width / portraitBackground.rect.height;
        }

        RectTransform big = Rect("Imagen", scene, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        big.offsetMin = new Vector2(-34f, -66f);
        big.offsetMax = new Vector2(34f, 66f);
        bigPortrait = Img(big, null, Color.white, false, 1f, false);
        bigPortrait.preserveAspect = true;

        // ---- Estadisticas ----
        RectTransform stats = Place(TopLeft("Estadisticas", charWindow, 0f, 0f, 420f, 350f), 430f, -236f, 420f, 350f, 310f, -240f, 420f, 350f);
        Txt(TopLeft("Titulo", stats, 0f, 0f, 420f, 40f), "ESTADÍSTICAS", 28f, GoldBright, TextAlignmentOptions.Midline);

        for (int i = 0; i < StatRows; i++)
        {
            float y = -50f - i * 50f;
            RectTransform line = TopLeft("Linea", stats, 0f, y - 47f, 420f, 2f);
            Img(line, null, new Color(Gold.r, Gold.g, Gold.b, 0.3f), false, 1f, false);

            Sprite statIcon = i < statIcons.Length ? statIcons[i] : null;
            RectTransform iconRect = TopLeft("Icono", stats, 4f, y - 9f, 30f, 30f);
            Image icon = Img(iconRect, statIcon, statIcon != null ? Color.white : new Color(Gold.r, Gold.g, Gold.b, 0.8f), false, 1f, false);
            icon.preserveAspect = true;

            if (statIcon == null)
            {
                iconRect.anchoredPosition = new Vector2(12f, y - 17f);
                iconRect.sizeDelta = new Vector2(14f, 14f);
                iconRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            statLabels[i] = Txt(TopLeft("Etiqueta", stats, 48f, y, 190f, 46f), "", 27f, Ivory, TextAlignmentOptions.MidlineLeft);
            statValues[i] = Txt(TopLeft("Valor", stats, 240f, y, 110f, 46f), "", 28f, GoldBright, TextAlignmentOptions.MidlineLeft);
            statDeltas[i] = Txt(TopLeft("Cambio", stats, 344f, y, 76f, 46f), "", 20f, Good, TextAlignmentOptions.MidlineRight);
            statDeltas[i].gameObject.SetActive(false);
        }

        // ---- Habilidades (solo Estado A): cuadricula de 2 x 2 con tarjetas identicas ----
        abilitiesGroup = TopLeft("Habilidades", charWindow, 900f, -236f, 540f, 340f);
        abilitiesCanvas = abilitiesGroup.gameObject.AddComponent<CanvasGroup>();
        Txt(TopLeft("Titulo", abilitiesGroup, 0f, 0f, 540f, 40f), "HABILIDADES", 28f, GoldBright, TextAlignmentOptions.Midline);

        for (int i = 0; i < 4; i++)
        {
            // Sin contenedor: icono grande con el nombre debajo. La zona transparente sigue recibiendo el cursor (tooltip).
            RectTransform card = Rect("Habilidad_" + i, abilitiesGroup, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            card.anchoredPosition = new Vector2((i % 2) * 280f, -54f - (i / 2) * 150f);
            card.sizeDelta = new Vector2(260f, 142f);
            Image cardFrame = Img(card, null, new Color(Gold.r, Gold.g, Gold.b, 0f), false, 1f, true);
            abilityTips[i] = card.gameObject.AddComponent<TipZone>();
            abilityTips[i].Setup(this, "", "");
            abilityTips[i].SetHover(cardFrame, null, new Color(Gold.r, Gold.g, Gold.b, 0f), new Color(Gold.r, Gold.g, Gold.b, 0.14f), Panel, Panel);

            RectTransform icon = Rect("Icono", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            icon.anchoredPosition = new Vector2(0f, -2f);
            icon.sizeDelta = new Vector2(108f, 108f);
            abilityIcons[i] = Img(icon, null, Color.white, false, 1f, false);
            abilityIcons[i].preserveAspect = true;

            RectTransform label = Rect("Nombre", card, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            label.offsetMin = new Vector2(4f, 0f);
            label.offsetMax = new Vector2(-4f, 34f);
            abilityNames[i] = Txt(label, "", 23f, Ivory, TextAlignmentOptions.Midline);
            abilityNames[i].textWrappingMode = TextWrappingModes.Normal;
            abilityNames[i].enableAutoSizing = true;
            abilityNames[i].fontSizeMin = 17f;
            abilityNames[i].fontSizeMax = 23f;
        }

        // ---- Reliquias equipadas: dos casillas iguales, alineadas y centradas, siempre visibles ----
        RectTransform relics = FramedBox("Reliquias", charWindow, 2f, new Color(Gold.r, Gold.g, Gold.b, 0.6f), new Color32(0x14, 0x1B, 0x27, 0xFF));
        Place(relics, 430f, -612f, 1010f, 198f, 56f, -608f, 693f, 198f);
        if (relicsBackground != null)
        {
            RectTransform relicsScene = Rect("Escena", relics, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            relicsScene.offsetMin = new Vector2(2f, 2f);
            relicsScene.offsetMax = new Vector2(-2f, -2f);
            relicsScene.gameObject.AddComponent<RectMask2D>();
            RectTransform relicsStage = Rect("Escenario", relicsScene, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            relicsStage.anchoredPosition = Vector2.zero;
            relicsStage.sizeDelta = new Vector2(1010f, 198f);
            Img(relicsStage, relicsBackground, Color.white, false, 1f, false);
            AspectRatioFitter relicsFit = relicsStage.gameObject.AddComponent<AspectRatioFitter>();
            relicsFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            relicsFit.aspectRatio = relicsBackground.rect.width / relicsBackground.rect.height;
        }

        relicsTitle = Txt(Rect("Titulo", relics, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f)), "RELIQUIAS EQUIPADAS", 26f, GoldBright, TextAlignmentOptions.Midline);
        relicsTitle.rectTransform.anchoredPosition = new Vector2(0f, -6f);
        relicsTitle.rectTransform.sizeDelta = new Vector2(0f, 36f);

        for (int i = 0; i < equipSlots.Length; i++)
        {
            RectTransform slotRect = Rect("CasillaReliquia_" + i, relics, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            slotRect.anchoredPosition = new Vector2(i == 0 ? -80f : 80f, -50f);
            slotRect.sizeDelta = new Vector2(112f, 112f);
            equipSlots[i] = MakeSlot(slotRect, RelicSlotView.SlotKind.Equip, i, 16f);
        }

        RectTransform status = Rect("Estado", relics, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        status.offsetMin = new Vector2(10f, 6f);
        status.offsetMax = new Vector2(-10f, 30f);
        statusText = Txt(status, "", 20f, Bad, TextAlignmentOptions.Midline);
    }

    private void BuildInventoryWindow(RectTransform parent)
    {
        invWindow = Window("Inventario", parent, out invGroup);
        invWindow.sizeDelta = new Vector2(InventoryW, WindowH);
        BuildCloseButton(invWindow, ToggleInventory);

        // Titulo plano (sin estandarte), igual que el de la tienda: icono y texto centrados como un solo grupo.
        RectTransform chestRect = TopLeft("Icono", invWindow, 66f, -36f, 72f, 72f);
        Image chest = Img(chestRect, inventoryIcon, Color.white, false, 1f, false);
        chest.preserveAspect = true;
        TMP_Text invTitle = Txt(TopLeft("Titulo", invWindow, 150f, -36f, 600f, 72f), "INVENTARIO", 50f, Ivory, TextAlignmentOptions.MidlineLeft);
        float titleWidth = invTitle.GetPreferredValues("INVENTARIO").x;
        const float iconGap = 14f;
        float groupStart = (InventoryW - (72f + iconGap + titleWidth)) * 0.5f;
        chestRect.anchoredPosition = new Vector2(groupStart, -36f);
        invTitle.rectTransform.anchoredPosition = new Vector2(groupStart + 72f + iconGap, -36f);
        invTitle.rectTransform.sizeDelta = new Vector2(titleWidth + 8f, 72f);

        // Filtros: mismo tamano; el activo con fondo iluminado y borde dorado.
        string[] names = { "Todo", "Reliquias", "Consumibles" };
        filterFrames = new Image[names.Length];
        filterFills = new Image[names.Length];
        float filtersX = (InventoryW - (names.Length * 190f + (names.Length - 1) * 16f)) * 0.5f;

        for (int i = 0; i < names.Length; i++)
        {
            RectTransform b = FramedBox("Filtro_" + names[i], invWindow, 1.5f, FilterIdleBorder, Panel);
            b.anchoredPosition = new Vector2(filtersX + i * 206f, -122f);
            b.sizeDelta = new Vector2(190f, 46f);
            filterFrames[i] = b.GetComponent<Image>();
            filterFrames[i].raycastTarget = true;
            filterFills[i] = b.GetChild(0).GetComponent<Image>();
            RectTransform labelRect = Rect("Texto", b, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            Txt(labelRect, names[i], 25f, Ivory, TextAlignmentOptions.Midline);
            Button button = b.gameObject.AddComponent<Button>();
            button.targetGraphic = filterFrames[i];
            button.transition = Selectable.Transition.None;
            int index = i;
            button.onClick.AddListener(() => SetFilter(index));
        }

        // Cuadricula con scroll. La zona completa recibe las reliquias equipadas que se arrastran de vuelta.
        RectTransform viewport = TopLeft("Cuadricula", invWindow, 70f, -190f, 850f, 600f);
        Img(viewport, null, new Color(0f, 0f, 0f, 0f), false, 1f, true);
        viewport.gameObject.AddComponent<RectMask2D>();
        RelicSlotView zoneView = viewport.gameObject.AddComponent<RelicSlotView>();
        zoneView.Setup(this, RelicSlotView.SlotKind.InventoryZone, -1, null);

        invContent = Rect("Contenido", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
        invContent.anchoredPosition = Vector2.zero;
        invContent.sizeDelta = new Vector2(0f, 3f * (SlotSize + SlotGap));

        RectTransform bar = TopLeft("Barra", invWindow, 936f, -190f, 12f, 600f);
        Img(bar, null, new Color(Panel.r, Panel.g, Panel.b, 0.9f), false, 1f, true);
        RectTransform handle = Rect("Asa", bar, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        handle.offsetMin = handle.offsetMax = Vector2.zero;
        Image handleImage = Img(handle, null, new Color(Gold.r, Gold.g, Gold.b, 0.8f), false, 1f, true);
        Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;

        invScroll = viewport.gameObject.AddComponent<ScrollRect>();
        invScroll.viewport = viewport;
        invScroll.content = invContent;
        invScroll.horizontal = false;
        invScroll.vertical = true;
        invScroll.movementType = ScrollRect.MovementType.Clamped;
        invScroll.scrollSensitivity = 40f;
        invScroll.verticalScrollbar = scrollbar;
        invScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        EnsureInventorySlots(MinInventorySlots);
        invWindow.gameObject.SetActive(false);
    }

    private RelicSlotView MakeSlot(RectTransform rect, RelicSlotView.SlotKind kind, int index, float iconPadding)
    {
        Image frame = Img(rect, null, FilterIdleBorder, false, 1f, true);
        RectTransform inner = Rect("Fondo", rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        inner.offsetMin = new Vector2(1.5f, 1.5f);
        inner.offsetMax = new Vector2(-1.5f, -1.5f);
        Img(inner, null, Panel, false, 1f, false);

        // Simbolo discreto de casilla vacia (rombo).
        RectTransform hole = Rect("Vacio", inner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        hole.sizeDelta = new Vector2(20f, 20f);
        hole.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image holeImage = Img(hole, null, new Color(Gold.r, Gold.g, Gold.b, 0.3f), false, 1f, false);

        RectTransform iconRect = Rect("Icono", inner, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        iconRect.offsetMin = new Vector2(iconPadding, iconPadding);
        iconRect.offsetMax = new Vector2(-iconPadding, -iconPadding);
        Image icon = Img(iconRect, null, Color.white, false, 1f, false);
        icon.preserveAspect = true;
        icon.enabled = false;

        RectTransform flashRect = Rect("Destello", inner, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        flashRect.offsetMin = flashRect.offsetMax = Vector2.zero;
        Image flash = Img(flashRect, null, new Color(GoldBright.r, GoldBright.g, GoldBright.b, 0f), false, 1f, false);

        RectTransform countRect = Rect("Cantidad", inner, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        countRect.anchoredPosition = new Vector2(-8f, 6f);
        countRect.sizeDelta = new Vector2(80f, 32f);
        TMP_Text count = Txt(countRect, "", 24f, GoldBright, TextAlignmentOptions.BottomRight);
        count.gameObject.SetActive(false);

        RelicSlotView view = rect.gameObject.AddComponent<RelicSlotView>();
        view.Setup(this, kind, index, frame);
        view.SetParts(icon, holeImage, flash, count);
        return view;
    }

    private RectTransform Window(string name, RectTransform parent, out CanvasGroup group)
    {
        RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        Img(rect, windowSprite, Color.white, true, 4.4f, true);
        group = rect.gameObject.AddComponent<CanvasGroup>();
        return rect;
    }

    private void BuildCloseButton(RectTransform window, UnityEngine.Events.UnityAction action)
    {
        RectTransform close = Rect("BotonCerrar", window, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
        close.anchoredPosition = new Vector2(-39f, -34f);   // misma posicion que el boton de cerrar de la tienda
        close.sizeDelta = new Vector2(48f, 48f);
        Image image = Img(close, closeIcon, closeIcon != null ? Color.white : Gold, false, 1f, true);
        image.preserveAspect = true;
        Button button = close.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(action);
    }

    // ---- Utilidades ----

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        return rect;
    }

    // Anclado arriba a la izquierda del padre: (x, y) con y negativa hacia abajo.
    private static RectTransform TopLeft(string name, Transform parent, float x, float y, float w, float h)
    {
        RectTransform rect = Rect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    private static Image Img(RectTransform rect, Sprite sprite, Color color, bool sliced, float multiplier, bool raycast)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = multiplier;
        image.raycastTarget = raycast;
        return image;
    }

    private TMP_Text Txt(RectTransform rect, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();

        if (font != null)
            label.font = font;

        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.richText = true;
        label.raycastTarget = false;
        return label;
    }

#if UNITY_EDITOR
    public void ConfigureInEditor(HeroProfile[] profiles, TMP_FontAsset fontAsset, Sprite window, Sprite frame, Sprite frameHover, Sprite close)
    {
        heroes = profiles;
        font = fontAsset;
        windowSprite = window;
        frameSprite = frame;
        frameHoverSprite = frameHover;
        closeIcon = close;
    }
#endif
}

// Muestra un tooltip del panel al pasar el cursor (habilidades, boton de inventario).
public class TipZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string title = "";
    public string body = "";
    private CharacterPanelController owner;
    private Image frame, fill;
    private Color frameNormal, frameHover, fillNormal, fillHover;

    public void SetHover(Image frameImage, Image fillImage, Color frameColor, Color frameHoverColor, Color fillColor, Color fillHoverColor)
    {
        frame = frameImage;
        fill = fillImage;
        frameNormal = frameColor;
        frameHover = frameHoverColor;
        fillNormal = fillColor;
        fillHover = fillHoverColor;
    }

    private void Highlight(bool on)
    {
        if (frame != null)
            frame.color = on ? frameHover : frameNormal;

        if (fill != null)
            fill.color = on ? fillHover : fillNormal;
    }

    public void Setup(CharacterPanelController controller, string tipTitle, string tipBody)
    {
        owner = controller;
        title = tipTitle;
        body = tipBody;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Highlight(true);

        if (owner != null)
            owner.ShowTip(title, body);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Highlight(false);

        if (owner != null)
            owner.HideTip();
    }

    private void OnDisable()
    {
        Highlight(false);

        if (owner != null)
            owner.HideTip();
    }
}

// Casilla de reliquia: del inventario, de equipo de un personaje, o la zona del inventario (destino al quitar una
// reliquia). Los consumibles usan la misma casilla pero sin identificador de reliquia: no se pueden arrastrar.
public class RelicSlotView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    public enum SlotKind { Inventory, Equip, InventoryZone }
    public enum Highlight { None, Valid, Invalid }

    private static readonly Color Neutral = new Color(0.239f, 0.290f, 0.361f, 1f);   // borde en reposo (igual que la tienda)
    private static readonly Color HoverGold = new Color(0.62f, 0.52f, 0.34f, 1f);
    private static readonly Color Valid = new Color32(0xF0, 0xC8, 0x6E, 0xFF);
    private static readonly Color Invalid = new Color32(0xD8, 0x3A, 0x3A, 0xFF);

    private CharacterPanelController owner;
    private Image frame;
    private Image icon;
    private Image hole;
    private Image flash;
    private TMP_Text count;
    private Highlight highlight;
    private bool dragging;
    private Color baseColor = Neutral;
    private float flashT;
    private bool hoverOn;

    public SlotKind Kind { get; private set; }
    public int Index { get; private set; }
    public int Uid { get; private set; }
    public int ConsumableId { get; private set; } = -1;

    public void Setup(CharacterPanelController controller, SlotKind kind, int index, Image frameImage)
    {
        owner = controller;
        Kind = kind;
        Index = index;
        frame = frameImage;
    }

    public void SetParts(Image iconImage, Image holeImage, Image flashImage, TMP_Text countText)
    {
        icon = iconImage;
        hole = holeImage;
        flash = flashImage;
        count = countText;
    }

    public void Set(int uid, RelicData data)
    {
        Uid = uid;
        ConsumableId = -1;
        baseColor = data != null ? Color.Lerp(Neutral, RelicData.RarityColor(data.rarity), 0.55f) : Neutral;

        if (icon != null)
            icon.rectTransform.localScale = Vector3.one;

        if (icon != null)
        {
            icon.sprite = data != null ? data.icon : null;
            icon.enabled = data != null && data.icon != null;
            icon.color = dragging ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }

        if (hole != null)
            hole.enabled = data == null;

        if (count != null)
            count.gameObject.SetActive(false);

        Paint();
    }

    // Consumible (solo lectura): icono y cantidad; sin Uid, asi que nunca se arrastra.
    public void SetConsumable(int consumableId, Sprite sprite, string amount)
    {
        Uid = 0;
        ConsumableId = consumableId;
        baseColor = Neutral;

        if (icon != null)
            icon.rectTransform.localScale = Vector3.one * 1.4f;   // los iconos de consumibles tienen mas margen transparente

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.color = Color.white;
        }

        if (hole != null)
            hole.enabled = false;

        if (count != null)
        {
            count.text = amount;
            count.gameObject.SetActive(true);
        }

        Paint();
    }

    public void SetDragging(bool value)
    {
        dragging = value;

        if (icon != null)
            icon.color = value ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
    }

    public void SetHighlight(Highlight value)
    {
        if (highlight == value)
            return;

        highlight = value;
        Paint();
    }

    // Destello sutil al equipar.
    public void Flash()
    {
        flashT = 1f;
    }

    private void Update()
    {
        if (flash == null || flashT <= 0f)
            return;

        flashT = Mathf.Max(0f, flashT - Time.unscaledDeltaTime * 2.2f);
        Color c = flash.color;
        c.a = flashT * 0.5f;
        flash.color = c;
    }

    private void Paint()
    {
        if (frame == null)
            return;

        frame.color = highlight == Highlight.Valid ? Valid : (highlight == Highlight.Invalid ? Invalid : (hoverOn ? Color.Lerp(baseColor, HoverGold, 0.85f) : baseColor));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Kind != SlotKind.InventoryZone)
        {
            hoverOn = true;
            Paint();
        }

        owner.Hover(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hoverOn = false;
        Paint();
        owner.Hover(this, false);
    }

    private void OnDisable()
    {
        hoverOn = false;

        if (owner != null && Kind != SlotKind.InventoryZone)
            owner.Hover(this, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Kind == SlotKind.Equip && eventData.button == PointerEventData.InputButton.Left)
            owner.OnEquipSlotClicked();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Kind != SlotKind.InventoryZone && Uid != 0)
            owner.BeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner.Drag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Kind != SlotKind.InventoryZone)
            owner.EndDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (Kind == SlotKind.Equip)
            owner.DropOnEquip(this);
        else
            owner.DropOnInventory();   // zona del inventario o una casilla de la cuadricula
    }
}
