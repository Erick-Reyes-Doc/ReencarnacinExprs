using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Un lugar del mapa: agrupa los encuentros (o el evento / el jefe) que se
// juegan alli. Posicion normalizada con origen arriba-izquierda. Editable en
// el Inspector; mover un lugar no requiere tocar codigo.
[Serializable]
public class MapLocationDef
{
    public string id;
    public string displayName;
    public string zone;
    public Vector2 position;
    public List<string> encounterIds = new List<string>();
}

// Presentacion por lugares (Paso 61.1): marcadores pequenos integrados al
// terreno, panel contextual con los encuentros del lugar y niebla de
// bloqueo por region. La logica de seleccion, desbloqueo y carga de
// combate es la de siempre (Select / StartSelected / MapProgression).
public partial class WorldMapController
{
    [Header("Lugares (marcadores pequenos)")]
    [Tooltip("true = marcadores por lugar y panel contextual. false = nodos grandes del Paso 61 (respaldo).")]
    [SerializeField] private bool useLocationMarkers = true;
    [SerializeField] private List<MapLocationDef> locations = new List<MapLocationDef>();

    [Serializable]
    public class LocationView
    {
        public string id;
        public RectTransform rect;
        public RectTransform badge;
        public Button button;
        public Image body;
        public Image frame;
        public Image icon;
        public Image glow;
        public Image ring;
        public Text label;
        public MapHoverTarget hover;
    }

    [SerializeField] private List<LocationView> locationViews = new List<LocationView>();
    [SerializeField] private CanvasGroup fogForest;
    [SerializeField] private CanvasGroup fogCorruption;
    [SerializeField] private Button closeLayerButton;

    [Header("Panel de lugar")]
    [SerializeField] private RectTransform placePanel;
    [SerializeField] private RectTransform placeDetails;
    [SerializeField] private Text placeTitle;
    [SerializeField] private Text placeZone;
    [SerializeField] private Button placeCloseButton;
    [SerializeField] private RectTransform[] rowRoots = new RectTransform[2];
    [SerializeField] private Image[] rowBackgrounds = new Image[2];
    [SerializeField] private Button[] rowButtons = new Button[2];
    [SerializeField] private Image[] rowIcons = new Image[2];
    [SerializeField] private Text[] rowNames = new Text[2];
    [SerializeField] private Text[] rowStates = new Text[2];

    private string openLocationId;
    private string hoveredLocationId;

    private const float PlaceW = 520f;
    private const float PlaceHead = 84f;
    private const float PlaceRow = 58f;
    private const float PlaceDetailsH = 178f;

    private enum PlaceState { Locked, Available, Recommended, Completed }

    public IReadOnlyList<MapLocationDef> Locations => locations;
    public IReadOnlyList<LocationView> LocationViews => locationViews;
    public string OpenLocationId => openLocationId;

    // Lugares por defecto (nombres provisionales aprobados).
    private static List<MapLocationDef> DefaultLocations()
    {
        return new List<MapLocationDef>
        {
            // Centro de cada cartel del mapa ilustrado (mapa.png, 1672x941).
            Loc("camp", "Campamento del Alba", MapProgression.Meadow, 0.0957f, 0.7513f, "P1", "P2"),
            Loc("ruins", "Ruinas de Liria", MapProgression.Meadow, 0.2751f, 0.4251f, "P3", "P4"),
            Loc("clearing", "Claro del Susurro", MapProgression.Forest, 0.3678f, 0.5866f, "B1"),
            Loc("shrine", "Santuario Velado", MapProgression.Forest, 0.5096f, 0.4421f, GameSession.NoelleEventId),
            Loc("trail", "Sendero Sombrío", MapProgression.Forest, 0.6776f, 0.4931f, "B2", "B3"),
            Loc("wastes", "Tierras Marchitas", MapProgression.Corruption, 0.8523f, 0.5494f, "C1", "C2"),
            Loc("gate", "Trono Sellado", MapProgression.Corruption, 0.9150f, 0.2040f, BossId),
        };
    }

    private static MapLocationDef Loc(string id, string name, string zone, float x, float y, params string[] encounterIds)
    {
        MapLocationDef def = new MapLocationDef { id = id, displayName = name, zone = zone, position = new Vector2(x, y) };
        def.encounterIds.AddRange(encounterIds);
        return def;
    }

    private static Vector2 LocationPx(MapLocationDef def)
    {
        return new Vector2(def.position.x * 1920f, (1f - def.position.y) * 1080f);
    }

    private MapLocationDef FindLocation(string id)
    {
        foreach (MapLocationDef def in locations)
            if (def.id == id) return def;

        return null;
    }

    private MapLocationDef LocationOf(string encounterId)
    {
        foreach (MapLocationDef def in locations)
            if (def.encounterIds.Contains(encounterId)) return def;

        return null;
    }

    // ------------------------------------------------------------
    // Seleccion (API publica)
    // ------------------------------------------------------------

    // Abre el panel del lugar y deja seleccionado su encuentro mas util:
    // el recomendado, si no el primero disponible, si no el primero.
    public bool SelectLocation(string id)
    {
        if (loading || eventOpen || (shop != null && shop.IsOpen))
            return false;

        MapLocationDef def = FindLocation(id);

        if (def == null || def.encounterIds.Count == 0)
            return false;

        string pick = null;
        string recommended = MapProgression.NextRecommended();

        foreach (string eid in def.encounterIds)
            if (eid == recommended) pick = eid;

        if (pick == null)
            foreach (string eid in def.encounterIds)
                if (eid != BossId && MapProgression.GetState(eid) == MapProgression.NodeState.Available)
                {
                    pick = eid;
                    break;
                }

        openLocationId = id;
        selectedId = pick ?? def.encounterIds[0];
        return true;
    }

    public void CloseLocation()
    {
        openLocationId = null;
    }

    private void ToggleLocation(string id)
    {
        if (openLocationId == id)
            CloseLocation();
        else
            SelectLocation(id);
    }

    private void SelectRow(int index)
    {
        MapLocationDef def = FindLocation(openLocationId);

        if (def != null && index >= 0 && index < def.encounterIds.Count)
            Select(def.encounterIds[index]);
    }

    private void WireLocationButtons()
    {
        foreach (LocationView view in locationViews)
        {
            string id = view.id;
            view.button.onClick.RemoveAllListeners();
            view.button.onClick.AddListener(() => ToggleLocation(id));

            if (view.hover != null)
            {
                view.hover.entered = () => hoveredLocationId = id;
                view.hover.exited = () => { if (hoveredLocationId == id) hoveredLocationId = null; };
            }
        }

        if (placeCloseButton != null)
        {
            placeCloseButton.onClick.RemoveAllListeners();
            placeCloseButton.onClick.AddListener(CloseLocation);
        }

        if (closeLayerButton != null)
        {
            closeLayerButton.onClick.RemoveAllListeners();
            closeLayerButton.onClick.AddListener(CloseLocation);
        }

        for (int i = 0; i < rowButtons.Length; i++)
        {
            if (rowButtons[i] == null)
                continue;

            int index = i;
            rowButtons[i].onClick.RemoveAllListeners();
            rowButtons[i].onClick.AddListener(() => SelectRow(index));
        }
    }

    // ------------------------------------------------------------
    // Estado visual de los lugares
    // ------------------------------------------------------------

    private PlaceState StateOfLocation(MapLocationDef def, string recommended)
    {
        if (def.encounterIds.Contains(BossId))
            return PlaceState.Locked;

        bool anyAvailable = false, allDone = true, hasRecommended = false;

        foreach (string eid in def.encounterIds)
        {
            MapProgression.NodeState s = MapProgression.GetState(eid);

            if (s != MapProgression.NodeState.Completed) allDone = false;
            if (s == MapProgression.NodeState.Available) anyAvailable = true;
            if (eid == recommended) hasRecommended = true;
        }

        if (allDone) return PlaceState.Completed;
        if (hasRecommended) return PlaceState.Recommended;
        return anyAvailable ? PlaceState.Available : PlaceState.Locked;
    }

    private void RefreshLocations(string recommended)
    {
        if (!useLocationMarkers || locationViews.Count == 0)
            return;

        foreach (LocationView view in locationViews)
        {
            MapLocationDef def = FindLocation(view.id);

            if (def == null)
                continue;

            bool evt = def.encounterIds.Contains(GameSession.NoelleEventId);
            bool boss = def.encounterIds.Contains(BossId);
            PlaceState state = StateOfLocation(def, recommended);
            bool open = openLocationId == view.id;
            bool hover = hoveredLocationId == view.id;

            Color body, frame;
            Sprite icon;
            Color iconColor = Color.white;

            if (boss)
            {
                body = new Color(0.24f, 0.12f, 0.26f, 1f); frame = new Color(0.62f, 0.30f, 0.62f, 1f);
                icon = IcSkull; iconColor = new Color(0.90f, 0.75f, 0.95f, 0.85f);
            }
            else if (state == PlaceState.Completed)
            {
                body = new Color(0.22f, 0.56f, 0.36f, 1f); frame = new Color(0.68f, 0.92f, 0.72f, 1f);
                icon = IcCheck;
            }
            else if (state == PlaceState.Locked)
            {
                body = new Color(0.20f, 0.21f, 0.26f, 1f); frame = new Color(0.40f, 0.41f, 0.48f, 1f);
                icon = IcLock; iconColor = new Color(0.78f, 0.80f, 0.88f, 0.9f);
            }
            else if (evt)
            {
                body = new Color(0.52f, 0.30f, 0.80f, 1f); frame = new Color(0.88f, 0.74f, 1f, 1f);
                icon = IcMask;
            }
            else if (state == PlaceState.Recommended)
            {
                body = new Color(0.90f, 0.66f, 0.18f, 1f); frame = new Color(1f, 0.93f, 0.62f, 1f);
                icon = IcStar;
            }
            else
            {
                body = new Color(0.26f, 0.50f, 0.80f, 1f); frame = new Color(0.84f, 0.92f, 1f, 1f);
                icon = SpCircle; iconColor = new Color(1f, 1f, 1f, 0.9f);
            }

            view.body.color = body;
            view.frame.color = frame;

            if (view.icon.sprite != icon) view.icon.sprite = icon;
            view.icon.color = iconColor;
            bool compact = view.badge != null;
            float iconSize = compact ? (icon == SpCircle ? 8f : 17f) : (icon == SpCircle ? 12f : 26f);
            view.icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);

            bool showRing = open || state == PlaceState.Recommended;
            if (view.ring.gameObject.activeSelf != showRing)
                view.ring.gameObject.SetActive(showRing);
            view.ring.color = open ? Color.white : new Color(1f, 0.85f, 0.35f, 0.85f);

            float glow = compact ? (hover ? 0.42f : (open ? 0.30f : 0f)) : (hover ? 0.85f : (open ? 0.55f : 0f));
            if (!Mathf.Approximately(view.glow.color.a, glow))
                view.glow.color = new Color(1f, 0.88f, 0.55f, glow);

            float pulse = state == PlaceState.Recommended && Application.isPlaying
                ? 1f + 0.07f * Mathf.Sin(Time.unscaledTime * 3.2f) : 1f;
            (compact ? view.badge : view.rect).localScale = Vector3.one * ((hover || open) ? 1.12f : 1f) * pulse;

            view.label.color = state == PlaceState.Locked
                ? new Color(0.80f, 0.82f, 0.90f, 0.78f) : new Color(1f, 0.97f, 0.88f, 1f);
        }

        UpdateFog();
        RefreshPlacePanel();
    }

    private void UpdateFog()
    {
        FadeFog(fogForest, MapProgression.IsZoneUnlocked(MapProgression.Forest) ? 0f : 1f);
        FadeFog(fogCorruption, MapProgression.IsZoneUnlocked(MapProgression.Corruption) ? 0f : 1f);
    }

    private static void FadeFog(CanvasGroup group, float target)
    {
        if (group == null)
            return;

        group.alpha = Application.isPlaying
            ? Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 1.2f)
            : target;
    }

    // ------------------------------------------------------------
    // Panel contextual del lugar
    // ------------------------------------------------------------

    private string EncounterTitle(string id)
    {
        if (id == BossId) return "Jefe";
        if (MapProgression.IsEvent(id)) return eventTitle;

        EncounterData data = FindEncounter(id);
        return data != null ? data.displayName : id;
    }

    private void RefreshPlacePanel()
    {
        if (placePanel == null)
            return;

        MapLocationDef def = FindLocation(openLocationId);

        if (def == null)
        {
            if (placePanel.gameObject.activeSelf) placePanel.gameObject.SetActive(false);
            return;
        }

        if (!placePanel.gameObject.activeSelf)
            placePanel.gameObject.SetActive(true);

        // El encuentro seleccionado siempre pertenece al lugar abierto.
        if (!def.encounterIds.Contains(selectedId))
            selectedId = def.encounterIds[0];

        int rows = Mathf.Min(def.encounterIds.Count, rowRoots.Length);

        SetText(placeTitle, def.displayName);
        SetText(placeZone, "Región: " + ZoneName(def.zone));

        for (int i = 0; i < rowRoots.Length; i++)
        {
            bool visible = i < rows;

            if (rowRoots[i].gameObject.activeSelf != visible)
                rowRoots[i].gameObject.SetActive(visible);

            if (!visible)
                continue;

            string eid = def.encounterIds[i];
            bool boss = eid == BossId;
            bool evt = MapProgression.IsEvent(eid);
            MapProgression.NodeState s = boss ? MapProgression.NodeState.Locked : MapProgression.GetState(eid);
            bool isRecommended = eid == MapProgression.NextRecommended();

            Sprite icon; Color iconColor = Color.white;
            if (boss) { icon = IcSkull; iconColor = new Color(0.9f, 0.75f, 0.95f, 0.85f); }
            else if (s == MapProgression.NodeState.Completed) { icon = IcCheck; iconColor = new Color(0.55f, 0.95f, 0.62f, 1f); }
            else if (s == MapProgression.NodeState.Locked) { icon = IcLock; iconColor = new Color(0.72f, 0.74f, 0.82f, 1f); }
            else if (evt) { icon = IcMask; iconColor = new Color(0.85f, 0.70f, 1f, 1f); }
            else if (isRecommended) { icon = IcStar; iconColor = new Color(1f, 0.85f, 0.35f, 1f); }
            else { icon = SpCircle; iconColor = new Color(0.55f, 0.75f, 1f, 1f); }

            rowIcons[i].sprite = icon;
            rowIcons[i].color = iconColor;
            SetText(rowNames[i], EncounterTitle(eid));
            rowNames[i].color = s == MapProgression.NodeState.Locked ? DimText : TextColor;
            SetText(rowStates[i], StateText(eid));
            rowStates[i].color = s == MapProgression.NodeState.Locked ? DimText : new Color(0.85f, 0.9f, 1f, 1f);

            bool selected = eid == selectedId;
            rowBackgrounds[i].color = selected ? new Color(0.22f, 0.27f, 0.40f, 1f) : new Color(0.10f, 0.12f, 0.19f, 1f);
        }

        // Altura segun el numero de filas y posicion junto al marcador.
        float detailsTop = PlaceHead + rows * PlaceRow + 8f;
        placeDetails.anchoredPosition = new Vector2(20f, -detailsTop);
        float height = detailsTop + PlaceDetailsH + 16f + 54f + 16f;
        placePanel.sizeDelta = new Vector2(PlaceW, height);

        placePanel.anchoredPosition = PlacePanelCenter(def, height);
    }

    // Centro del panel junto al lugar abierto: prueba ambos lados y tres
    // alturas, descarta lo que se sale de pantalla y elige lo que menos tapa
    // otros destinos (prefiere el lado con mas espacio).
    private Vector2 PlacePanelCenter(MapLocationDef def, float height)
    {
        Vector2 marker = LocationPx(def);
        float gap = (Illustrated ? 140f : 118f) + PlaceW * 0.5f;
        float minX = 20f + PlaceW * 0.5f, maxX = 1900f - PlaceW * 0.5f;
        float minY = 20f + height * 0.5f, maxY = 1080f - 76f - height * 0.5f;
        float preferred = marker.x < 960f ? 1f : -1f;

        Vector2 best = Vector2.zero;
        float bestScore = float.MaxValue;

        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? preferred : -preferred;
            float cx = Mathf.Clamp(marker.x + side * gap, minX, maxX);

            for (int v = 0; v < 3; v++)
            {
                float target = v == 0 ? marker.y : v == 1 ? marker.y - height * 0.5f + 60f : marker.y + height * 0.5f - 60f;
                float cy = Mathf.Clamp(target, minY, maxY);
                Rect panelRect = new Rect(cx - PlaceW * 0.5f - 8f, cy - height * 0.5f - 8f, PlaceW + 16f, height + 16f);
                float score = (s == 0 ? 0f : 1f) + (v == 0 ? 0f : 0.5f);

                // El propio lugar y sus vecinos no deben quedar tapados.
                foreach (MapLocationDef other in locations)
                {
                    Vector2 o = LocationPx(other);
                    Rect bannerRect = new Rect(o.x - 115f, o.y - 28f, 230f, 56f);
                    if (panelRect.Overlaps(bannerRect)) score += other == def ? 5000f : 1000f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = new Vector2(cx, cy);
                }
            }
        }

        return best;
    }

    // ------------------------------------------------------------
    // Construccion
    // ------------------------------------------------------------

    private Text TopLabel(string name, Transform parent, float x, float y, float w, float h, int size, TextAnchor anchor, Color color)
    {
        Text text = Label(name, parent, Vector2.zero, Vector2.zero, size, anchor, color);
        TopRect((RectTransform)text.transform, x, y, w, h);
        return text;
    }

    private static void TopRect(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    private void BuildLocations(RectTransform area)
    {
        if (locations.Count == 0)
            locations = DefaultLocations();

        locationViews.Clear();

        // Marcadores interactivos: capa de cierre + lugares.
        RectTransform markers = Group("MarcadoresInteractivos", area);

        // Capa que cierra el panel al pulsar el paisaje.
        RectTransform closeLayer = Box("CerrarLugar", markers, Vector2.zero, Vector2.one, Clear0);
        closeLayer.GetComponent<Image>().raycastTarget = true;
        closeLayerButton = closeLayer.gameObject.AddComponent<Button>();
        closeLayerButton.targetGraphic = closeLayer.GetComponent<Image>();
        closeLayerButton.transition = Selectable.Transition.None;

        Transform parent = Group("Lugares", markers);
        bool illustrated = Illustrated;

        foreach (MapLocationDef def in locations)
        {
            Vector2 p = LocationPx(def);
            bool evt = def.encounterIds.Contains(GameSession.NoelleEventId);
            bool boss = def.encounterIds.Contains(BossId);
            float frameSize = illustrated ? (evt || boss ? 34f : 30f) : (evt ? 58f : boss ? 62f : 50f);
            float bodySize = frameSize - (illustrated ? 6f : 10f);
            Sprite shape = evt ? SpDiamond : boss ? SpHex : SpCircle;

            RectTransform container = Place("Lugar_" + def.id, parent, p, new Vector2(240f, 120f), Clear0, null);

            // Con arte ilustrado el cartel con el nombre ya esta pintado: el
            // marcador es un indicador pequeno junto a su borde derecho y la
            // zona de clic cubre el cartel.
            RectTransform badge = container;
            if (illustrated)
            {
                badge = Local("Indicador", container, new Vector2(112f, 0f), new Vector2(frameSize + 20f, frameSize + 20f), Clear0, null);
            }

            Local("Sombra", badge, new Vector2(0f, -4f), new Vector2(frameSize + 10f, frameSize * 0.5f), new Color(0f, 0f, 0f, illustrated ? 0.4f : 0.5f), SpSoft);
            RectTransform glow = illustrated
                ? Local("Brillo", container, Vector2.zero, new Vector2(236f, 66f), new Color(1f, 0.88f, 0.55f, 0f), SpSoft)
                : Local("Brillo", container, Vector2.zero, new Vector2(frameSize + 56f, frameSize + 56f), new Color(1f, 0.88f, 0.55f, 0f), SpSoft);
            RectTransform ring = Local("Anillo", badge, Vector2.zero, new Vector2(frameSize + 14f, frameSize + 14f), Color.white, SpRing);
            RectTransform frame = Local("Marco", badge, Vector2.zero, new Vector2(frameSize, frameSize), GoldLine, shape);
            RectTransform body = Local("Cuerpo", badge, Vector2.zero, new Vector2(bodySize, bodySize), LockedColor, shape);
            RectTransform icon = Local("Icono", body, Vector2.zero, new Vector2(26f, 26f), Color.white, IcLock);

            // Zona de clic algo mayor que el dibujo para que sea facil de pulsar.
            RectTransform hit = illustrated
                ? Local("Zona", container, Vector2.zero, new Vector2(240f, 72f), new Color(1f, 1f, 1f, 0.004f), SpCircle)
                : Local("Zona", container, Vector2.zero, new Vector2(frameSize + 26f, frameSize + 26f), new Color(1f, 1f, 1f, 0.004f), SpCircle);
            Image hitImage = hit.GetComponent<Image>();
            hitImage.raycastTarget = true;
            Button button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hitImage;
            button.transition = Selectable.Transition.None;
            MapHoverTarget hover = hit.gameObject.AddComponent<MapHoverTarget>();

            Text label = Shadowed(Label("Nombre", container, Vector2.zero, Vector2.zero, 18, TextAnchor.MiddleCenter, new Color(1f, 0.97f, 0.88f, 1f)));
            RectTransform lr = (RectTransform)label.transform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(236f, 26f);
            lr.anchoredPosition = new Vector2(0f, -(frameSize * 0.5f + 18f));
            label.text = def.displayName;
            label.fontStyle = FontStyle.Bold;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            ring.gameObject.SetActive(false);

            // El nombre ya viene pintado en la ilustracion: sin segundo rotulo.
            if (illustrated)
                label.gameObject.SetActive(false);

            locationViews.Add(new LocationView
            {
                id = def.id, rect = container, badge = illustrated ? badge : null, button = button,
                body = body.GetComponent<Image>(), frame = frame.GetComponent<Image>(),
                icon = icon.GetComponent<Image>(), glow = glow.GetComponent<Image>(),
                ring = ring.GetComponent<Image>(), label = label, hover = hover
            });
        }

        BuildPlacePanel(Group("PanelesUI", area));
    }

    private void BuildPlacePanel(RectTransform area)
    {
        RectTransform panel = Panel("PanelLugar", area, Vector2.zero, Vector2.zero, new Color(0.050f, 0.062f, 0.100f, 1f), GoldLine);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PlaceW, 420f);
        panel.GetComponent<Image>().raycastTarget = true;
        placePanel = panel;

        RectTransform accent = NewRect("Acento", panel, out Image accentImage);
        accent.anchorMin = new Vector2(0, 0); accent.anchorMax = new Vector2(0, 1);
        accent.offsetMin = new Vector2(8, 16); accent.offsetMax = new Vector2(14, -16);
        accentImage.color = GoldLine;
        infoAccent = accentImage;

        placeTitle = Shadowed(TopLabel("Titulo", panel, 20f, 10f, 420f, 38f, 30, TextAnchor.MiddleLeft, GoldBright));
        placeTitle.fontStyle = FontStyle.Bold;
        placeZone = TopLabel("Region", panel, 20f, 48f, 420f, 24f, 18, TextAnchor.MiddleLeft, DimText);

        placeCloseButton = MakeButton("Cerrar", panel, Vector2.zero, Vector2.zero, "X", 22, out _);
        TopRect((RectTransform)placeCloseButton.transform, PlaceW - 20f - 40f, 14f, 40f, 40f);

        RectTransform divider = NewRect("Separador", panel, out Image dividerImage);
        TopRect(divider, 20f, PlaceHead - 8f, PlaceW - 40f, 2f);
        dividerImage.color = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.55f);

        for (int i = 0; i < 2; i++)
        {
            RectTransform row = Panel("Fila_" + i, panel, Vector2.zero, Vector2.zero, new Color(0.10f, 0.12f, 0.19f, 1f), new Color(0.30f, 0.34f, 0.46f, 0.8f));
            TopRect(row, 20f, PlaceHead + i * PlaceRow, PlaceW - 40f, 52f);
            Image rowBg = row.GetComponent<Image>();
            rowBg.raycastTarget = true;
            Button rowButton = row.gameObject.AddComponent<Button>();
            rowButton.targetGraphic = rowBg;
            ColorBlock colors = rowButton.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.0f, 1.0f, 1.0f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            rowButton.colors = colors;

            RectTransform iconRect = NewRect("Icono", row, out Image iconImage);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(14f, 0f);
            iconRect.sizeDelta = new Vector2(30f, 30f);

            Text rowName = Label("Nombre", row, new Vector2(0, 0), new Vector2(0.70f, 1), 18, TextAnchor.MiddleLeft, TextColor);
            ((RectTransform)rowName.transform).offsetMin = new Vector2(54f, 0f);
            rowName.horizontalOverflow = HorizontalWrapMode.Overflow;
            Text rowState = Label("Estado", row, new Vector2(0.55f, 0), new Vector2(1, 1), 15, TextAnchor.MiddleRight, TextColor);
            ((RectTransform)rowState.transform).offsetMax = new Vector2(-12f, 0f);
            rowState.horizontalOverflow = HorizontalWrapMode.Overflow;

            rowRoots[i] = row;
            rowBackgrounds[i] = rowBg;
            rowButtons[i] = rowButton;
            rowIcons[i] = iconImage;
            rowNames[i] = rowName;
            rowStates[i] = rowState;
        }

        // Detalle del encuentro seleccionado (textos que ya escribe RefreshInfo).
        RectTransform details = new GameObject("Detalles", typeof(RectTransform)).GetComponent<RectTransform>();
        details.SetParent(panel, false);
        TopRect(details, 20f, 200f, PlaceW - 40f, PlaceDetailsH);
        placeDetails = details;

        float w = PlaceW - 40f;
        infoTitle = Shadowed(TopLabel("Titulo", details, 0f, 0f, w, 34f, 24, TextAnchor.MiddleLeft, GoldBright));
        infoTitle.fontStyle = FontStyle.Bold;
        infoSubtitle = TopLabel("Subtitulo", details, 0f, 36f, w, 26f, 18, TextAnchor.MiddleLeft, new Color(0.84f, 0.89f, 1f, 1f));
        infoRewards = TopLabel("Recompensas", details, 0f, 64f, w, 28f, 20, TextAnchor.MiddleLeft, new Color(1f, 0.88f, 0.52f, 1f));
        infoRewards.fontStyle = FontStyle.Bold;
        infoEnemies = TopLabel("Enemigos", details, 0f, 94f, w, 46f, 18, TextAnchor.UpperLeft, new Color(0.78f, 0.84f, 0.95f, 1f));
        infoNote = TopLabel("Nota", details, 0f, 142f, w, 34f, 16, TextAnchor.UpperLeft, DimText);
        infoNote.fontStyle = FontStyle.Italic;

        startButton = MakeButton("IniciarCombate", panel, Vector2.zero, Vector2.zero, "Iniciar combate", 26, out startButtonLabel);
        RectTransform sr = (RectTransform)startButton.transform;
        sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 0f);
        sr.pivot = new Vector2(0.5f, 0f);
        sr.offsetMin = new Vector2(20f, 16f); sr.offsetMax = new Vector2(-20f, 70f);
        startButton.targetGraphic.raycastTarget = true;

        panel.gameObject.SetActive(false);
    }

    // ---------------- Niebla de bloqueo ----------------

    // Con la ilustracion la niebla es una capa independiente y ligera: el
    // arte sigue legible por debajo.
    private bool Illustrated => art != null && art.worldBase != null;

    private CanvasGroup FogGroup(string name, Transform parent, Sprite fullFrame, Color tint, float[][] blobs)
    {
        if (Illustrated)
            tint.a *= 0.85f;

        RectTransform root = Group(name, parent);
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        if (fullFrame != null)
        {
            Box("NieblaIlustrada", root, Vector2.zero, Vector2.one, Color.white, fullFrame);
            return group;
        }

        foreach (float[] b in blobs)
            Place("Bruma", root, new Vector2(b[0], b[1]), new Vector2(b[2], b[3]), new Color(tint.r, tint.g, tint.b, tint.a * b[4]), SpSoft);

        return group;
    }

    private void BuildFog(RectTransform area)
    {
        Transform parent = Group("NieblaBloqueo", area);

        fogForest = FogGroup("Niebla_Bosque", parent, art != null ? art.fogLockForest : null,
            new Color(0.46f, 0.60f, 0.66f, 0.40f), new[]
            {
                new[] { 760f, 270f, 760f, 460f, 1f }, new[] { 880f, 650f, 800f, 520f, 1f }, new[] { 980f, 900f, 760f, 420f, 0.9f },
                new[] { 1040f, 430f, 860f, 560f, 1f }, new[] { 1150f, 720f, 800f, 500f, 1f }, new[] { 1230f, 300f, 700f, 460f, 0.9f },
                new[] { 940f, 130f, 760f, 380f, 0.9f }, new[] { 1170f, 960f, 660f, 380f, 0.8f }
            });

        fogCorruption = FogGroup("Niebla_Corrupcion", parent, art != null ? art.fogLockCorruption : null,
            new Color(0.24f, 0.15f, 0.34f, 0.50f), new[]
            {
                new[] { 1440f, 320f, 800f, 480f, 1f }, new[] { 1600f, 640f, 860f, 540f, 1f }, new[] { 1740f, 900f, 780f, 440f, 0.9f },
                new[] { 1790f, 430f, 840f, 540f, 1f }, new[] { 1520f, 920f, 720f, 400f, 0.9f }, new[] { 1860f, 210f, 700f, 420f, 0.9f },
                new[] { 1630f, 150f, 740f, 400f, 0.9f }, new[] { 1420f, 640f, 640f, 420f, 0.8f }
            });
    }
}
