using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Marcadores de mision del mapa ilustrado (medallones) y tarjetas con el nombre de
// las ubicaciones pintadas en el mapa. Todo sale de sprites y componentes que ya
// existen; no hay brillos, particulas ni animaciones.
public partial class WorldMapController
{
    [Header("Marcadores de mision")]
    [Tooltip("Simbolo de las misiones disponibles (espadas cruzadas). Si falta se usa la estrella.")]
    [SerializeField] private Sprite questIcon;

    private const float MedallionSize = 64f;

    // Paleta del mapa: #10151E, #171F2B, #D4AF6A, #B7955B, #E8DCC6.
    private static readonly Color MapNavy = new Color(0.063f, 0.082f, 0.118f, 1f);
    private static readonly Color MapNavyCard = new Color(0.063f, 0.082f, 0.118f, 0.94f);
    private static readonly Color MapGold = new Color(0.831f, 0.686f, 0.416f, 1f);
    private static readonly Color MapGoldDim = new Color(0.718f, 0.584f, 0.357f, 1f);
    private static readonly Color MapIvory = new Color(0.910f, 0.863f, 0.776f, 1f);

    [Tooltip("Muestra tambien las etiquetas de los lugares secundarios (pabellon, catedral, nido).")]
    [SerializeField] private bool showSecondaryLabels = false;

    // Etiquetas informativas de las ubicaciones ilustradas (no son misiones). Solo texto visible:
    // los identificadores internos de los nodos no cambian. "key" coincide con el id del lugar
    // jugable al que acompana la etiqueta (se oculta mientras se muestra la tarjeta de su nodo).
    // Posicion en el lienzo 1920x1080 con origen arriba-izquierda.
    private static readonly (string key, string text, float x, float y, bool primary)[] LocationTags =
    {
        ("camp", "Campamento del Alba", 300f, 727f, true),
        ("ruins", "Ruinas de Liria", 330f, 932f, true),
        ("clearing", "Gruta de los Susurros", 1190f, 836f, true),
        ("trail", "Refugio del Vigía", 1420f, 688f, true),
        ("shrine", "Santuario Velado", 1735f, 878f, true),
        ("gate", "Trono Sellado", 1590f, 215f, true),
        ("city", "Ciudad de Valdoria", 872f, 626f, true),
        ("pavilion", "Pabellón de Caza", 395f, 337f, true),
        ("cathedral", "Catedral del Sol", 862f, 397f, true),
        ("nest", "Nido de Wyverns", 1370f, 327f, true),
    };

    // ---------------- Tarjetas ----------------

    // Tarjeta pequena: fondo azul marino, borde dorado fino y texto marfil.
    private RectTransform InfoCard(string name, Transform parent, string text, float fontSize)
    {
        RectTransform card = Local(name, parent, Vector2.zero, new Vector2(100f, 34f), MapNavyCard, null);

        RectTransform textRect = HudRect("Texto", card, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TMP_Text label = HudText(textRect, text, fontSize, MapIvory, TextAlignmentOptions.Center);

        float width = Mathf.Ceil(label.GetPreferredValues(text).x) + 30f;
        card.sizeDelta = new Vector2(width, 34f);

        const float t = 2f;
        Strip(card, "Borde_Sup", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -t), Vector2.zero);
        Strip(card, "Borde_Inf", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, t));
        Strip(card, "Borde_Izq", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(t, 0f));
        Strip(card, "Borde_Der", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-t, 0f), Vector2.zero);
        return card;
    }

    private static void Strip(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = NewRect(name, parent, out Image image);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        image.color = MapGoldDim;
    }

    // Etiqueta de un lugar jugable: siempre pegada a su marcador (centrada, justo debajo; encima si debajo chocaria
    // con los contadores inferiores). Las demas conservan su posicion fija.
    private const float TagOffset = 62f;

    private Vector2 TagPosition(string key, float x, float yFromBottom)
    {
        MapLocationDef def = FindLocation(key);

        if (def == null)
            return new Vector2(x, yFromBottom);

        Vector2 p = LocationPx(def);   // origen abajo-izquierda
        bool above = key == "clearing";
        return new Vector2(p.x, p.y + (above ? TagOffset : -TagOffset));
    }

    private RectTransform BuildLocationLabels(RectTransform area)
    {
        RectTransform group = Group("EtiquetasUbicacion", area);

        foreach ((string key, string text, float x, float y, bool primary) tag in LocationTags)
        {
            RectTransform card = InfoCard("Etiqueta_" + tag.key, group, tag.text, 19f);
            card.anchorMin = card.anchorMax = Vector2.zero;
            card.anchoredPosition = TagPosition(tag.key, tag.x, 1080f - tag.y);
            card.gameObject.SetActive(tag.primary || showSecondaryLabels);
        }

        return group;
    }

    // Enlaza cada marcador con la etiqueta de su lugar para ocultarla mientras se ve la tarjeta del nodo.
    private void LinkLocationTags(Transform area)
    {
        Transform group = area.Find("MarcadoresInteractivos/EtiquetasUbicacion");

        if (group == null)
            return;

        foreach (LocationView view in locationViews)
        {
            Transform tag = group.Find("Etiqueta_" + view.id);
            view.locationTag = tag != null ? tag.gameObject : null;

            if (tag == null)
                continue;

            // Las etiquetas de lugares jugables responden como el marcador: crecen ~5 % y abren el mismo lugar.
            Image background = tag.GetComponent<Image>();
            background.raycastTarget = true;

            if (tag.GetComponent<Button>() == null)
            {
                Button button = tag.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.None;
            }

            if (tag.GetComponent<HoverScale>() == null)
                tag.gameObject.AddComponent<HoverScale>().HoverFactor = 1.05f;

            if (tag.GetComponent<MapHoverTarget>() == null)
                tag.gameObject.AddComponent<MapHoverTarget>();
        }
    }

    // ---------------- Medallones ----------------

    private LocationView BuildMedallion(Transform parent, MapLocationDef def, Vector2 center)
    {
        float size = MedallionSize;
        RectTransform container = Place("Lugar_" + def.id, parent, center, new Vector2(size + 16f, size + 16f), Clear0, null);
        RectTransform badge = Local("Indicador", container, Vector2.zero, new Vector2(size, size), Clear0, null);

        Local("Sombra", badge, new Vector2(0f, -5f), new Vector2(size + 8f, size * 0.55f), new Color(0f, 0f, 0f, 0.35f), SpSoft);
        // Halo dorado detras del marcador: solo se enciende (con un pulso suave) en el lugar recomendado.
        RectTransform glow = Local("Brillo", container, Vector2.zero, new Vector2(size * 2.3f, size * 2.3f), Clear0, SpSoft);
        glow.SetAsFirstSibling();
        RectTransform ring = Local("Anillo", badge, Vector2.zero, new Vector2(size + 24f, size + 24f), Color.white, SpRing);
        ring.gameObject.SetActive(false);

        RectTransform frame = Local("Marco", badge, Vector2.zero, new Vector2(size, size), MapGold, SpCircle);
        RectTransform body = Local("Cuerpo", badge, Vector2.zero, new Vector2(size - 8f, size - 8f), MapNavy, SpCircle);
        RectTransform icon = Local("Icono", body, Vector2.zero, new Vector2(34f, 34f), Color.white, IcLock);

        // Combate disponible: el icono "Combate" es un medallon completo (anillo dorado + fondo azul + espadas), asi que
        // sustituye a Marco y Cuerpo. Su anillo ocupa ~79 % del lienzo del sprite: se amplia para igualar los 76 px.
        float questSize = size / 0.79f;
        RectTransform quest = Local("Combate", badge, Vector2.zero, new Vector2(questSize, questSize), Color.white, questIcon != null ? questIcon : IcStar);
        quest.GetComponent<Image>().preserveAspect = true;
        frame.gameObject.SetActive(false);   // estado inicial = disponible (en ejecucion lo fija RefreshMedallion)
        body.gameObject.SetActive(false);

        // Zona de clic algo mayor que el medallon.
        RectTransform hit = Local("Zona", container, Vector2.zero, new Vector2(size + 12f, size + 12f), new Color(1f, 1f, 1f, 0.004f), SpCircle);
        Image hitImage = hit.GetComponent<Image>();
        hitImage.raycastTarget = true;
        Button button = hit.gameObject.AddComponent<Button>();
        button.targetGraphic = hitImage;
        button.transition = Selectable.Transition.None;
        MapHoverTarget hover = hit.gameObject.AddComponent<MapHoverTarget>();

        // El medallon crece suavemente al pasar el cursor (mismo efecto que los botones).
        badge.gameObject.AddComponent<HoverScale>().HoverFactor = 1.08f;

        Text label = Label("Nombre", container, Vector2.zero, Vector2.zero, 18, TextAnchor.MiddleCenter, MapIvory);
        label.text = def.displayName;
        label.gameObject.SetActive(false);

        return new LocationView
        {
            id = def.id, rect = container, badge = badge, button = button,
            body = body.GetComponent<Image>(), frame = frame.GetComponent<Image>(),
            icon = icon.GetComponent<Image>(), glow = glow.GetComponent<Image>(),
            ring = ring.GetComponent<Image>(), label = label, hover = hover,
            quest = quest.GetComponent<Image>()
        };
    }

    // Tres estados: disponible (borde dorado + espadas), bloqueado (azul grisaceo + candado)
    // y completado (mas discreto + verificacion). Sin brillos ni animaciones.
    private void RefreshMedallion(LocationView view, PlaceState state, bool evt, bool open, bool hover)
    {
        Color frame = MapGold, body = MapNavy, iconColor = MapIvory;
        Sprite icon = null;
        bool showQuest = false;

        if (state == PlaceState.Completed)
        {
            frame = MapGoldDim;
            icon = IcCheck;
            iconColor = new Color(0.80f, 0.76f, 0.62f, 0.95f);
        }
        else if (state == PlaceState.Locked)
        {
            frame = new Color(0.43f, 0.50f, 0.60f, 1f);
            body = new Color(0.18f, 0.22f, 0.29f, 1f);
            icon = IcLock;
            iconColor = new Color(0.72f, 0.78f, 0.88f, 1f);
        }
        else if (evt)
        {
            icon = IcMask;
        }
        else
        {
            showQuest = true;
        }

        view.frame.color = frame;
        view.body.color = body;

        // Disponible: solo el medallon "Combate"; bloqueado, completado y evento: marco + cuerpo + icono.
        if (view.quest.gameObject.activeSelf != showQuest) view.quest.gameObject.SetActive(showQuest);
        if (view.frame.gameObject.activeSelf == showQuest) view.frame.gameObject.SetActive(!showQuest);
        if (view.body.gameObject.activeSelf == showQuest) view.body.gameObject.SetActive(!showQuest);
        if (view.icon.gameObject.activeSelf == showQuest) view.icon.gameObject.SetActive(!showQuest);

        if (!showQuest)
        {
            if (view.icon.sprite != icon) view.icon.sprite = icon;
            view.icon.color = iconColor;
        }

        bool recommendedHere = state == PlaceState.Recommended;
        if (view.ring.gameObject.activeSelf != recommendedHere) view.ring.gameObject.SetActive(recommendedHere);

        // Guia visual: el lugar que toca jugar respira con un halo dorado suave (sin movimiento ni particulas).
        // Lo que todavia no se ha descubierto no dibuja marcador ni candado. El nombre del lugar se mantiene visible, pero
        // sin interaccion (ni hover ni clic) hasta que el lugar este disponible.
        bool revealed = state != PlaceState.Locked;
        if (view.rect.gameObject.activeSelf != revealed) view.rect.gameObject.SetActive(revealed);

        if (view.locationTag != null)
        {
            Image tagBackground = view.locationTag.GetComponent<Image>();
            if (tagBackground.raycastTarget != revealed) tagBackground.raycastTarget = revealed;
        }

        float glowAlpha = 0f;
        float pulse = Application.isPlaying ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f) : 1f;

        if (recommendedHere)
        {
            glowAlpha = 0.70f + 0.30f * pulse;
            Color ringColor = new Color(1f, 0.86f, 0.40f, 0.55f + 0.45f * pulse);
            if (view.ring.color != ringColor) view.ring.color = ringColor;
        }

        Color glowColor = new Color(1f, 0.80f, 0.28f, glowAlpha);
        if (view.glow.color != glowColor) view.glow.color = glowColor;

        // Medallon y etiqueta de su lugar crecen juntos (la posicion no cambia).
        HoverScale badgeScale = view.badge.GetComponent<HoverScale>();
        if (badgeScale != null) badgeScale.ExternalHover = hover || open;

        if (view.locationTag != null)
        {
            HoverScale tagScale = view.locationTag.GetComponent<HoverScale>();
            if (tagScale != null) tagScale.ExternalHover = hover || open;
        }
    }

#if UNITY_EDITOR
    // Reconstruye marcadores y etiquetas en la escena guardada sin regenerar el resto de la interfaz.
    [ContextMenu("Reconstruir marcadores y etiquetas del mapa")]
    public void RebuildMedallionsInEditor()
    {
        if (canvas == null)
            return;

        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Transform area = canvas.transform.Find("MapArea");
        Transform markers = area.Find("MarcadoresInteractivos");
        Transform places = markers.Find("Lugares");

        for (int i = places.childCount - 1; i >= 0; i--)
            DestroyImmediate(places.GetChild(i).gameObject);

        foreach (Transform old in new[] { area.Find("EtiquetasUbicacion"), markers.Find("EtiquetasUbicacion") })
            if (old != null)
                DestroyImmediate(old.gameObject);

        locationViews.Clear();
        BuildLocationMarkers(places);
        BuildLocationLabels((RectTransform)markers).SetSiblingIndex(places.GetSiblingIndex());
        LinkLocationTags(area);
        EditorUtility.SetDirty(this);
    }
#endif
}
