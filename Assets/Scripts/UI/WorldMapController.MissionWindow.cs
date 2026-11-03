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
        RectTransform window = Place("PanelLugar", parent, new Vector2(960f, 1080f - 560f), new Vector2(MissionWindowW, MissionWindowH), MwFill, null);
        window.GetComponent<Image>().raycastTarget = true;   // no deja pasar el clic a la capa que cierra
        ThinBorder(window, MapGoldDim, 2f);
        placePanel = window;

        // Cabecera.
        missionTitle = TopText("Titulo", window, 40f, 18f, 900f, 54f, "", 40f, MwTitle, TextAlignmentOptions.MidlineLeft);
        missionRegion = TopText("Region", window, 42f, 72f, 700f, 32f, "", 24f, MwDim, TextAlignmentOptions.MidlineLeft);
        TopBox("Separador", window, 28f, 114f, MissionWindowW - 56f, 2f, new Color(MapGoldDim.r, MapGoldDim.g, MapGoldDim.b, 0.6f));

        // Boton X (UI_Cerrar), el mismo de PAUSA, tienda e inventario.
        RectTransform close = TopBox("BotonCerrar", window, MissionWindowW - 34f, -14f, 48f, 48f, Color.white);   // 48 x 48, sobresale ~14 px de la esquina, como en PAUSA y tienda
        Image closeImage = close.GetComponent<Image>();
        closeImage.sprite = missionCloseSprite;
        closeImage.preserveAspect = true;
        closeImage.raycastTarget = true;
        closeImage.raycastPadding = new Vector4(-12f, -12f, -12f, -12f);
        placeCloseButton = close.gameObject.AddComponent<Button>();
        placeCloseButton.targetGraphic = closeImage;
        placeCloseButton.transition = Selectable.Transition.None;
        close.gameObject.AddComponent<HoverScale>().HoverFactor = 1.08f;

        // Panel izquierdo: lista de misiones.
        RectTransform left = TopBox("PanelMisiones", window, 28f, 132f, 440f, 566f, MwPanel);
        ThinBorder(left, MwLine, 2f);
        TopText("Titulo", left, 22f, 12f, 300f, 38f, "Misiones", 26f, MwTitle, TextAlignmentOptions.MidlineLeft);
        TopBox("Separador", left, 18f, 58f, 404f, 2f, new Color(MapGoldDim.r, MapGoldDim.g, MapGoldDim.b, 0.5f));

        RectTransform viewport = TopBox("Lista", left, 12f, 70f, 416f, 484f, Clear0);
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

        // Panel derecho: detalle del encuentro.
        RectTransform right = TopBox("PanelDetalle", window, 484f, 132f, 668f, 566f, MwPanel);
        ThinBorder(right, MwLine, 2f);

        RectTransform previewFrame = TopBox("VistaPrevia", right, 16f, 14f, 636f, 150f, MapNavy);
        GameObject rawGo = new GameObject("Imagen", typeof(RectTransform), typeof(RawImage));
        rawGo.transform.SetParent(previewFrame, false);
        RectTransform rawRect = (RectTransform)rawGo.transform;
        Stretch(rawRect);
        rawRect.offsetMin = new Vector2(2f, 2f);
        rawRect.offsetMax = new Vector2(-2f, -2f);
        missionPreview = rawGo.GetComponent<RawImage>();
        missionPreview.raycastTarget = false;
        ThinBorder(previewFrame, MapGoldDim, 2f);

        missionName = TopText("Nombre", right, 16f, 176f, 636f, 44f, "", 30f, MwTitle, TextAlignmentOptions.Center);
        missionName.enableAutoSizing = true;
        missionName.fontSizeMin = 20f;
        missionName.fontSizeMax = 30f;
        TopBox("Separador", right, 60f, 224f, 548f, 2f, new Color(MapGoldDim.r, MapGoldDim.g, MapGoldDim.b, 0.5f));

        InfoRow(right, "Zona", 238f, "Zona:", out missionZone);
        InfoRow(right, "Estado", 286f, "Estado:", out missionState);
        InfoRow(right, "XP", 334f, "XP:", out missionXp);
        InfoRow(right, "Coronas", 382f, "Coronas:", out missionCoins);

        RectTransform enemies = TopBox("Enemigos", right, 16f, 436f, 636f, 116f, MwPanel);
        ThinBorder(enemies, MwLine, 2f);
        TopText("Etiqueta", enemies, 18f, 6f, 200f, 30f, "Enemigos:", 20f, MwDim, TextAlignmentOptions.MidlineLeft);
        missionEnemiesHint = TopText("Aviso", enemies, 18f, 40f, 600f, 60f, "", 20f, MapIvory, TextAlignmentOptions.MidlineLeft);

        enemyTiles.Clear();
        for (int i = 0; i < 4; i++)
        {
            RectTransform tile = TopBox("Enemigo_" + i, enemies, 18f + i * 88f, 36f, 72f, 72f, MapNavy);
            Image tileBg = tile.GetComponent<Image>();
            tileBg.raycastTarget = true;
            Image[] tileBorder = ThinBorder(tile, MapGoldDim, 2f);

            RectTransform spriteRect = NewRect("Sprite", tile, out Image spriteImage);
            Stretch(spriteRect);
            spriteRect.offsetMin = new Vector2(6f, 6f);
            spriteRect.offsetMax = new Vector2(-6f, -6f);
            spriteImage.preserveAspect = true;

            TMP_Text count = TopText("Cantidad", tile, 0f, 0f, 66f, 68f, "", 24f, MapIvory, TextAlignmentOptions.BottomRight);
            count.fontStyle = FontStyles.Bold;
            count.outlineWidth = 0.25f;
            count.outlineColor = new Color32(10, 14, 22, 255);

            MapHoverTarget hover = tile.gameObject.AddComponent<MapHoverTarget>();
            enemyTiles.Add(new EnemyTile { root = tile, border = tileBorder[0], sprite = spriteImage, count = count, hover = hover });
            tile.gameObject.SetActive(false);
        }

        // Boton Iniciar con el sprite "Misiones" (2172 x 724, con margen transparente). Se dibuja como imagen simple a
        // escala uniforme (sin deformar las esquinas): 420 x 140 de lienzo = marco visible de ~398 x 69.
        const float StartW = 420f, StartH = 140f, StartTop = 34f, StartVisH = 69f;
        RectTransform start = TopBox("BotonIniciar", window, (MissionWindowW - StartW) * 0.5f, 684f, StartW, StartH, Color.white);
        Image startImage = start.GetComponent<Image>();
        startImage.sprite = missionButtonSprite;
        startImage.type = Image.Type.Simple;
        startImage.preserveAspect = true;
        startImage.raycastTarget = true;
        startImage.raycastPadding = new Vector4(12f, 38f, 12f, 34f);   // el area de clic es solo el marco visible
        startButton = start.gameObject.AddComponent<Button>();
        startButton.targetGraphic = startImage;
        startButton.transition = Selectable.Transition.ColorTint;      // ButtonFeedback le pone el velo de hover/deshabilitado
        // Hover como el de los botones de tienda e inventario, a la mitad: escala 1,04 (en vez de 1,08) y velo al 50 %.
        start.gameObject.AddComponent<HoverScale>().HoverFactor = 1.04f;
        start.gameObject.AddComponent<ButtonFeedback>().hoverStrength = 0.5f;
        TMP_Text startLabel = TopText("Texto", start, 0f, StartTop, StartW, StartVisH, "Iniciar", 30f, MapIvory, TextAlignmentOptions.Center);
        startLabel.fontStyle = FontStyles.Bold;
        startButtonLabel = null;

        // Tarjeta con el nombre del enemigo (solo al pasar el cursor por su icono).
        missionTooltip = InfoCard("NombreEnemigo", window, "Enemigo", 20f);
        missionTooltipText = missionTooltip.GetComponentInChildren<TMP_Text>();
        missionTooltip.gameObject.SetActive(false);

        window.gameObject.SetActive(false);
    }

    private MissionCard AddMissionCard(int index)
    {
        RectTransform root = TopBox("Mision_" + index, missionListContent, 0f, index * (MissionCardH + MissionCardGap), 416f, MissionCardH, MapNavy);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.offsetMin = new Vector2(0f, root.offsetMin.y);
        root.offsetMax = new Vector2(0f, root.offsetMax.y);
        Image background = root.GetComponent<Image>();
        background.raycastTarget = true;
        Image[] border = ThinBorder(root, MwLine, 2f);

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        int captured = index;
        button.onClick.AddListener(() => SelectRow(captured));

        RectTransform iconRect = NewRect("Icono", root, out Image icon);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(34f, 34f);
        iconRect.anchoredPosition = new Vector2(16f, 0f);
        icon.preserveAspect = true;

        TMP_Text name = TopText("Nombre", root, 64f, 8f, 336f, 50f, "", 21f, MapIvory, TextAlignmentOptions.MidlineLeft);
        name.textWrappingMode = TextWrappingModes.Normal;
        name.enableAutoSizing = true;
        name.fontSizeMin = 15f;
        name.fontSizeMax = 21f;

        RectTransform chipRect = TopBox("Estado", root, 64f, 62f, 230f, 26f, MwFill);
        Image chip = chipRect.GetComponent<Image>();
        Image[] chipBorder = ThinBorder(chipRect, MapGoldDim, 2f);
        TMP_Text state = TopText("Texto", chipRect, 0f, 0f, 230f, 26f, "", 16f, MapIvory, TextAlignmentOptions.Center);

        MissionCard card = new MissionCard
        {
            root = root, button = button, background = background, border = border, icon = icon,
            name = name, chip = chip, chipBorder = chipBorder, state = state
        };
        missionCards.Add(card);
        return card;
    }

    // ---------------- Actualizacion ----------------

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

        if (!placePanel.gameObject.activeSelf)
            placePanel.gameObject.SetActive(true);

        // El encuentro seleccionado siempre pertenece al lugar abierto.
        if (!def.encounterIds.Contains(selectedId))
            selectedId = def.encounterIds[0];

        SetMissionText(missionTitle, VisibleLocationName(def));
        SetMissionText(missionRegion, "Región: " + ZoneName(def.zone));

        // Lista de misiones (todas las del lugar, con scroll si no caben).
        while (missionCards.Count < def.encounterIds.Count)
            AddMissionCard(missionCards.Count);

        string recommended = MapProgression.NextRecommended();

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
            bool boss = eid == BossId;
            bool evt = MapProgression.IsEvent(eid);

            Sprite icon;
            Color iconColor = Color.white;
            Color borderColor, fill, textColor, chipColor;

            if (s == MapProgression.NodeState.Locked)
            {
                icon = IcLock; iconColor = new Color(0.62f, 0.68f, 0.78f, 1f);
                fill = new Color(0.11f, 0.14f, 0.19f, 1f);
                borderColor = new Color(0.30f, 0.36f, 0.45f, 1f);
                textColor = new Color(0.58f, 0.62f, 0.70f, 1f);
                chipColor = borderColor;
            }
            else if (s == MapProgression.NodeState.Completed)
            {
                icon = IcCheck; iconColor = new Color(0.80f, 0.76f, 0.62f, 1f);
                fill = new Color(0.075f, 0.10f, 0.14f, 1f);
                borderColor = new Color(0.55f, 0.46f, 0.30f, 1f);
                textColor = new Color(0.80f, 0.78f, 0.72f, 1f);
                chipColor = borderColor;
            }
            else
            {
                icon = evt ? IcMask : (eid == recommended ? IcStar : SpCircle);
                iconColor = evt ? MapIvory : (eid == recommended ? MapGold : new Color(0.55f, 0.72f, 1f, 1f));
                fill = MapNavy;
                borderColor = MapGoldDim;
                textColor = MapIvory;
                chipColor = MapGoldDim;
            }

            if (selected)
            {
                fill = Color.Lerp(fill, new Color(0.17f, 0.22f, 0.32f, 1f), 0.65f);
                borderColor = MapGold;
            }

            card.background.color = fill;
            SetBorderColor(card.border, borderColor);

            if (card.icon.sprite != icon) card.icon.sprite = icon;
            card.icon.color = iconColor;
            card.icon.rectTransform.sizeDelta = icon == SpCircle ? new Vector2(14f, 14f) : new Vector2(34f, 34f);

            SetMissionText(card.name, EncounterTitle(eid));
            card.name.color = textColor;
            SetMissionText(card.state, StateText(eid));
            card.state.color = textColor;
            SetBorderColor(card.chipBorder, chipColor);
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

        // Mismo titulo que la tarjeta de la lista.
        SetMissionText(missionName, EncounterTitle(id));
        string zoneId = MapProgression.ZoneOf(id);
        SetMissionText(missionZone, ZoneName(string.IsNullOrEmpty(zoneId) ? def.zone : zoneId));
        SetMissionText(missionState, StateText(id));
        missionState.color = state == MapProgression.NodeState.Locked ? MwDim : (state == MapProgression.NodeState.Completed ? new Color(0.80f, 0.76f, 0.62f, 1f) : MwTitle);

        if (evt)
        {
            SetMissionText(missionXp, "Sin XP");
            SetMissionText(missionCoins, "Sin Coronas");
        }
        else if (data != null)
        {
            bool done = state == MapProgression.NodeState.Completed;
            SetMissionText(missionXp, data.experienceReward.ToString() + (done ? "  (ya obtenida)" : ""));
            SetMissionText(missionCoins, data.coinReward.ToString() + (done ? "  (ya obtenidas)" : ""));
        }
        else
        {
            SetMissionText(missionXp, "-");
            SetMissionText(missionCoins, "-");
        }

        RefreshMissionPreview(def);
        RefreshMissionEnemies(evt, data);
    }

    // Vista previa: recorte del propio mapa (sprite del proyecto) centrado en el lugar; no es una imagen nueva.
    private void RefreshMissionPreview(MapLocationDef def)
    {
        if (art == null || art.worldBase == null)
            return;

        Texture texture = art.worldBase.texture;
        Rect spriteRect = art.worldBase.textureRect;
        Vector2 p = LocationPx(def);                       // origen abajo-izquierda en el lienzo 1920x1080
        const float cropW = 520f, cropH = cropW * 150f / 636f;
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
            tile.sprite.sprite = sprites[i];
            tile.sprite.enabled = sprites[i] != null;
            SetMissionText(tile.count, counts[i].ToString());

            int captured = i;
            tile.hover.entered = () => ShowEnemyTooltip(captured);
            tile.hover.exited = HideEnemyTooltip;
        }
    }

    private readonly Dictionary<string, string> enemyNameCache = new Dictionary<string, string>();

    private void ShowEnemyTooltip(int index)
    {
        if (index < 0 || index >= enemyTiles.Count || missionTooltip == null)
            return;

        hoveredEnemy = index;
        EnemyTile tile = enemyTiles[index];
        missionTooltipText.text = tile.enemyName;
        missionTooltip.sizeDelta = new Vector2(Mathf.Ceil(missionTooltipText.GetPreferredValues(tile.enemyName).x) + 30f, 34f);

        // Debajo del icono, en el espacio de la ventana (la posicion del icono no cambia ni se tapa otra fila).
        Vector3 local = placePanel.InverseTransformPoint(tile.root.position);
        local.x += 36f;                 // el icono tiene el pivote arriba-izquierda: se centra la tarjeta en el
        local.y -= 72f + 34f * 0.5f + 4f;   // icono y se coloca justo debajo
        missionTooltip.position = placePanel.TransformPoint(local);
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
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        missionCloseSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Iconos/Ayuda/UI_Cerrar.png");
        missionButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Buttons/Misiones.png");

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

        BuildMissionWindow(parent);
        EditorUtility.SetDirty(this);
    }
#endif
}
