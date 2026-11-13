using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Ventana de misiones del mapa ilustrado: cabecera con el lugar, lista de encuentros a la izquierda,
// detalle del encuentro a la derecha e "Iniciar" abajo. Solo presenta: la seleccion, el desbloqueo y la
// carga del combate siguen siendo Select / StartSelected / MapProgression, y los datos salen de EncounterData.
public partial class WorldMapController
{
    [Serializable]
    public class MissionCard
    {
        public RectTransform root;
        public Button button;
        public Image background;
        public Image[] border;
        public Image icon;
        public TMP_Text name;
        public Image chip;
        public Image[] chipBorder;
        public TMP_Text state;
        public Image stripe;
    }

    [Serializable]
    public class EnemyTile
    {
        public RectTransform root;
        public Image border;
        public Image sprite;
        public TMP_Text count;
        public MapHoverTarget hover;
        public string enemyName;
        public string enemyStats;
    }

    [Header("Ventana de misiones")]
    [SerializeField] private Sprite missionCloseSprite;      // UI_Cerrar
    [SerializeField] private Sprite missionButtonSprite;     // Misiones (boton Iniciar)
    [SerializeField] private RawImage missionPreview;
    [SerializeField] private TMP_Text missionTitle;
    [SerializeField] private TMP_Text missionRegion;
    [SerializeField] private TMP_Text missionName;
    [SerializeField] private TMP_Text missionZone;
    [SerializeField] private TMP_Text missionState;
    [SerializeField] private TMP_Text missionXp;
    [SerializeField] private TMP_Text missionCoins;
    [SerializeField] private TMP_FontAsset missionFontRegular;   // Merriweather Regular
    [SerializeField] private TMP_FontAsset missionFontBold;      // Merriweather Bold
    [SerializeField] private RectTransform missionContent;
    [SerializeField] private GameObject missionVeil;
    [SerializeField] private Sprite missionWindowSprite;   // Assets/Sprites/Buttons/opciones 1 (el de la tienda)
    [SerializeField] private TMP_Text missionDescription;
    [SerializeField] private TMP_Text missionProgress;
    [SerializeField] private TMP_Text missionCounter;
    [SerializeField] private Image missionXpIcon;
    [SerializeField] private Image missionCoinIcon;
    [SerializeField] private Sprite missionXpSprite;      // Assets/Sprites/Iconos/XP
    [SerializeField] private Sprite missionCoinSprite;    // Assets/Sprites/Iconos/coronas
    [SerializeField] private TMP_Text missionEnemiesHint;
    [SerializeField] private RectTransform missionListContent;
    [SerializeField] private RectTransform missionTooltip;
    [SerializeField] private TMP_Text missionTooltipText;
    [SerializeField] private List<MissionCard> missionCards = new List<MissionCard>();
    [SerializeField] private List<EnemyTile> enemyTiles = new List<EnemyTile>();

    private const float MissionWindowW = 1180f, MissionWindowH = 820f;
    private const float MissionCardH = 96f, MissionCardGap = 10f;

    private static readonly Color MwFill = new Color(0.063f, 0.082f, 0.118f, 1f);
    private static readonly Color MwPanel = new Color(0.090f, 0.122f, 0.169f, 1f);
    private static readonly Color MwLine = new Color(0.30f, 0.34f, 0.46f, 0.85f);
    private static readonly Color MwTitle = new Color(0.96f, 0.86f, 0.58f, 1f);
    private static readonly Color MwDim = new Color(0.72f, 0.74f, 0.80f, 1f);

    // ---------------- Nombres visibles ----------------

    // Nombre de la ubicacion en el mapa (el mismo de su etiqueta); el id interno del lugar no cambia.
    private string VisibleLocationName(MapLocationDef def)
    {
        foreach ((string key, string text, float x, float y, bool primary) tag in LocationTags)
            if (tag.key == def.id) return tag.text;

        return def.displayName;
    }

    private static string EnemyName(EncounterData.EnemySlot slot)
    {
        EnemyArchetypeData archetype = slot.Archetype;

        if (archetype != null && !string.IsNullOrEmpty(archetype.archetypeName))
            return archetype.archetypeName;

        return slot.prefab != null ? slot.prefab.name : "";
    }

    private static Sprite EnemySprite(EncounterData.EnemySlot slot)
    {
        if (slot == null || slot.prefab == null)
            return null;

        SpriteRenderer renderer = slot.prefab.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    // ---------------- Construccion ----------------

    private RectTransform TopBox(string name, Transform parent, float x, float y, float w, float h, Color color)
    {
        RectTransform rect = NewRect(name, parent, out Image image);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, -y);
        image.color = color;
        return rect;
    }

    private TMP_Text TopText(string name, Transform parent, float x, float y, float w, float h, string text, float size, Color color, TextAlignmentOptions align)
    {
        RectTransform rect = HudRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, -y);
        return HudText(rect, text, size, color, align);
    }

    private static Image[] ThinBorder(RectTransform box, Color color, float t)
    {
        Image[] strips = new Image[4];
        string[] names = { "Borde_Sup", "Borde_Inf", "Borde_Izq", "Borde_Der" };
        Vector2[] min = { new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0f) };
        Vector2[] max = { new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        Vector2[] offMin = { new Vector2(0f, -t), Vector2.zero, Vector2.zero, new Vector2(-t, 0f) };
        Vector2[] offMax = { Vector2.zero, new Vector2(0f, t), new Vector2(t, 0f), Vector2.zero };

        for (int i = 0; i < 4; i++)
        {
            RectTransform rect = NewRect(names[i], box, out Image image);
            rect.anchorMin = min[i];
            rect.anchorMax = max[i];
            rect.offsetMin = offMin[i];
            rect.offsetMax = offMax[i];
            image.color = color;
            strips[i] = image;
        }

        return strips;
    }

    private static void SetBorderColor(Image[] strips, Color color)
    {
        if (strips == null)
            return;

        foreach (Image strip in strips)
            if (strip != null && strip.color != color) strip.color = color;
    }

    private TMP_Text InfoRow(Transform parent, string name, float y, string label, out TMP_Text value)
    {
        RectTransform row = TopBox("Fila_" + name, parent, 16f, y, 636f, 42f, MwPanel);
        ThinBorder(row, MwLine, 2f);
        TMP_Text left = TopText("Etiqueta", row, 18f, 0f, 200f, 42f, label, 20f, MwDim, TextAlignmentOptions.MidlineLeft);
        value = TopText("Valor", row, 230f, 0f, 390f, 42f, "", 22f, MapIvory, TextAlignmentOptions.MidlineLeft);
        return left;
    }

    public void BuildMissionWindow(Transform parent)
    {
        // Mismo lienzo, velo, marco ("opciones 1"), transparencia y animacion de apertura que la tienda y el inventario.
        Transform host = shop != null && shop.WindowParent != null ? shop.WindowParent : parent;

        GameObject veilGo = new GameObject("VeloMisiones", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        veilGo.transform.SetParent(host, false);
        RectTransform veilRect = (RectTransform)veilGo.transform;
        veilRect.anchorMin = Vector2.zero;
        veilRect.anchorMax = Vector2.one;
        veilRect.offsetMin = veilRect.offsetMax = Vector2.zero;
        Image veilImage = veilGo.GetComponent<Image>();
        veilImage.color = shop != null ? shop.VeilColor : new Color(0.063f, 0.082f, 0.118f, 0.8f);
        veilImage.raycastTarget = true;
        Button veilButton = veilGo.GetComponent<Button>();
        veilButton.transition = Selectable.Transition.None;   // clic fuera de la ventana = cerrar (se conecta al iniciar)
        missionVeil = veilGo;
        veilGo.SetActive(false);

        // Rectangulo del marco: incluye los margenes transparentes del sprite (~29 px a los lados, 24 arriba y 31 abajo);
        // el contenido (1180 x 820) ocupa justo el marco visible. Centrado como la tienda (-3,5 px).
        GameObject rootGo = new GameObject("PanelLugar", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        rootGo.transform.SetParent(host, false);
        RectTransform rootRect = (RectTransform)rootGo.transform;
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(MissionWindowW + 58f, MissionWindowH + 55f);
        rootRect.anchoredPosition = new Vector2(0f, -3.5f);
        Image frame = rootGo.GetComponent<Image>();
        frame.sprite = missionWindowSprite;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 4.4f;
        frame.raycastTarget = true;   // no deja pasar el clic al velo
        placePanel = rootRect;

        RectTransform window = HudRect("Contenido", rootRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        window.sizeDelta = new Vector2(MissionWindowW, MissionWindowH);
        window.anchoredPosition = new Vector2(29f, -24f);
        missionContent = window;

        Color softLine = new Color(MwLine.r, MwLine.g, MwLine.b, 0.45f);

        // A. Encabezado: lugar, region, X y una linea discreta.
        missionTitle = TopText("Titulo", window, 40f, 18f, 900f, 54f, "", 40f, MwTitle, TextAlignmentOptions.MidlineLeft);
        missionRegion = TopText("Region", window, 42f, 72f, 700f, 32f, "", 24f, MwDim, TextAlignmentOptions.MidlineLeft);
        TopBox("Separador", window, 28f, 114f, MissionWindowW - 56f, 1f, softLine);

        // Boton X (UI_Cerrar), el mismo de PAUSA, tienda e inventario. Sin efecto de agrandado.
        RectTransform close = TopBox("BotonCerrar", window, MissionWindowW - 34f, -14f, 48f, 48f, Color.white);
        Image closeImage = close.GetComponent<Image>();
        closeImage.sprite = missionCloseSprite;
        closeImage.preserveAspect = true;
        closeImage.raycastTarget = true;
        closeImage.raycastPadding = new Vector4(-12f, -12f, -12f, -12f);
        placeCloseButton = close.gameObject.AddComponent<Button>();
        placeCloseButton.targetGraphic = closeImage;
        placeCloseButton.transition = Selectable.Transition.None;

        // B. Panel izquierdo: titulo, contador y lista (sin caja ni marco).
        RectTransform left = TopBox("PanelMisiones", window, 28f, 132f, 440f, 566f, Clear0);
        left.GetComponent<Image>().raycastTarget = false;
        TopText("Titulo", left, 14f, 6f, 260f, 40f, "Misiones", 28f, MwTitle, TextAlignmentOptions.MidlineLeft);
        missionCounter = TopText("Contador", left, 250f, 6f, 176f, 40f, "", 24f, MwDim, TextAlignmentOptions.MidlineRight);
        TopBox("Separador", left, 14f, 52f, 412f, 1f, softLine);

        RectTransform viewport = TopBox("Lista", left, 4f, 64f, 432f, 500f, Clear0);
        viewport.GetComponent<Image>().raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = HudRect("Contenido", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        content.sizeDelta = new Vector2(0f, MissionCardH);
        content.anchoredPosition = Vector2.zero;
        missionListContent = content;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        missionCards.Clear();
        for (int i = 0; i < 4; i++)
            AddMissionCard(i);

        TopBox("DivisorVertical", window, 480f, 132f, 1f, 566f, softLine);

        // C. Panel derecho: imagen, nombre, descripcion, zona/progreso, recompensas y enemigos.
        RectTransform right = TopBox("PanelDetalle", window, 500f, 132f, 652f, 566f, Clear0);
        right.GetComponent<Image>().raycastTarget = false;

        RectTransform previewFrame = TopBox("VistaPrevia", right, 0f, 0f, 652f, 150f, MapNavy);
        GameObject rawGo = new GameObject("Imagen", typeof(RectTransform), typeof(RawImage));
        rawGo.transform.SetParent(previewFrame, false);
        RectTransform rawRect = (RectTransform)rawGo.transform;
        Stretch(rawRect);
        missionPreview = rawGo.GetComponent<RawImage>();
        missionPreview.raycastTarget = false;

        missionName = TopText("Nombre", right, 0f, 160f, 652f, 46f, "", 34f, MwTitle, TextAlignmentOptions.MidlineLeft);
        missionName.enableAutoSizing = true;
        missionName.fontSizeMin = 22f;
        missionName.fontSizeMax = 34f;

        missionDescription = TopText("Descripcion", right, 0f, 208f, 652f, 64f, "", 20f, MwDim, TextAlignmentOptions.TopLeft);
        missionDescription.textWrappingMode = TextWrappingModes.Normal;
        missionDescription.enableAutoSizing = true;
        missionDescription.fontSizeMin = 15f;
        missionDescription.fontSizeMax = 20f;

        TopText("EtiquetaZona", right, 0f, 280f, 80f, 36f, "Zona:", 20f, MwDim, TextAlignmentOptions.MidlineLeft);
        missionZone = TopText("Zona", right, 72f, 280f, 240f, 36f, "", 22f, MapIvory, TextAlignmentOptions.MidlineLeft);
        TopBox("Divisor", right, 326f, 282f, 1f, 32f, softLine);
        TopText("EtiquetaProgreso", right, 346f, 280f, 120f, 36f, "Progreso:", 20f, MwDim, TextAlignmentOptions.MidlineLeft);
        missionProgress = TopText("Progreso", right, 464f, 280f, 188f, 36f, "", 22f, MapIvory, TextAlignmentOptions.MidlineLeft);
        TopBox("Separador", right, 0f, 326f, 652f, 1f, softLine);

        // Recompensas: icono + cantidad, sin recuadros.
        TopText("EtiquetaRecompensas", right, 0f, 340f, 300f, 34f, "Recompensas", 22f, MapIvory, TextAlignmentOptions.MidlineLeft).fontStyle = FontStyles.Bold;

        RectTransform xpIconRect = TopBox("IconoXP", right, 0f, 382f, 58f, 58f, Color.white);
        missionXpIcon = xpIconRect.GetComponent<Image>();
        missionXpIcon.sprite = missionXpSprite;
        missionXpIcon.preserveAspect = true;
        missionXpIcon.raycastTarget = false;
        missionXp = TopText("XP", right, 62f, 388f, 100f, 46f, "", 24f, MapIvory, TextAlignmentOptions.MidlineLeft);

        RectTransform coinIconRect = TopBox("IconoCoronas", right, 160f, 388f, 56f, 46f, Color.white);
        missionCoinIcon = coinIconRect.GetComponent<Image>();
        missionCoinIcon.sprite = missionCoinSprite;
        missionCoinIcon.preserveAspect = true;
        missionCoinIcon.raycastTarget = false;
        missionCoins = TopText("Coronas", right, 222f, 388f, 150f, 46f, "", 24f, MapIvory, TextAlignmentOptions.MidlineLeft);

        TopBox("Divisor", right, 386f, 340f, 1f, 108f, softLine);

        // Enemigos: sus sprites, ordenados, sin marco.
        TopText("EtiquetaEnemigos", right, 406f, 340f, 200f, 34f, "Enemigos", 22f, MapIvory, TextAlignmentOptions.MidlineLeft).fontStyle = FontStyles.Bold;
        missionEnemiesHint = TopText("Aviso", right, 406f, 384f, 246f, 56f, "", 18f, MapIvory, TextAlignmentOptions.MidlineLeft);
        missionEnemiesHint.textWrappingMode = TextWrappingModes.Normal;

        enemyTiles.Clear();
        for (int i = 0; i < 4; i++)
        {
            RectTransform tile = TopBox("Enemigo_" + i, right, 406f + i * 62f, 378f, 60f, 60f, Clear0);
            Image tileBg = tile.GetComponent<Image>();
            tileBg.raycastTarget = true;

            RectTransform spriteRect = NewRect("Sprite", tile, out Image spriteImage);
            Stretch(spriteRect);
            spriteImage.preserveAspect = true;
            spriteImage.raycastTarget = false;

            TMP_Text count = TopText("Cantidad", tile, 0f, 0f, 58f, 58f, "", 20f, MapIvory, TextAlignmentOptions.BottomRight);
            count.fontStyle = FontStyles.Bold;
            count.outlineWidth = 0.25f;
            count.outlineColor = new Color32(10, 14, 22, 255);
            count.raycastTarget = false;

            MapHoverTarget hover = tile.gameObject.AddComponent<MapHoverTarget>();
            enemyTiles.Add(new EnemyTile { root = tile, border = null, sprite = spriteImage, count = count, hover = hover });
            tile.gameObject.SetActive(false);
        }

        TopBox("SeparadorInferior", right, 0f, 470f, 652f, 1f, softLine);

        // D. Boton Iniciar abajo a la derecha (sprite "Misiones" 2172 x 724 con margen transparente, escala uniforme).
        const float StartW = 380f, StartH = 127f, StartTop = 31f, StartVisH = 63f;
        RectTransform start = TopBox("BotonIniciar", window, MissionWindowW - 28f - StartW + 10f, 684f, StartW, StartH, Color.white);
        Image startImage = start.GetComponent<Image>();
        startImage.sprite = missionButtonSprite;
        startImage.type = Image.Type.Simple;
        startImage.preserveAspect = true;
        startImage.raycastTarget = true;
        startImage.raycastPadding = new Vector4(11f, 34f, 11f, 31f);   // el area de clic es solo el marco visible
        startButton = start.gameObject.AddComponent<Button>();
        startButton.targetGraphic = startImage;
        startButton.transition = Selectable.Transition.ColorTint;      // ButtonFeedback le pone el velo de hover/deshabilitado
        // El pivote va al centro del boton (sin mover su posicion): al pasar el cursor crece parejo hacia el frente,
        // en lugar de desplazarse hacia un lado desde la esquina superior izquierda.
        start.pivot = new Vector2(0.5f, 0.5f);
        start.anchoredPosition += new Vector2(StartW * 0.5f, -StartH * 0.5f);
        start.gameObject.AddComponent<HoverScale>().HoverFactor = 1.03f;
        start.gameObject.AddComponent<ButtonFeedback>().hoverStrength = 0.5f;
        TMP_Text startLabel = TopText("Texto", start, 0f, StartTop, StartW, StartVisH, "Iniciar", 30f, MapIvory, TextAlignmentOptions.Center);
        startLabel.fontStyle = FontStyles.Bold;
        startButtonLabel = null;

        // Tarjeta con nombre, estadisticas y ataques del enemigo (solo al pasar el cursor por su icono).
        missionTooltip = InfoCard("NombreEnemigo", window, "Enemigo", 20f);
        missionTooltipText = missionTooltip.GetComponentInChildren<TMP_Text>();
        missionTooltip.gameObject.SetActive(false);

        ApplyMissionFonts(rootRect);
        rootGo.SetActive(false);
    }

    // Tipografia de la ventana de misiones: Merriweather. Negrita en titulos, nombres, etiquetas de seccion,
    // valores de zona/progreso y el boton; el resto en regular.
    private void ApplyMissionFonts(RectTransform root)
    {
        if (missionFontRegular == null || missionFontBold == null)
            return;

        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            string n = label.name;
            bool bold = n == "Titulo" || n == "Nombre" || n == "Zona" || n == "Progreso" ||
                        n == "EtiquetaRecompensas" || n == "EtiquetaEnemigos" ||
                        (n == "Texto" && label.transform.parent != null && label.transform.parent.name == "BotonIniciar");

            label.font = bold ? missionFontBold : missionFontRegular;
            label.fontStyle = FontStyles.Normal;
        }
    }

    private MissionCard AddMissionCard(int index)
    {
        RectTransform root = TopBox("Mision_" + index, missionListContent, 0f, index * (MissionCardH + MissionCardGap), 432f, MissionCardH, MapNavy);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.offsetMin = new Vector2(0f, root.offsetMin.y);
        root.offsetMax = new Vector2(0f, root.offsetMax.y);
        Image background = root.GetComponent<Image>();
        background.raycastTarget = true;

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        int captured = index;
        button.onClick.AddListener(() => SelectRow(captured));

        // Franja dorada a la izquierda (solo la mision seleccionada).
        RectTransform stripeRect = TopBox("Franja", root, 0f, 0f, 5f, MissionCardH, Clear0);
        Image stripe = stripeRect.GetComponent<Image>();
        stripe.raycastTarget = false;

        RectTransform iconRect = NewRect("Icono", root, out Image icon);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(30f, 30f);
        iconRect.anchoredPosition = new Vector2(22f, 0f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text name = TopText("Nombre", root, 64f, 10f, 350f, 46f, "", 23f, MapIvory, TextAlignmentOptions.MidlineLeft);
        name.textWrappingMode = TextWrappingModes.Normal;
        name.enableAutoSizing = true;
        name.fontSizeMin = 16f;
        name.fontSizeMax = 23f;
        name.raycastTarget = false;

        // Estado: texto plano, o con una pequena etiqueta cuando es la recomendada.
        RectTransform chipRect = TopBox("Estado", root, 64f, 58f, 150f, 26f, Clear0);
        Image chip = chipRect.GetComponent<Image>();
        chip.raycastTarget = false;
        Image[] chipBorder = ThinBorder(chipRect, MapGoldDim, 2f);
        TMP_Text state = TopText("Texto", chipRect, 0f, 0f, 150f, 26f, "", 16f, MwDim, TextAlignmentOptions.Center);
        state.raycastTarget = false;

        MissionCard card = new MissionCard
        {
            root = root, button = button, background = background, border = null, icon = icon,
            name = name, chip = chip, chipBorder = chipBorder, state = state, stripe = stripe
        };
        missionCards.Add(card);
        return card;
    }

    // ---------------- Actualizacion ----------------

    // Apertura y cierre como la tienda: fundido + escala de 0,95 a 1 con smoothstep en 0,3 s (tiempo no escalado), y velo.
    private const float MissionOpenSeconds = 0.3f;
    private float missionOpenProgress;

    private void AnimateMissionWindow(bool open)
    {
        if (placePanel == null)
            return;

        float step = Application.isPlaying ? Time.unscaledDeltaTime / MissionOpenSeconds : 1f;
        missionOpenProgress = Mathf.MoveTowards(missionOpenProgress, open ? 1f : 0f, step);
        float eased = missionOpenProgress * missionOpenProgress * (3f - 2f * missionOpenProgress);
        bool visible = open || missionOpenProgress > 0f;

        if (placePanel.gameObject.activeSelf != visible)
            placePanel.gameObject.SetActive(visible);

        if (missionVeil != null && missionVeil.activeSelf != visible)
            missionVeil.SetActive(visible);

        if (!visible)
            return;

        CanvasGroup group = placePanel.GetComponent<CanvasGroup>();

        if (group != null)
        {
            group.alpha = eased;
            group.interactable = open;
            group.blocksRaycasts = open;
        }

        placePanel.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, eased);

        if (missionVeil != null)
        {
            CanvasGroup veilGroup = missionVeil.GetComponent<CanvasGroup>();

            if (veilGroup != null)
            {
                veilGroup.alpha = eased;
                veilGroup.blocksRaycasts = open;
            }
        }
    }

    // Las etiquetas del mapa se ocultan mientras la ventana esta abierta (el borde las cortaria).
    private void SetLocationTagsVisible(bool visible)
    {
        foreach (LocationView view in locationViews)
        {
            if (view.locationTag == null)
                continue;

            GameObject group = view.locationTag.transform.parent.gameObject;

            if (group.activeSelf != visible)
                group.SetActive(visible);

            return;
        }
    }

    private void RefreshMissionWindow(MapLocationDef def)
    {
        SetLocationTagsVisible(false);

        // Si el icono bajo el cursor deja de existir (otra mision), la tarjeta de nombre no debe quedarse.
        if (hoveredEnemy >= 0 && (hoveredEnemy >= enemyTiles.Count || !enemyTiles[hoveredEnemy].root.gameObject.activeSelf))
            HideEnemyTooltip();

        AnimateMissionWindow(true);

        // El encuentro seleccionado siempre pertenece al lugar abierto.
        if (!def.encounterIds.Contains(selectedId))
            selectedId = def.encounterIds[0];

        SetMissionText(missionTitle, VisibleLocationName(def));
        SetMissionText(missionRegion, "Región: " + ZoneName(def.zone));

        // Contador de la ubicacion: misiones completadas / total.
        int placeDone = 0;
        foreach (string placeId in def.encounterIds)
            if (MapProgression.GetState(placeId) == MapProgression.NodeState.Completed) placeDone++;
        SetMissionText(missionCounter, placeDone + "/" + def.encounterIds.Count);

        // Lista de misiones (todas las del lugar, con scroll si no caben).
        while (missionCards.Count < def.encounterIds.Count)
            AddMissionCard(missionCards.Count);

        string recommended = MapProgression.NextRecommended();

        Color gold = MapGold;
        Color lockedFill = new Color(0.075f, 0.095f, 0.13f, 1f);
        Color idleFill = new Color(0.098f, 0.128f, 0.175f, 1f);
        Color selectedFill = new Color(0.20f, 0.20f, 0.19f, 1f);   // fondo ligeramente destacado, calido

        for (int i = 0; i < missionCards.Count; i++)
        {
            MissionCard card = missionCards[i];
            bool visible = i < def.encounterIds.Count;

            if (card.root.gameObject.activeSelf != visible)
                card.root.gameObject.SetActive(visible);

            if (!visible)
                continue;

            string eid = def.encounterIds[i];
            MapProgression.NodeState s = MapProgression.GetState(eid);
            bool selected = eid == selectedId;
            bool evt = MapProgression.IsEvent(eid);
            bool isRecommended = eid == recommended;

            Sprite icon = null;
            Color iconColor = Color.white;
            Color fill, textColor, stateColor;
            string stateLabel;

            if (s == MapProgression.NodeState.Locked)
            {
                icon = IcLock; iconColor = new Color(0.55f, 0.60f, 0.70f, 1f);
                fill = lockedFill;
                textColor = new Color(0.52f, 0.56f, 0.64f, 1f);
                stateColor = new Color(0.45f, 0.49f, 0.58f, 1f);
                stateLabel = "Bloqueada";
            }
            else if (s == MapProgression.NodeState.Completed)
            {
                icon = IcCheck; iconColor = new Color(0.80f, 0.76f, 0.62f, 1f);
                fill = idleFill;
                textColor = new Color(0.80f, 0.78f, 0.72f, 1f);
                stateColor = new Color(0.62f, 0.66f, 0.74f, 1f);
                stateLabel = "Completada";
            }
            else
            {
                icon = evt ? IcMask : null;
                iconColor = MapIvory;
                fill = idleFill;
                textColor = MapIvory;
                stateColor = isRecommended ? MapIvory : MwDim;
                stateLabel = isRecommended ? "Recomendada" : (evt ? "Evento narrativo" : "Disponible");
            }

            if (selected)
                fill = s == MapProgression.NodeState.Locked ? new Color(0.115f, 0.14f, 0.19f, 1f) : selectedFill;

            card.background.color = fill;

            // Franja dorada: solo la mision seleccionada (mas apagada si esta bloqueada).
            card.stripe.color = selected
                ? (s == MapProgression.NodeState.Locked ? MapGoldDim : gold)
                : Color.clear;

            bool hasIcon = icon != null;
            card.icon.enabled = hasIcon;

            if (hasIcon)
            {
                if (card.icon.sprite != icon) card.icon.sprite = icon;
                card.icon.color = iconColor;
            }

            float textX = hasIcon ? 66f : 26f;
            card.name.rectTransform.anchoredPosition = new Vector2(textX, card.name.rectTransform.anchoredPosition.y);
            card.chip.rectTransform.anchoredPosition = new Vector2(textX, card.chip.rectTransform.anchoredPosition.y);

            // "Recomendada" va en una etiqueta; el resto de estados, como texto plano.
            bool tag = s == MapProgression.NodeState.Available && isRecommended && !evt;
            card.chip.rectTransform.sizeDelta = new Vector2(tag ? 150f : 300f, 26f);
            card.state.rectTransform.sizeDelta = card.chip.rectTransform.sizeDelta;
            card.state.alignment = tag ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            card.chip.color = tag ? MwFill : Color.clear;
            SetBorderColor(card.chipBorder, tag ? MapGoldDim : Color.clear);

            SetMissionText(card.name, EncounterTitle(eid));
            card.name.color = textColor;
            SetMissionText(card.state, stateLabel);
            card.state.color = stateColor;
        }

        float contentHeight = Mathf.Max(MissionCardH, def.encounterIds.Count * (MissionCardH + MissionCardGap) - MissionCardGap);
        if (!Mathf.Approximately(missionListContent.sizeDelta.y, contentHeight))
            missionListContent.sizeDelta = new Vector2(0f, contentHeight);

        RefreshMissionDetail(def);
    }

    private void RefreshMissionDetail(MapLocationDef def)
    {
        string id = selectedId;
        bool evt = MapProgression.IsEvent(id);
        EncounterData data = evt ? null : FindEncounter(id);
        MapProgression.NodeState state = MapProgression.GetState(id);
        bool done = state == MapProgression.NodeState.Completed;

        // Mismo titulo que la tarjeta de la lista.
        SetMissionText(missionName, EncounterTitle(id));

        // Descripcion narrativa si existe; si no, el subtitulo del encuentro. Si esta bloqueada, que falta.
        string description = MapProgression.DescriptionOf(id);

        if (string.IsNullOrEmpty(description))
            description = evt ? "Evento narrativo obligatorio." : MapProgression.SubtitleOf(id);

        int orderIndex = MapProgression.IndexOf(id);

        if (state == MapProgression.NodeState.Locked && orderIndex > 0)
            description += "\n<color=#8F96A6>Bloqueada: completa «" + EncounterTitle(MapProgression.Order[orderIndex - 1]) + "» para desbloquearla.</color>";
        else if (done)
            description += "\n<color=#8F96A6>Completada: las recompensas ya fueron obtenidas.</color>";

        SetMissionText(missionDescription, description);

        // Zona y progreso de la region.
        string zoneId = MapProgression.ZoneOf(id);
        string zoneKey = string.IsNullOrEmpty(zoneId) ? def.zone : zoneId;
        SetMissionText(missionZone, ZoneName(zoneKey));
        MapProgression.ZoneProgress(zoneKey, out int zoneDone, out int zoneTotal);
        SetMissionText(missionProgress, zoneDone + "/" + zoneTotal);

        // Recompensas reales del encuentro (icono + cantidad).
        Color rewardColor = done ? MwDim : MapIvory;
        missionXp.color = rewardColor;
        missionCoins.color = rewardColor;

        // La mascara rota: sin XP ni Coronas, pero entrega el Fragmento de la Mascara (icono y nombre en Recompensas).
        RelicData eventRelic = null;

        if (evt && RelicCatalog.Instance != null)
        {
            if (id == GameSession.NoelleEventId)
                eventRelic = RelicCatalog.Instance.Find(RelicRewards.FragmentTypeId);
            else if (id == GameSession.ValdoriaEventId)
                eventRelic = RelicCatalog.Instance.Find(RelicRewards.ValdoriaRewardTypeId);
        }

        ApplyRewardLayout(eventRelic != null);

        if (eventRelic != null)
        {
            if (eventRelic.icon != null && missionXpIcon.sprite != eventRelic.icon)
                missionXpIcon.sprite = eventRelic.icon;

            SetMissionText(missionXp, eventRelic.displayName);
            SetMissionText(missionCoins, "");   // sin leyenda "Sin XP · Sin Coronas"
        }
        else if (evt)
        {
            SetMissionText(missionXp, "");
            SetMissionText(missionCoins, "");

            if (missionXpIcon.gameObject.activeSelf)
                missionXpIcon.gameObject.SetActive(false);

            if (missionCoinIcon.gameObject.activeSelf)
                missionCoinIcon.gameObject.SetActive(false);
        }
        else if (data != null)
        {
            SetMissionText(missionXp, data.experienceReward + " XP");
            SetMissionText(missionCoins, data.coinReward + " Coronas");
        }
        else
        {
            SetMissionText(missionXp, "-");
            SetMissionText(missionCoins, "-");
        }

        RefreshMissionPreview(def);
        RefreshMissionEnemies(evt, data);
    }

    // Recompensas: disposicion normal (XP y Coronas en una fila) o la del evento con reliquia (icono + nombre y, debajo,
    // "Sin XP · Sin Coronas"). Se fija en cada refresco, asi no queda cambiada al elegir otra mision.
    private void ApplyRewardLayout(bool relic)
    {
        RectTransform xp = (RectTransform)missionXp.transform;
        RectTransform coins = (RectTransform)missionCoins.transform;

        if (relic)
        {
            xp.sizeDelta = new Vector2(300f, 46f);
            missionXp.fontSize = 22f;
            coins.anchoredPosition = new Vector2(0f, -440f);
            coins.sizeDelta = new Vector2(360f, 30f);
            missionCoins.fontSize = 18f;

            if (missionCoinIcon.gameObject.activeSelf)
                missionCoinIcon.gameObject.SetActive(false);

            if (!missionXpIcon.gameObject.activeSelf)
                missionXpIcon.gameObject.SetActive(true);
        }
        else
        {
            xp.sizeDelta = new Vector2(100f, 46f);
            missionXp.fontSize = 24f;
            coins.anchoredPosition = new Vector2(222f, -388f);
            coins.sizeDelta = new Vector2(150f, 46f);
            missionCoins.fontSize = 24f;

            if (!missionCoinIcon.gameObject.activeSelf)
                missionCoinIcon.gameObject.SetActive(true);

            if (!missionXpIcon.gameObject.activeSelf)
                missionXpIcon.gameObject.SetActive(true);

            if (missionXpSprite != null && missionXpIcon.sprite != missionXpSprite)
                missionXpIcon.sprite = missionXpSprite;
        }
    }

    // Vista previa: recorte del propio mapa (sprite del proyecto) centrado en el lugar; no es una imagen nueva.
    private void RefreshMissionPreview(MapLocationDef def)
    {
        if (art == null || art.worldBase == null)
            return;

        Texture texture = art.worldBase.texture;
        Rect spriteRect = art.worldBase.textureRect;
        Vector2 p = LocationPx(def);                       // origen abajo-izquierda en el lienzo 1920x1080
        const float cropW = 520f, cropH = cropW * 150f / 652f;
        float x = Mathf.Clamp(p.x - cropW * 0.5f, 0f, 1920f - cropW);
        float y = Mathf.Clamp(p.y - cropH * 0.5f, 0f, 1080f - cropH);

        Rect uv = new Rect(
            (spriteRect.x + x / 1920f * spriteRect.width) / texture.width,
            (spriteRect.y + y / 1080f * spriteRect.height) / texture.height,
            cropW / 1920f * spriteRect.width / texture.width,
            cropH / 1080f * spriteRect.height / texture.height);

        if (missionPreview.texture != texture) missionPreview.texture = texture;
        if (missionPreview.uvRect != uv) missionPreview.uvRect = uv;
    }

    private void RefreshMissionEnemies(bool evt, EncounterData data)
    {
        List<string> names = new List<string>();
        List<Sprite> sprites = new List<Sprite>();
        List<int> counts = new List<int>();

        if (data != null)
        {
            foreach (EncounterData.EnemySlot slot in data.enemies)
            {
                if (slot == null || slot.prefab == null)
                    continue;

                string key = slot.prefab.name;
                int index = names.IndexOf(key);

                if (index < 0)
                {
                    names.Add(key);
                    sprites.Add(EnemySprite(slot));
                    counts.Add(1);
                    enemyNameCache[key] = EnemyName(slot);
                    enemyStatsCache[key] = EnemyStats(slot);
                }
                else
                {
                    counts[index]++;
                }
            }
        }

        SetMissionText(missionEnemiesHint, evt ? "Evento narrativo obligatorio" : (names.Count == 0 ? "Sin datos de enemigos" : ""));

        for (int i = 0; i < enemyTiles.Count; i++)
        {
            EnemyTile tile = enemyTiles[i];
            bool visible = i < names.Count;

            if (tile.root.gameObject.activeSelf != visible)
                tile.root.gameObject.SetActive(visible);

            if (!visible)
                continue;

            tile.enemyName = enemyNameCache[names[i]];
            tile.enemyStats = enemyStatsCache.TryGetValue(names[i], out string stats) ? stats : "";
            tile.sprite.sprite = sprites[i];
            tile.sprite.enabled = sprites[i] != null;
            SetMissionText(tile.count, counts[i].ToString());

            int captured = i;
            tile.hover.entered = () => ShowEnemyTooltip(captured);
            tile.hover.exited = HideEnemyTooltip;
        }
    }

    private readonly Dictionary<string, string> enemyNameCache = new Dictionary<string, string>();
    private readonly Dictionary<string, string> enemyStatsCache = new Dictionary<string, string>();

    // "Vida 12 · Daño 2–4 · Prot. 10 %" leido del UnitData del prefab (solo informativo).
    private static string EnemyStats(EncounterData.EnemySlot slot)
    {
        BattleUnit unit = slot != null && slot.prefab != null ? slot.prefab.GetComponent<BattleUnit>() : null;
        UnitData data = unit != null ? unit.Data : null;

        if (data == null)
            return "";

        string text = "Vida " + data.maxHealth + "  ·  Velocidad " + data.speed + "\n" +
                      "Daño " + data.damageMin + "–" + data.damageMax + "  ·  Protección " + data.protectionPercent + " %\n" +
                      "Evasión " + Mathf.RoundToInt(data.evasionPercent) + " %  ·  Crítico " + Mathf.RoundToInt(data.criticalChance) +
                      " % (x" + data.criticalDamage.ToString("0.#") + ")";

        EnemyArchetypeData archetype = slot.Archetype;

        if (archetype != null)
        {
            string abilities = "";

            if (archetype.basicAttack != null)
                abilities += "\n• " + EnemyAbilityLine(archetype.basicAttack, data);

            if (archetype.specialAbilities != null)
                foreach (AbilityData special in archetype.specialAbilities)
                    if (special != null)
                        abilities += "\n• " + EnemyAbilityLine(special, data);

            if (abilities.Length > 0)
                text += "\n<color=#F1C879>Ataques</color>" + abilities;
        }

        return text;
    }

    // "Garrotazo: 3–5 de daño" / "Rugido (recarga 3)".
    private static string EnemyAbilityLine(AbilityData ability, UnitData data)
    {
        string line = ability.abilityName;

        if (ability.dealsDamage && ability.abilityType == AbilityType.Attack)
        {
            int min = Mathf.Max(1, Mathf.RoundToInt((data.damageMin * ability.damagePercent / 100f) + ability.basePower));
            int max = Mathf.Max(min, Mathf.RoundToInt((data.damageMax * ability.damagePercent / 100f) + ability.basePower));
            line += ": " + min + "–" + max + " de daño";

            if (ability.targetType == AbilityTargetType.AllEnemies)
                line += " a todos";
        }
        else if (ability.abilityType == AbilityType.Heal)
        {
            line += ": cura";
        }
        else
        {
            line += ": apoyo";
        }

        if (ability.cooldown > 0)
            line += " (recarga " + ability.cooldown + ")";

        return line;
    }

    private void ShowEnemyTooltip(int index)
    {
        if (index < 0 || index >= enemyTiles.Count || missionTooltip == null)
            return;

        hoveredEnemy = index;
        EnemyTile tile = enemyTiles[index];
        float ts = tile.root.rect.width;
        bool hasStats = !string.IsNullOrEmpty(tile.enemyStats);
        string content = hasStats
            ? "<size=125%><b>" + tile.enemyName + "</b></size>\n<size=85%><color=#D9CBA0>" + tile.enemyStats + "</color></size>"
            : tile.enemyName;

        // Texto alineado a la izquierda con margen; la tarjeta no debe bloquear el cursor sobre los otros iconos.
        missionTooltipText.alignment = hasStats ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
        missionTooltipText.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform textRect = missionTooltipText.rectTransform;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        foreach (Graphic graphic in missionTooltip.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        missionTooltipText.text = content;
        Vector2 preferred = missionTooltipText.GetPreferredValues(content);
        float height = hasStats ? Mathf.Ceil(preferred.y) + 20f : 34f;
        float width = Mathf.Ceil(preferred.x) + 34f;
        missionTooltip.sizeDelta = new Vector2(width, height);

        if (!hasStats)
        {
            // Solo nombre: debajo del icono, como antes.
            Vector3 below = missionContent.InverseTransformPoint(tile.root.position);
            below.x += ts * 0.5f;
            below.y -= ts + height * 0.5f + 4f;
            missionTooltip.position = missionContent.TransformPoint(below);
            missionTooltip.SetAsLastSibling();
            missionTooltip.gameObject.SetActive(true);
            return;
        }

        // Con estadisticas: a la derecha del icono, con la base alineada a la base del icono (crece hacia arriba).
        Vector3 local = missionContent.InverseTransformPoint(tile.root.position);   // pivote arriba-izquierda del icono
        local.x += ts + 10f + width * 0.5f;
        local.y += -ts + height * 0.5f;

        // Si no cabe a la derecha (ultimos iconos), pasa a la izquierda del icono.
        float rightEdge = missionContent.rect.width * (1f - missionContent.pivot.x) - 16f;

        if (local.x + width * 0.5f > rightEdge)
            local.x -= ts + 20f + width;
        missionTooltip.position = missionContent.TransformPoint(local);
        missionTooltip.SetAsLastSibling();
        missionTooltip.gameObject.SetActive(true);
    }

    private int hoveredEnemy = -1;

    private void HideEnemyTooltip()
    {
        hoveredEnemy = -1;

        if (missionTooltip != null)
            missionTooltip.gameObject.SetActive(false);
    }

    private static void SetMissionText(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

#if UNITY_EDITOR
    // Sustituye el panel de lugar antiguo de la escena por la ventana de misiones, conservando el resto de la interfaz.
    [ContextMenu("Reconstruir ventana de misiones")]
    public void RebuildMissionWindowInEditor()
    {
        if (canvas == null)
            return;

        if (font == null)
            font = GameFonts.Regular;

        missionCloseSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Iconos/Ayuda/UI_Cerrar.png");
        missionButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/Misiones.png");
        missionWindowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/opciones 1.png");
        missionFontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherRegular_SDF.asset");
        missionFontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MerriweatherBold_SDF.asset");
        missionXpSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Iconos/XP.png");
        missionCoinSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Iconos/coronas.png");

        Transform area = canvas.transform.Find("MapArea");
        Transform parent = area.Find("PanelesUI");

        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyImmediate(parent.GetChild(i).gameObject);

        // Referencias del panel antiguo (ya destruido).
        rowRoots = new RectTransform[2];
        rowBackgrounds = new Image[2];
        rowButtons = new Button[2];
        rowIcons = new Image[2];
        rowNames = new Text[2];
        rowStates = new Text[2];
        placeDetails = null; placeTitle = null; placeZone = null;
        infoTitle = null; infoSubtitle = null; infoRewards = null; infoEnemies = null; infoNote = null;
        infoAccent = null;
        startButtonLabel = null;

        // Restos de una ventana anterior en el lienzo de la tienda.
        if (shop != null && shop.WindowParent != null)
        {
            Transform host = shop.WindowParent;

            for (int i = host.childCount - 1; i >= 0; i--)
            {
                string childName = host.GetChild(i).name;

                if (childName == "PanelLugar" || childName == "VeloMisiones")
                    DestroyImmediate(host.GetChild(i).gameObject);
            }
        }

        BuildMissionWindow(parent);
        EditorUtility.SetDirty(this);
    }
#endif
}
