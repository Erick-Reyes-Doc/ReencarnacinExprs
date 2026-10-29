using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Presentacion del mapa del mundo (Paso 61). Solo construye y colorea la
// interfaz; la logica (seleccion, desbloqueos, tienda, evento, carga de
// escenas) vive en WorldMapController. Los recursos salen de MapArtSet para
// poder sustituirlos por arte final sin tocar este codigo.
//
// Capas dentro de MapArea (1920x1080 fijo): Terreno > Conexiones > Nodos.
// Fuera de MapArea: BarraSuperior, PanelInfo y EventoVelo.
public partial class WorldMapController
{
    [Header("Arte provisional")]
    [SerializeField] private MapArtSet art;
    [SerializeField] private Image infoAccent;

    // Paleta del mapa.
    private static readonly Color Navy = new Color(0.050f, 0.062f, 0.100f, 0.96f);
    private static readonly Color ChipColor = new Color(0.090f, 0.112f, 0.180f, 1f);
    private static readonly Color GoldBright = new Color(0.96f, 0.80f, 0.42f, 1f);
    private static readonly Color GoldLine = new Color(0.78f, 0.62f, 0.30f, 0.85f);
    private static readonly Color Clear0 = new Color(0f, 0f, 0f, 0f);

    // Sprites con respaldo si el MapArtSet no esta asignado.
    private Sprite SpCircle => art != null ? art.circle : circleSprite;
    private Sprite SpRing => art != null ? art.ring : null;
    private Sprite SpSoft => art != null ? art.softCircle : circleSprite;
    private Sprite SpDiamond => art != null ? art.diamond : circleSprite;
    private Sprite SpHex => art != null ? art.hexagon : circleSprite;
    private Sprite SpTri => art != null ? art.triangle : null;
    private Sprite SpRound => art != null ? art.roundedRect : null;
    private Sprite SpFrame => art != null ? art.frame : null;
    private Sprite SpGrad => art != null ? art.gradient : null;
    private Sprite SpDash => art != null ? art.dash : null;
    private Sprite IcLock => art != null ? art.lockIcon : null;
    private Sprite IcCheck => art != null ? art.checkIcon : null;
    private Sprite IcStar => art != null ? art.starIcon : null;
    private Sprite IcMask => art != null ? art.maskIcon : null;
    private Sprite IcSkull => art != null ? art.skullIcon : null;
    private Sprite IcPotion => art != null ? art.potionIcon : null;
    private Sprite IcAntidote => art != null ? art.antidoteIcon : null;

    // ------------------------------------------------------------
    // Utilidades de UI
    // ------------------------------------------------------------

    private static RectTransform NewRect(string name, Transform parent, out Image image)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private RectTransform Box(string name, Transform parent, Vector2 min, Vector2 max, Color color, Sprite sprite = null)
    {
        RectTransform rect = NewRect(name, parent, out Image image);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        image.color = color;
        image.sprite = sprite;
        return rect;
    }

    // Posicion absoluta dentro de MapArea (origen abajo a la izquierda).
    private RectTransform Place(string name, Transform parent, Vector2 center, Vector2 size, Color color, Sprite sprite, float rotation = 0f, bool sliced = false)
    {
        RectTransform rect = NewRect(name, parent, out Image image);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = center;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        image.color = color;
        image.sprite = sprite;
        if (sliced && sprite != null) image.type = Image.Type.Sliced;
        return rect;
    }

    // Posicion relativa al centro del padre.
    private RectTransform Local(string name, Transform parent, Vector2 offset, Vector2 size, Color color, Sprite sprite)
    {
        RectTransform rect = NewRect(name, parent, out Image image);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
        image.color = color;
        image.sprite = sprite;
        return rect;
    }

    // Panel con esquinas redondeadas (9-slice) y borde opcional.
    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color, Color frameColor, bool frame = true)
    {
        RectTransform rect = NewRect(name, parent, out Image image);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        image.color = color;
        image.sprite = SpRound;
        if (SpRound != null) image.type = Image.Type.Sliced;

        if (frame && SpFrame != null)
        {
            RectTransform border = NewRect("Borde", rect, out Image borderImage);
            Stretch(border);
            borderImage.sprite = SpFrame;
            borderImage.type = Image.Type.Sliced;
            borderImage.color = frameColor;
        }

        return rect;
    }

    private Text Label(string name, Transform parent, Vector2 min, Vector2 max, int size, TextAnchor anchor, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = new Vector2(6, 2); rect.offsetMax = new Vector2(-6, -2);
        Text text = go.GetComponent<Text>();
        text.font = font; text.fontSize = size; text.alignment = anchor;
        text.color = color; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Text Shadowed(Text text)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.01f, 0.02f, 0.04f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }

    private Button MakeButton(string name, Transform parent, Vector2 min, Vector2 max, string label, int size, out Text labelText)
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

        labelText = Label("Texto", rect, Vector2.zero, Vector2.one, size, TextAnchor.MiddleCenter, TextColor);
        labelText.text = label;
        labelText.fontStyle = FontStyle.Bold;
        return button;
    }

    // ------------------------------------------------------------
    // Definicion del mapa
    // ------------------------------------------------------------

    private struct NodeDef
    {
        public string id, zone;
        public float x, y;
        public NodeDef(string id, string zone, float x, float y)
        { this.id = id; this.zone = zone; this.x = x; this.y = y; }
    }

    // Camino de izquierda a derecha, una region por tercio.
    private static readonly NodeDef[] NodeDefs =
    {
        new NodeDef("P1", MapProgression.Meadow, 0.080f, 0.34f),
        new NodeDef("P2", MapProgression.Meadow, 0.145f, 0.62f),
        new NodeDef("P3", MapProgression.Meadow, 0.215f, 0.38f),
        new NodeDef("P4", MapProgression.Meadow, 0.285f, 0.60f),
        new NodeDef("B1", MapProgression.Forest, 0.380f, 0.345f),
        new NodeDef(GameSession.NoelleEventId, MapProgression.Forest, 0.442f, 0.515f),
        new NodeDef("B2", MapProgression.Forest, 0.485f, 0.62f),
        new NodeDef("B3", MapProgression.Forest, 0.585f, 0.40f),
        new NodeDef("C1", MapProgression.Corruption, 0.715f, 0.62f),
        new NodeDef("C2", MapProgression.Corruption, 0.815f, 0.36f),
        new NodeDef(BossId, MapProgression.Corruption, 0.925f, 0.58f),
    };

    private static Vector2 Px(NodeDef def) { return new Vector2(def.x * 1920f, def.y * 1080f); }

    private static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    // Recorrido natural entre los lugares (puntos en px de MapArea).
    private readonly List<Vector2> pathPoints = new List<Vector2>();

    // Spline de Catmull-Rom por los lugares en orden de progresion.
    private void ComputePath()
    {
        pathPoints.Clear();

        if (locations.Count < 2)
            return;

        List<Vector2> pts = new List<Vector2>();
        foreach (MapLocationDef def in locations)
            pts.Add(LocationPx(def));

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
            int steps = Mathf.Max(6, Mathf.RoundToInt(Vector2.Distance(p1, p2) / 26f));

            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                pathPoints.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }

        pathPoints.Add(pts[pts.Count - 1]);
    }

    // true si el punto no tapa lugares ni caminos (para el decorado).
    private bool FreeSpot(Vector2 p, float nodeRadius, float pathRadius)
    {
        if (useLocationMarkers && pathPoints.Count > 1)
        {
            foreach (MapLocationDef def in locations)
                if (Vector2.Distance(p, LocationPx(def)) < nodeRadius) return false;

            for (int i = 0; i < pathPoints.Count - 1; i++)
                if (DistSeg(p, pathPoints[i], pathPoints[i + 1]) < pathRadius) return false;

            return true;
        }

        foreach (NodeDef d in NodeDefs)
            if (Vector2.Distance(p, Px(d)) < nodeRadius) return false;

        for (int i = 0; i < NodeDefs.Length - 1; i++)
            if (DistSeg(p, Px(NodeDefs[i]), Px(NodeDefs[i + 1])) < pathRadius) return false;

        return true;
    }

    private static float Rf(System.Random rng, float a, float b)
    {
        return a + (float)rng.NextDouble() * (b - a);
    }

    // ------------------------------------------------------------
    // Construccion
    // ------------------------------------------------------------

    public void BuildUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        nodes.Clear(); links.Clear(); regions.Clear();
        infoAccent = null;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasGo = new GameObject("WorldMapCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        // Expand: el lienzo 1920x1080 siempre cabe entero en pantalla.
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        Box("FondoGeneral", canvasGo.transform, Vector2.zero, Vector2.one, new Color(0.035f, 0.042f, 0.070f, 1f));

        GameObject areaGo = new GameObject("MapArea", typeof(RectTransform));
        areaGo.transform.SetParent(canvasGo.transform, false);
        RectTransform area = (RectTransform)areaGo.transform;
        area.anchorMin = area.anchorMax = new Vector2(0.5f, 0.5f);
        area.pivot = new Vector2(0.5f, 0.5f);
        area.sizeDelta = new Vector2(1920, 1080);
        area.anchoredPosition = Vector2.zero;

        locationViews.Clear();
        if (useLocationMarkers)
        {
            if (locations.Count == 0)
                locations = DefaultLocations();

            ComputePath();
        }

        BuildTerrain(area);

        if (useLocationMarkers)
        {
            // Lugares pequenos sobre el terreno; sin conexiones de diagrama.
            BuildFog(area);
            BuildLocations(area);
        }
        else
        {
            BuildLinks(area);
            BuildNodes(area);
        }

        // HUD del Paso 89 si estan asignados sus sprites; si no, la barra superior original.
        if (hudFrame != null)
            BuildMapHud(canvasGo.transform);
        else
            BuildTopBar(canvasGo.transform);

        if (!useLocationMarkers)
            BuildInfoPanel(canvasGo.transform);

        BuildEventPanel(canvasGo.transform);

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

    private static RectTransform Group(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        Stretch(rect);
        return rect;
    }

    // ---------------- Terreno ----------------

    // Prolonga una capa de terreno mas alla del lienzo 16:9 para cubrir
    // pantallas mas anchas o mas altas sin dejar huecos.
    private static RectTransform Extend(RectTransform rect, bool left, bool right)
    {
        rect.offsetMin = new Vector2(left ? -1400f : 0f, -400f);
        rect.offsetMax = new Vector2(right ? 1400f : 0f, 400f);
        return rect;
    }

    private void BuildTerrain(RectTransform area)
    {
        System.Random rng = new System.Random(20261);
        // Con arte ilustrado asignado se usa ese en lugar del terreno provisional.
        if (art != null && art.worldBase != null)
        {
            BuildIllustratedWorld(area);
            return;
        }

        Transform terrain = Group("Terreno", area);

        if (useLocationMarkers)
        {
            BuildOrganicTerrain(terrain, rng);
            return;
        }

        string[] zones = { MapProgression.Meadow, MapProgression.Forest, MapProgression.Corruption };
        Color[] baseColors =
        {
            new Color(0.47f, 0.64f, 0.42f, 1f),   // Pradera: verdes suaves
            new Color(0.09f, 0.22f, 0.18f, 1f),   // Bosque: verdes oscuros
            new Color(0.17f, 0.09f, 0.23f, 1f),   // Corrupcion: morados y negros
        };

        Transform bases = Group("Bases", terrain);
        for (int i = 0; i < 3; i++)
            Extend(Box("Region_" + zones[i], bases, new Vector2(i / 3f, 0f), new Vector2((i + 1) / 3f, 1f), baseColors[i]), i == 0, i == 2);

        // Manchas de suelo para romper el color plano.
        Transform ground = Group("Suelo", terrain);
        for (int i = 0; i < 36; i++)
        {
            int zone = i % 3;
            Vector2 c = new Vector2(zone * 640f + Rf(rng, -40f, 680f), Rf(rng, 40f, 1040f));
            c.x = Mathf.Clamp(c.x, 0f, 1920f);
            bool light = rng.NextDouble() < 0.5;
            Color tint = zone == 0
                ? (light ? new Color(0.66f, 0.80f, 0.55f, 0.55f) : new Color(0.34f, 0.52f, 0.34f, 0.45f))
                : zone == 1
                    ? (light ? new Color(0.17f, 0.34f, 0.26f, 0.55f) : new Color(0.04f, 0.12f, 0.10f, 0.55f))
                    : (light ? new Color(0.36f, 0.17f, 0.46f, 0.45f) : new Color(0.05f, 0.02f, 0.08f, 0.60f));
            Place("Mancha", ground, c, new Vector2(Rf(rng, 320f, 620f), Rf(rng, 200f, 380f)), tint, SpSoft);
        }

        BuildMeadow(Group("Decorado_Pradera", terrain), rng);
        BuildForest(Group("Decorado_Bosque", terrain), rng);
        BuildCorruption(Group("Decorado_Corrupcion", terrain), rng);

        // Transiciones suaves entre regiones.
        Transform seams = Group("Transiciones", terrain);
        Place("Transicion_Pradera_Bosque", seams, new Vector2(640f, 540f), new Vector2(340f, 1300f), new Color(0.20f, 0.36f, 0.28f, 0.55f), SpSoft);
        Place("Transicion_Bosque_Corrupcion", seams, new Vector2(1280f, 540f), new Vector2(340f, 1300f), new Color(0.16f, 0.14f, 0.26f, 0.60f), SpSoft);

        // Vineta superior e inferior (atmosfera).
        Extend(Box("VinetaSuperior", terrain, new Vector2(0f, 0.78f), new Vector2(1f, 1f), new Color(0.01f, 0.01f, 0.03f, 0.60f), SpGrad), true, true);
        RectTransform bottom = Extend(Box("VinetaInferior", terrain, new Vector2(0f, 0f), new Vector2(1f, 0.30f), new Color(0.01f, 0.01f, 0.03f, 0.65f), SpGrad), true, true);
        bottom.localScale = new Vector3(1f, -1f, 1f);

        // Velos de bloqueo (los controla Refresh) y rotulos de region.
        Transform labels = Group("Rotulos", terrain);
        for (int i = 0; i < 3; i++)
        {
            RectTransform veil = Extend(Box("Velo_" + zones[i], labels,
                new Vector2(i / 3f, 0f), new Vector2((i + 1) / 3f, 1f), Clear0), i == 0, i == 2);

            float x0 = i / 3f + 0.045f, x1 = (i + 1) / 3f - 0.045f;
            Panel("Cartel_" + zones[i], labels, new Vector2(x0, 0.832f), new Vector2(x1, 0.902f),
                new Color(0.03f, 0.04f, 0.07f, 0.66f), GoldLine);

            Vector2 mid = new Vector2((i + 0.5f) * 640f, 0.867f * 1080f);
            float half = (x1 - x0) * 1920f * 0.5f - 22f;
            Place("AdornoIzq_" + zones[i], labels, mid + new Vector2(-half, 0f), new Vector2(16f, 16f), GoldBright, SpDiamond);
            Place("AdornoDer_" + zones[i], labels, mid + new Vector2(half, 0f), new Vector2(16f, 16f), GoldBright, SpDiamond);

            Text label = Shadowed(Label("Nombre_" + zones[i], labels,
                new Vector2(x0, 0.832f), new Vector2(x1, 0.902f), 25, TextAnchor.MiddleCenter,
                new Color(0.97f, 0.95f, 0.88f, 1f)));
            label.fontStyle = FontStyle.Bold;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            regions.Add(new RegionView
            {
                zone = zones[i],
                overlay = veil.GetComponent<Image>(),
                label = label
            });
        }
    }

    // ---------------- Terreno organico (modo lugares) ----------------

    private void PaintZone(Transform g, System.Random rng, float xMin, float xMax, Color[] palette, int count, Vector2 sizeMin, Vector2 sizeMax)
    {
        for (int i = 0; i < count; i++)
        {
            Color c = palette[rng.Next(palette.Length)];
            c.a = Rf(rng, 0.55f, 0.92f);
            Place("Pincelada", g, new Vector2(Rf(rng, xMin, xMax), Rf(rng, -40f, 1120f)),
                new Vector2(Rf(rng, sizeMin.x, sizeMax.x), Rf(rng, sizeMin.y, sizeMax.y)), c, SpSoft);
        }
    }

    // Un solo territorio: las regiones se funden por pinceladas suaves, hay
    // una cordillera al norte y un sendero de tierra que une los lugares.
    private void BuildOrganicTerrain(Transform terrain, System.Random rng)
    {
        Transform bases = Group("Base", terrain);
        Extend(Box("BaseOscura", bases, Vector2.zero, Vector2.one, new Color(0.09f, 0.14f, 0.13f, 1f)), true, true);

        // Prolongacion de los bordes para pantallas mas anchas.
        Place("BordeIzquierdo", bases, new Vector2(-700f, 540f), new Vector2(1400f, 1500f), new Color(0.44f, 0.58f, 0.40f, 1f), null);
        Place("BordeDerecho", bases, new Vector2(2620f, 540f), new Vector2(1400f, 1500f), new Color(0.14f, 0.08f, 0.20f, 1f), null);

        // Difuminado de las costuras para que el borde del lienzo no se note.
        for (int i = 0; i < 5; i++)
        {
            float y = 120f + i * 210f;
            Place("PlumaIzquierda", bases, new Vector2(-30f, y), new Vector2(900f, 760f), new Color(0.44f, 0.58f, 0.40f, 1f), SpSoft);
            Place("PlumaDerecha", bases, new Vector2(1950f, y), new Vector2(900f, 760f), new Color(0.14f, 0.08f, 0.20f, 1f), SpSoft);
        }

        Color[] meadow =
        {
            new Color(0.50f, 0.66f, 0.44f, 1f), new Color(0.58f, 0.72f, 0.48f, 1f),
            new Color(0.76f, 0.70f, 0.52f, 1f), new Color(0.62f, 0.60f, 0.54f, 1f)
        };
        Color[] forest =
        {
            new Color(0.09f, 0.24f, 0.19f, 1f), new Color(0.11f, 0.29f, 0.29f, 1f),
            new Color(0.06f, 0.17f, 0.14f, 1f), new Color(0.20f, 0.33f, 0.33f, 1f)
        };
        Color[] corruption =
        {
            new Color(0.19f, 0.10f, 0.27f, 1f), new Color(0.30f, 0.26f, 0.36f, 1f),
            new Color(0.10f, 0.06f, 0.15f, 1f), new Color(0.40f, 0.16f, 0.42f, 0.45f)
        };

        Transform paint = Group("Pinceladas", terrain);
        PaintZone(paint, rng, -60f, 760f, meadow, 26, new Vector2(520f, 380f), new Vector2(880f, 640f));
        PaintZone(paint, rng, 520f, 1400f, forest, 26, new Vector2(520f, 380f), new Vector2(900f, 660f));
        PaintZone(paint, rng, 1160f, 1980f, corruption, 26, new Vector2(520f, 380f), new Vector2(880f, 640f));

        BuildMeadow(Group("Decorado_Pradera", terrain), rng);
        BuildForest(Group("Decorado_Bosque", terrain), rng);
        BuildCorruption(Group("Decorado_Corrupcion", terrain), rng);
        BuildMountains(Group("Cordillera", terrain), rng);
        BuildNaturalPath(Group("Sendero", terrain));

        Extend(Box("VinetaSuperior", terrain, new Vector2(0f, 0.80f), new Vector2(1f, 1f), new Color(0.01f, 0.01f, 0.03f, 0.55f), SpGrad), true, true);
        RectTransform bottom = Extend(Box("VinetaInferior", terrain, new Vector2(0f, 0f), new Vector2(1f, 0.22f), new Color(0.01f, 0.01f, 0.03f, 0.55f), SpGrad), true, true);
        bottom.localScale = new Vector3(1f, -1f, 1f);
    }

    // Cordillera provisional: picos en capas que cierran el norte.
    private void BuildMountains(Transform g, System.Random rng)
    {
        for (int row = 0; row < 3; row++)
        {
            float baseY = 905f - row * 42f;
            float shade = 0.20f + row * 0.07f;
            int count = 11 - row * 2;

            for (int i = 0; i < count; i++)
            {
                float x = 560f + (1500f / count) * i + Rf(rng, -30f, 30f);
                float w = Rf(rng, 230f, 360f), h = Rf(rng, 150f, 240f);
                Vector2 p = new Vector2(x, baseY + h * 0.5f);
                Color body = new Color(shade, shade * 0.95f + 0.02f, shade * 1.25f, 1f);

                Place("PicoSombra", g, p + new Vector2(0f, -h * 0.5f), new Vector2(w * 0.9f, 22f), new Color(0f, 0f, 0f, 0.25f), SpSoft);
                Place("Pico", g, p, new Vector2(w, h), body, SpTri);
                Place("PicoLuz", g, p + new Vector2(w * 0.10f, -h * 0.04f), new Vector2(w * 0.52f, h * 0.86f), body * 1.28f, SpTri);

                if (row == 0)
                    Place("Nieve", g, p + new Vector2(0f, h * 0.34f), new Vector2(w * 0.26f, h * 0.30f), new Color(0.82f, 0.84f, 0.92f, 0.9f), SpTri);
            }
        }

        // Estribaciones suaves al noroeste de la pradera.
        for (int i = 0; i < 4; i++)
            Place("Estribacion", g, new Vector2(Rf(rng, 60f, 520f), Rf(rng, 930f, 1010f)), new Vector2(Rf(rng, 280f, 440f), Rf(rng, 110f, 170f)), new Color(0.40f, 0.52f, 0.38f, 0.8f), SpCircle);
    }

    // Sendero de tierra como parte del terreno (sin flechas ni trazos de diagrama).
    private void BuildNaturalPath(Transform g)
    {
        if (pathPoints.Count < 2)
            return;

        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                Vector2 a = pathPoints[i], b = pathPoints[i + 1], d = b - a;
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                float t = a.x / 1920f;
                Color dirt = t < 0.34f ? new Color(0.80f, 0.72f, 0.54f, 1f)
                           : t < 0.68f ? Color.Lerp(new Color(0.80f, 0.72f, 0.54f, 1f), new Color(0.38f, 0.32f, 0.25f, 1f), (t - 0.34f) / 0.34f)
                           : Color.Lerp(new Color(0.38f, 0.32f, 0.25f, 1f), new Color(0.40f, 0.35f, 0.46f, 1f), (t - 0.68f) / 0.32f);

                if (pass == 0)
                    Place("SenderoBorde", g, (a + b) * 0.5f, new Vector2(d.magnitude + 6f, 26f), new Color(dirt.r * 0.45f, dirt.g * 0.42f, dirt.b * 0.40f, 0.55f), null, angle);
                else
                    Place("Sendero", g, (a + b) * 0.5f, new Vector2(d.magnitude + 5f, 16f), dirt, null, angle);
            }
        }
    }

    // Capas del mundo ilustrado: base > capas (alfa) > atmosfera. Las capas
    // extra son Images a pantalla completa del lienzo 16:9.
    // Jerarquia bajo MapArea (de atras hacia delante): FondoBase,
    // NieblaBloqueo, MarcadoresInteractivos y PanelesUI (las dos ultimas las
    // crean BuildFog / BuildLocations).
    private void BuildIllustratedWorld(RectTransform area)
    {
        Transform baseGroup = Group("FondoBase", area);

        // La ilustracion conserva su proporcion: se ajusta a la altura del lienzo
        // (sin recortar ni estirar) y queda centrada; lo que sobra a los lados
        // lo cubre el fondo oscuro general.
        Rect spriteRect = art.worldBase.rect;
        float mapWidth = 1080f * spriteRect.width / spriteRect.height;
        RectTransform world = Box("MundoBase", Group("Mundo", baseGroup), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white, art.worldBase);
        world.sizeDelta = new Vector2(mapWidth, 1080f);
        world.anchoredPosition = Vector2.zero;
        world.GetComponent<Image>().preserveAspect = true;

        Transform layers = Group("MundoCapas", baseGroup);
        if (art.worldLayers != null)
            for (int i = 0; i < art.worldLayers.Length; i++)
                if (art.worldLayers[i] != null)
                    Box("Capa_" + i + "_" + art.worldLayers[i].name, layers, Vector2.zero, Vector2.one, Color.white, art.worldLayers[i]);

        Transform atmosphere = Group("MundoAtmosfera", baseGroup);
        if (art.atmosphereLayers != null)
            for (int i = 0; i < art.atmosphereLayers.Length; i++)
                if (art.atmosphereLayers[i] != null)
                    Box("Atmosfera_" + i + "_" + art.atmosphereLayers[i].name, atmosphere, Vector2.zero, Vector2.one, Color.white, art.atmosphereLayers[i]);
    }

    private void Tree(Transform g, Vector2 p, float s, Color crown)
    {
        Place("Sombra", g, p + new Vector2(0f, -s * 0.55f), new Vector2(s * 0.95f, s * 0.22f), new Color(0f, 0f, 0f, 0.25f), SpSoft);
        Place("Tronco", g, p + new Vector2(0f, -s * 0.32f), new Vector2(s * 0.14f, s * 0.46f), new Color(0.33f, 0.24f, 0.16f, 1f), null);
        Place("Copa", g, p + new Vector2(0f, s * 0.16f), new Vector2(s * 0.92f, s * 0.84f), crown, SpCircle);
        Place("CopaLuz", g, p + new Vector2(-s * 0.13f, s * 0.30f), new Vector2(s * 0.46f, s * 0.40f), crown * 1.22f, SpCircle);
    }

    private void Pine(Transform g, Vector2 p, float s, Color dark, Color light)
    {
        Place("Sombra", g, p + new Vector2(0f, -s * 0.50f), new Vector2(s * 0.8f, s * 0.14f), new Color(0f, 0f, 0f, 0.35f), SpSoft);
        Place("Tronco", g, p + new Vector2(0f, -s * 0.40f), new Vector2(s * 0.10f, s * 0.20f), new Color(0.20f, 0.14f, 0.10f, 1f), null);
        Place("Nivel1", g, p + new Vector2(0f, -s * 0.12f), new Vector2(s * 0.80f, s * 0.46f), dark, SpTri);
        Place("Nivel2", g, p + new Vector2(0f, s * 0.12f), new Vector2(s * 0.64f, s * 0.42f), Color.Lerp(dark, light, 0.5f), SpTri);
        Place("Nivel3", g, p + new Vector2(0f, s * 0.34f), new Vector2(s * 0.46f, s * 0.38f), light, SpTri);
    }

    private void BuildMeadow(Transform g, System.Random rng)
    {
        Color hillLight = new Color(0.62f, 0.79f, 0.52f, 0.95f);
        Color hillShade = new Color(0.33f, 0.50f, 0.33f, 0.90f);

        // Colinas suaves (sombra + luz).
        for (int i = 0; i < 9; i++)
        {
            Vector2 c = new Vector2(Rf(rng, 40f, 600f), Rf(rng, 300f, 860f));
            float w = Rf(rng, 240f, 400f), h = Rf(rng, 90f, 150f);
            Place("ColinaSombra", g, c + new Vector2(0f, -14f), new Vector2(w, h), hillShade, SpCircle);
            Place("Colina", g, c, new Vector2(w * 0.96f, h * 0.90f), hillLight, SpCircle);
            Place("ColinaLuz", g, c + new Vector2(-w * 0.12f, h * 0.18f), new Vector2(w * 0.50f, h * 0.38f), new Color(0.74f, 0.88f, 0.60f, 0.65f), SpSoft);
        }

        // Arboles dispersos.
        int placed = 0;
        for (int tries = 0; tries < 160 && placed < 20; tries++)
        {
            Vector2 p = new Vector2(Rf(rng, 30f, 610f), Rf(rng, 290f, 880f));
            if (!FreeSpot(p, 120f, 55f)) continue;
            Tree(g, p, Rf(rng, 46f, 72f), new Color(0.27f, 0.45f, 0.28f, 1f));
            placed++;
        }

        // Pequenas ruinas.
        Ruins(g, new Vector2(300f, 858f));
        Ruins(g, new Vector2(95f, 640f));
        Ruins(g, new Vector2(585f, 270f));
    }

    private void Ruins(Transform g, Vector2 p)
    {
        Color stone = new Color(0.68f, 0.68f, 0.66f, 1f);
        Color shade = new Color(0.45f, 0.46f, 0.46f, 1f);
        Place("Sombra", g, p + new Vector2(0f, -34f), new Vector2(150f, 26f), new Color(0f, 0f, 0f, 0.28f), SpSoft);
        Place("PilarA", g, p + new Vector2(-34f, 0f), new Vector2(22f, 68f), stone, null);
        Place("PilarASombra", g, p + new Vector2(-26f, 0f), new Vector2(8f, 68f), shade, null);
        Place("PilarB", g, p + new Vector2(8f, -8f), new Vector2(22f, 48f), stone, null);
        Place("PilarBRoto", g, p + new Vector2(8f, 22f), new Vector2(14f, 12f), stone, null, 18f);
        Place("Bloque", g, p + new Vector2(42f, -26f), new Vector2(46f, 18f), shade, null, 14f);
        Place("Musgo", g, p + new Vector2(-30f, -30f), new Vector2(40f, 14f), new Color(0.32f, 0.52f, 0.30f, 0.9f), SpSoft);
    }

    private void BuildForest(Transform g, System.Random rng)
    {
        Color dark = new Color(0.05f, 0.17f, 0.13f, 1f);
        Color light = new Color(0.10f, 0.28f, 0.20f, 1f);

        // Masa de pinos (los mas lejanos primero).
        List<Vector2> spots = new List<Vector2>();
        for (int tries = 0; tries < 700 && spots.Count < 78; tries++)
        {
            Vector2 p = new Vector2(Rf(rng, 650f, 1270f), Rf(rng, 60f, 1030f));
            if (!FreeSpot(p, 118f, 54f)) continue;
            spots.Add(p);
        }

        spots.Sort((a, b) => b.y.CompareTo(a.y));
        foreach (Vector2 p in spots)
            Pine(g, p, Rf(rng, 80f, 150f), dark, Color.Lerp(light, new Color(0.14f, 0.34f, 0.24f, 1f), (float)rng.NextDouble()));

        // Niebla ligera.
        Transform fog = Group("Niebla", g);
        for (int i = 0; i < 7; i++)
            Place("Niebla", fog, new Vector2(Rf(rng, 680f, 1240f), Rf(rng, 220f, 860f)),
                new Vector2(Rf(rng, 420f, 640f), Rf(rng, 140f, 240f)), new Color(0.70f, 0.85f, 0.80f, Rf(rng, 0.08f, 0.14f)), SpSoft);
    }

    private void DeadTree(Transform g, Vector2 p, float s)
    {
        Color wood = new Color(0.26f, 0.22f, 0.27f, 1f);
        Place("Sombra", g, p + new Vector2(0f, -s * 0.05f), new Vector2(s * 0.55f, s * 0.12f), new Color(0f, 0f, 0f, 0.35f), SpSoft);
        Place("Tronco", g, p + new Vector2(0f, s * 0.42f), new Vector2(s * 0.09f, s * 0.90f), wood, null);
        Place("RamaDer", g, p + new Vector2(s * 0.13f, s * 0.78f), new Vector2(s * 0.05f, s * 0.46f), wood, null, -38f);
        Place("RamaIzq", g, p + new Vector2(-s * 0.12f, s * 0.64f), new Vector2(s * 0.05f, s * 0.40f), wood, null, 42f);
        Place("RamaAlta", g, p + new Vector2(s * 0.05f, s * 0.98f), new Vector2(s * 0.04f, s * 0.28f), wood, null, -16f);
    }

    private void Crack(Transform g, System.Random rng, Vector2 start)
    {
        Vector2 p = start;
        float angle = Rf(rng, 0f, 360f);
        int segments = rng.Next(4, 7);

        for (int s = 0; s < segments; s++)
        {
            float length = Rf(rng, 36f, 84f);
            angle += Rf(rng, -42f, 42f);
            Vector2 q = p + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * length;
            Vector2 mid = (p + q) * 0.5f;
            Place("GrietaLuz", g, mid, new Vector2(length + 5f, 13f), new Color(0.62f, 0.22f, 0.80f, 0.20f), null, angle);
            Place("Grieta", g, mid, new Vector2(length + 2f, 5f), new Color(0.02f, 0.01f, 0.04f, 0.95f), null, angle);
            p = q;
        }
    }

    private void BuildCorruption(Transform g, System.Random rng)
    {
        // Resplandor morado del suelo marchito.
        for (int i = 0; i < 8; i++)
            Place("Resplandor", g, new Vector2(Rf(rng, 1320f, 1880f), Rf(rng, 120f, 960f)),
                new Vector2(Rf(rng, 260f, 420f), Rf(rng, 150f, 260f)), new Color(0.50f, 0.16f, 0.62f, 0.20f), SpSoft);

        // Grietas.
        for (int i = 0; i < 15; i++)
            Crack(g, rng, new Vector2(Rf(rng, 1300f, 1900f), Rf(rng, 80f, 980f)));

        // Rocas puntiagudas.
        int rocks = 0;
        for (int tries = 0; tries < 160 && rocks < 12; tries++)
        {
            Vector2 p = new Vector2(Rf(rng, 1310f, 1890f), Rf(rng, 80f, 980f));
            if (!FreeSpot(p, 112f, 52f)) continue;
            float s = Rf(rng, 50f, 104f);
            Place("RocaSombra", g, p + new Vector2(0f, -s * 0.42f), new Vector2(s * 0.9f, s * 0.16f), new Color(0f, 0f, 0f, 0.35f), SpSoft);
            Place("Roca", g, p, new Vector2(s * 0.8f, s), new Color(0.12f, 0.10f, 0.16f, 1f), SpTri);
            Place("RocaFaz", g, p + new Vector2(s * 0.10f, -s * 0.05f), new Vector2(s * 0.40f, s * 0.70f), new Color(0.22f, 0.17f, 0.30f, 1f), SpTri);
            rocks++;
        }

        // Arboles muertos.
        int dead = 0;
        for (int tries = 0; tries < 200 && dead < 14; tries++)
        {
            Vector2 p = new Vector2(Rf(rng, 1310f, 1890f), Rf(rng, 70f, 900f));
            if (!FreeSpot(p, 118f, 54f)) continue;
            DeadTree(g, p, Rf(rng, 90f, 150f));
            dead++;
        }

        // Bruma morada.
        for (int i = 0; i < 5; i++)
            Place("Bruma", g, new Vector2(Rf(rng, 1340f, 1860f), Rf(rng, 200f, 800f)),
                new Vector2(Rf(rng, 380f, 560f), Rf(rng, 120f, 200f)), new Color(0.55f, 0.30f, 0.68f, Rf(rng, 0.10f, 0.16f)), SpSoft);
    }

    // ---------------- Conexiones ----------------

    private void BuildLinks(RectTransform area)
    {
        Transform parent = Group("Conexiones", area);

        for (int i = 0; i < NodeDefs.Length - 1; i++)
        {
            NodeDef a = NodeDefs[i], b = NodeDefs[i + 1];
            Vector2 pa = Px(a), pb = Px(b);
            Vector2 delta = pb - pa;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Vector2 mid = (pa + pb) * 0.5f;

            // Sombra y borde del camino (fijos); el trazo lo recolorea Refresh.
            Place("CaminoSombra_" + a.id + "_" + b.id, parent, mid + new Vector2(0f, -4f), new Vector2(delta.magnitude, 28f), new Color(0f, 0f, 0f, 0.32f), null, angle);
            Place("CaminoBorde_" + a.id + "_" + b.id, parent, mid, new Vector2(delta.magnitude, 22f), new Color(0.05f, 0.05f, 0.09f, 0.55f), null, angle);
            RectTransform line = Place("Camino_" + a.id + "_" + b.id, parent, mid, new Vector2(delta.magnitude, 16f), Color.white, null, angle);

            links.Add(new LinkView { fromId = a.id, toId = b.id, line = line.GetComponent<Image>() });
        }
    }

    private void ApplyLinkVisual(LinkView link, string recommended)
    {
        bool done = link.fromId != BossId && GameSession.IsEncounterCompleted(link.fromId);
        bool next = link.toId == recommended;

        Color color = done
            ? new Color(0.96f, 0.80f, 0.42f, 1f)
            : next
                ? new Color(1f, 0.95f, 0.80f, 0.95f)
                : new Color(0.80f, 0.83f, 0.92f, 0.30f);

        // Completado = trazo continuo; pendiente = discontinuo.
        Sprite desired = done ? null : SpDash;

        if (link.line.sprite != desired)
        {
            link.line.sprite = desired;
            link.line.type = desired == null ? Image.Type.Simple : Image.Type.Tiled;
        }

        link.line.color = color;
    }

    // ---------------- Nodos ----------------

    private void BuildNodes(RectTransform area)
    {
        Transform parent = Group("Nodos", area);

        foreach (NodeDef def in NodeDefs)
        {
            Vector2 p = Px(def);
            bool evt = MapProgression.IsEvent(def.id);
            bool boss = def.id == BossId;
            float frameSize = evt || boss ? 134f : 112f;
            float bodySize = evt || boss ? 110f : 92f;
            Sprite shape = evt ? SpDiamond : boss ? SpHex : SpCircle;

            RectTransform container = Place("Nodo_" + def.id, parent, p, new Vector2(170f, 170f), Clear0, null);

            Local("Sombra", container, new Vector2(0f, -10f), new Vector2(frameSize + 26f, frameSize * 0.78f), new Color(0f, 0f, 0f, 0.5f), SpSoft);
            RectTransform ring = Local("Anillo", container, Vector2.zero, new Vector2(frameSize + 46f, frameSize + 46f), Color.white, SpRing);
            RectTransform frame = Local("Marco", container, Vector2.zero, new Vector2(frameSize, frameSize), GoldLine, shape);
            RectTransform body = Local("Cuerpo", container, Vector2.zero, new Vector2(bodySize, bodySize), LockedColor, shape);

            Image bodyImage = body.GetComponent<Image>();
            bodyImage.raycastTarget = true;
            Button button = body.gameObject.AddComponent<Button>();
            button.targetGraphic = bodyImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            Image iconImage = null;
            if (evt || boss)
            {
                RectTransform icon = Local("Icono", body, new Vector2(0f, 16f), new Vector2(48f, 48f), Color.white, evt ? IcMask : IcSkull);
                iconImage = icon.GetComponent<Image>();
            }

            Text label = Shadowed(Label("Id", body, Vector2.zero, Vector2.one, evt || boss ? 20 : 34,
                TextAnchor.MiddleCenter, Color.white));
            label.text = boss ? "Jefe" : (evt ? "Evento" : def.id);
            label.fontStyle = FontStyle.Bold;

            if (evt || boss)
            {
                RectTransform lr = (RectTransform)label.transform;
                lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0.46f);
            }

            // Insignia de estado (candado / visto / estrella).
            // El evento (rombo) lleva la insignia a la izquierda para no tapar al nodo vecino.
            RectTransform badge = Local("Insignia", container, new Vector2(evt ? -56f : 52f, 50f), new Vector2(44f, 44f), new Color(0.07f, 0.09f, 0.15f, 1f), SpCircle);
            Local("BordeInsignia", badge, Vector2.zero, new Vector2(44f, 44f), GoldLine, SpRing);
            RectTransform badgeIcon = Local("Icono", badge, Vector2.zero, new Vector2(26f, 26f), Color.white, IcLock);
            badge.gameObject.SetActive(false);

            // Etiqueta de estado con fondo.
            Vector2 pillCenter = p + new Vector2(0f, -(frameSize * 0.5f + 26f));
            Place("EtiquetaFondo_" + def.id, parent, pillCenter, new Vector2(244f, 34f), new Color(0.02f, 0.03f, 0.06f, 0.66f), SpRound, 0f, true);
            Text state = Shadowed(Label("Estado", parent, Vector2.zero, Vector2.zero, 20, TextAnchor.MiddleCenter, TextColor));
            RectTransform sr = (RectTransform)state.transform;
            sr.offsetMin = pillCenter + new Vector2(-120f, -17f);
            sr.offsetMax = pillCenter + new Vector2(120f, 17f);

            nodes.Add(new NodeView
            {
                id = def.id, zone = def.zone, position = new Vector2(def.x, def.y),
                rect = container, button = button, image = bodyImage,
                ring = ring.GetComponent<Image>(), label = label, stateLabel = state,
                frame = frame.GetComponent<Image>(), badge = badge.GetComponent<Image>(),
                badgeIcon = badgeIcon.GetComponent<Image>(), icon = iconImage
            });
        }
    }

    // Estado -> color + marco + insignia + icono. No usa solo el color:
    // forma (circulo / rombo / hexagono), insignia y texto del estado.
    private void ApplyNodeVisual(NodeView node, MapProgression.NodeState state, string recommended)
    {
        bool evt = MapProgression.IsEvent(node.id);
        bool boss = node.id == BossId;
        bool rec = node.id == recommended;

        Color body, frame;
        Sprite badgeSprite = null;
        Color badgeColor = Color.white;
        bool showBadge = true;

        if (boss && state == MapProgression.NodeState.Locked)
        {
            body = new Color(0.23f, 0.13f, 0.24f, 1f);
            frame = new Color(0.55f, 0.30f, 0.55f, 1f);
            badgeSprite = IcLock; badgeColor = new Color(0.75f, 0.65f, 0.80f, 1f);
        }
        else if (state == MapProgression.NodeState.Completed)
        {
            body = new Color(0.22f, 0.58f, 0.36f, 1f);
            frame = new Color(0.68f, 0.92f, 0.72f, 1f);
            badgeSprite = IcCheck; badgeColor = new Color(0.55f, 0.95f, 0.62f, 1f);
        }
        else if (state == MapProgression.NodeState.Available)
        {
            if (evt) { body = new Color(0.55f, 0.32f, 0.82f, 1f); frame = new Color(0.88f, 0.74f, 1f, 1f); }
            else if (rec) { body = new Color(0.90f, 0.66f, 0.18f, 1f); frame = new Color(1f, 0.93f, 0.62f, 1f); }
            else { body = new Color(0.26f, 0.50f, 0.80f, 1f); frame = new Color(0.84f, 0.92f, 1f, 1f); }

            if (rec) { badgeSprite = IcStar; badgeColor = new Color(1f, 0.85f, 0.35f, 1f); }
            else showBadge = false;
        }
        else
        {
            body = new Color(0.21f, 0.22f, 0.27f, 1f);
            frame = new Color(0.38f, 0.39f, 0.46f, 1f);
            badgeSprite = IcLock; badgeColor = new Color(0.72f, 0.74f, 0.82f, 1f);
        }

        node.image.color = body;
        node.frame.color = frame;

        if (node.badge != null)
        {
            if (node.badge.gameObject.activeSelf != showBadge)
                node.badge.gameObject.SetActive(showBadge);

            if (showBadge)
            {
                node.badgeIcon.sprite = badgeSprite;
                node.badgeIcon.color = badgeColor;
            }
        }

        node.label.color = state == MapProgression.NodeState.Locked
            ? new Color(0.78f, 0.80f, 0.88f, 0.92f) : Color.white;

        if (node.icon != null)
            node.icon.color = state == MapProgression.NodeState.Locked
                ? new Color(1f, 1f, 1f, 0.62f) : Color.white;
    }

    // ---------------- Barra superior ----------------

    private Text BuildChip(Transform bar, string name, float x0, float x1, Sprite icon, Color iconColor, bool coin, int size, Color textColor)
    {
        RectTransform chip = Panel("Chip_" + name, bar, new Vector2(x0, 0.16f), new Vector2(x1, 0.84f),
            ChipColor, new Color(0.30f, 0.34f, 0.46f, 0.9f));

        RectTransform iconRect = NewRect("Icono", chip, out Image iconImage);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(14f, 0f);
        iconRect.sizeDelta = new Vector2(38f, 38f);
        iconImage.sprite = icon;
        iconImage.color = iconColor;

        if (coin)
            Local("Relieve", iconRect, Vector2.zero, new Vector2(26f, 26f), new Color(0.55f, 0.38f, 0.08f, 0.9f), SpRing);

        Text text = Label(name, chip, Vector2.zero, Vector2.one, size, TextAnchor.MiddleLeft, textColor);
        RectTransform tr = (RectTransform)text.transform;
        tr.offsetMin = new Vector2(62f, 0f);
        tr.offsetMax = new Vector2(-8f, 0f);
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    private void BuildTopBar(Transform parent)
    {
        RectTransform bar = NewRect("BarraSuperior", parent, out Image barImage);
        bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.offsetMin = new Vector2(0, -68);
        bar.offsetMax = Vector2.zero;
        barImage.color = Navy;

        RectTransform line = NewRect("LineaDorada", bar, out Image lineImage);
        line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(1, 0);
        line.offsetMin = Vector2.zero; line.offsetMax = new Vector2(0, 3);
        lineImage.color = GoldLine;

        coinsText = BuildChip(bar, "Coronas", 0.008f, 0.128f, SpCircle, new Color(0.96f, 0.78f, 0.28f, 1f), true, 26, GoldColor);
        potionsText = BuildChip(bar, "Pociones", 0.135f, 0.262f, IcPotion, new Color(0.95f, 0.50f, 0.58f, 1f), false, 24, TextColor);
        antidotesText = BuildChip(bar, "Antidotos", 0.269f, 0.405f, IcAntidote, new Color(0.50f, 0.90f, 0.74f, 1f), false, 24, TextColor);

        RectTransform progress = Panel("Chip_Progreso", bar, new Vector2(0.412f, 0.16f), new Vector2(0.672f, 0.84f),
            ChipColor, new Color(0.30f, 0.34f, 0.46f, 0.9f));
        progressText = Label("Progreso", progress, Vector2.zero, Vector2.one, 23, TextAnchor.MiddleCenter, TextColor);
        progressText.fontStyle = FontStyle.Bold;

        shopButton = MakeButton("Tienda", bar, new Vector2(0.685f, 0.15f), new Vector2(0.775f, 0.85f), "Tienda", 24, out _);
        inventoryButton = MakeButton("Inventario", bar, new Vector2(0.785f, 0.15f), new Vector2(0.875f, 0.85f), "Inventario", 24, out _);
        menuButton = MakeButton("VolverMenu", bar, new Vector2(0.885f, 0.15f), new Vector2(0.995f, 0.85f), "Volver al menú", 22, out menuButtonLabel);
    }

    // ---------------- Panel de informacion ----------------

    private void BuildInfoPanel(Transform parent)
    {
        RectTransform panel = Panel("PanelInfo", parent, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Navy, GoldLine);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.offsetMin = new Vector2(-620, 24);
        panel.offsetMax = new Vector2(620, 256);

        RectTransform accent = NewRect("Acento", panel, out Image accentImage);
        accent.anchorMin = new Vector2(0, 0); accent.anchorMax = new Vector2(0, 1);
        accent.offsetMin = new Vector2(14, 18); accent.offsetMax = new Vector2(22, -18);
        accentImage.color = GoldLine;
        infoAccent = accentImage;

        infoTitle = Shadowed(Label("Titulo", panel, new Vector2(0.040f, 0.74f), new Vector2(0.745f, 1f), 34, TextAnchor.MiddleLeft, GoldBright));
        infoTitle.fontStyle = FontStyle.Bold;

        RectTransform divider = NewRect("Separador", panel, out Image dividerImage);
        divider.anchorMin = new Vector2(0.040f, 0.735f); divider.anchorMax = new Vector2(0.745f, 0.735f);
        divider.offsetMin = new Vector2(6, -1); divider.offsetMax = new Vector2(-6, 1);
        dividerImage.color = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.55f);

        infoSubtitle = Label("Subtitulo", panel, new Vector2(0.040f, 0.55f), new Vector2(0.745f, 0.73f), 22, TextAnchor.MiddleLeft, new Color(0.84f, 0.89f, 1f, 1f));
        infoRewards = Label("Recompensas", panel, new Vector2(0.040f, 0.37f), new Vector2(0.745f, 0.55f), 24, TextAnchor.MiddleLeft, new Color(1f, 0.88f, 0.52f, 1f));
        infoRewards.fontStyle = FontStyle.Bold;
        infoEnemies = Label("Enemigos", panel, new Vector2(0.040f, 0.17f), new Vector2(0.745f, 0.37f), 22, TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.95f, 1f));
        infoNote = Label("Nota", panel, new Vector2(0.040f, 0f), new Vector2(0.745f, 0.17f), 18, TextAnchor.MiddleLeft, DimText);
        infoNote.fontStyle = FontStyle.Italic;

        startButton = MakeButton("IniciarCombate", panel, new Vector2(0.775f, 0.20f), new Vector2(0.972f, 0.80f), "Iniciar combate", 28, out startButtonLabel);
        startButton.targetGraphic.raycastTarget = true;
    }

    // El acento lateral toma el color de la zona del nodo seleccionado.
    private void UpdateInfoAccent()
    {
        if (infoAccent == null)
            return;

        Color color = GoldLine;

        if (MapProgression.IsEvent(selectedId)) color = new Color(0.70f, 0.45f, 0.95f, 1f);
        else if (selectedId == BossId) color = new Color(0.65f, 0.22f, 0.32f, 1f);
        else
        {
            string zone = MapProgression.ZoneOf(selectedId);
            if (zone == MapProgression.Meadow) color = new Color(0.55f, 0.80f, 0.45f, 1f);
            else if (zone == MapProgression.Forest) color = new Color(0.22f, 0.62f, 0.44f, 1f);
            else if (zone == MapProgression.Corruption) color = new Color(0.66f, 0.32f, 0.86f, 1f);
        }

        if (infoAccent.color != color)
            infoAccent.color = color;
    }

    // ---------------- Modal del evento ----------------

    // Modal narrativo provisional (oculto hasta abrir el evento). El velo
    // bloquea los clics sobre el mapa mientras esta abierto.
    private void BuildEventPanel(Transform parent)
    {
        RectTransform veil = Box("EventoVelo", parent, Vector2.zero, Vector2.one, new Color(0.01f, 0.01f, 0.03f, 0.78f));
        veil.GetComponent<Image>().raycastTarget = true;
        eventPanel = veil.gameObject;

        RectTransform box = Panel("PanelEvento", veil, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Navy, GoldBright);
        box.offsetMin = new Vector2(-460, -270);
        box.offsetMax = new Vector2(460, 250);

        // Medallon con la mascara rota.
        Local("MedallonBorde", box, new Vector2(0f, 260f), new Vector2(150f, 150f), GoldBright, SpHex);
        Local("Medallon", box, new Vector2(0f, 260f), new Vector2(134f, 134f), new Color(0.40f, 0.22f, 0.66f, 1f), SpHex);
        Local("MascaraRota", box, new Vector2(0f, 262f), new Vector2(72f, 72f), new Color(0.96f, 0.92f, 1f, 1f), IcMask);

        eventTitleText = Shadowed(Label("Titulo", box, new Vector2(0, 0.70f), new Vector2(1, 0.86f), 42, TextAnchor.MiddleCenter, GoldBright));
        eventTitleText.fontStyle = FontStyle.Bold;

        RectTransform divider = NewRect("Separador", box, out Image dividerImage);
        divider.anchorMin = new Vector2(0.22f, 0.69f); divider.anchorMax = new Vector2(0.78f, 0.69f);
        divider.offsetMin = new Vector2(0, -1); divider.offsetMax = new Vector2(0, 1);
        dividerImage.color = GoldLine;

        eventBodyText = Label("Texto", box, new Vector2(0.07f, 0.26f), new Vector2(0.93f, 0.67f), 26, TextAnchor.MiddleCenter, new Color(0.90f, 0.92f, 0.98f, 1f));

        eventConfirmButton = MakeButton("RomperMascara", box, new Vector2(0.24f, 0.06f), new Vector2(0.72f, 0.21f), "Romper la máscara", 28, out _);
        eventCloseButton = MakeButton("Volver", box, new Vector2(0.77f, 0.06f), new Vector2(0.96f, 0.21f), "Volver", 22, out _);

        eventPanel.SetActive(false);
    }
}
