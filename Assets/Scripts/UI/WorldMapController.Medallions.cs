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

    private const float MedallionSize = 50f;

    // El medallon flota sobre el edificio (no encima) para dejar ver la ilustracion; su etiqueta sigue bajo el lugar.
    // La gruta, cuya etiqueta va arriba, conserva su posicion.
    private const float MedallionLift = 40f;

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
        float extra = key == "shrine" ? 14f : 0f;   // la placa del santuario baja un poco para no tapar la base de la capilla
        return new Vector2(p.x, p.y + (above ? TagOffset : -(TagOffset + extra)));
    }

    private RectTransform BuildLocationLabels(RectTransform area)
    {
        RectTransform group = Group("EtiquetasUbicacion", area);

        foreach ((string key, string text, float x, float y, bool primary) tag in LocationTags)
        {
            RectTransform card = InfoCard("Etiqueta_" + tag.key, group, tag.text, 17f);
            card.sizeDelta = new Vector2(card.sizeDelta.x, 30f);
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

            if (tag.GetComponent<CanvasGroup>() == null)
                tag.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = true;

            if (tag.GetComponent<MapTagHoverFade>() == null)
                tag.gameObject.AddComponent<MapTagHoverFade>();

            // El medallon se acopla al borde izquierdo de la placa de su nombre: la ilustracion queda libre y
            // marcador y nombre se leen como una sola pieza.
            RectTransform tagRect = (RectTransform)tag;
            view.rect.anchoredPosition = tagRect.anchoredPosition + new Vector2(-(tagRect.sizeDelta.x * 0.5f + 31f), 0f);
        }

        // Las placas de lugares sin mision (ciudad, pabellon, catedral, nido) son informativas: se atenuan.
        for (int i = 0; i < group.childCount; i++)
        {
            Transform tag = group.GetChild(i);
            bool linked = false;

            foreach (LocationView view in locationViews)
                if (view.locationTag != null && view.locationTag.transform == tag)
                    linked = true;

            if (linked)
                continue;

            CanvasGroup cg = tag.GetComponent<CanvasGroup>();

            if (cg == null)
                cg = tag.gameObject.AddComponent<CanvasGroup>();

            cg.alpha = 0.72f;
            cg.blocksRaycasts = true;

            // Informativas: sin clic, pero al pasar el cursor se leen con claridad.
            Image infoBackground = tag.GetComponent<Image>();

            if (infoBackground != null)
                infoBackground.raycastTarget = true;

            MapTagHoverFade infoFade = tag.GetComponent<MapTagHoverFade>();

            if (infoFade == null)
                infoFade = tag.gameObject.AddComponent<MapTagHoverFade>();

            infoFade.restAlpha = 0.72f;
        }
    }

    // ---------------- Medallones ----------------

    private LocationView BuildMedallion(Transform parent, MapLocationDef def, Vector2 center)
    {
        float size = MedallionSize;
        float lift = 0f;   // el medallon se acopla despues a la placa de su nombre (LinkLocationTags)
        RectTransform container = Place("Lugar_" + def.id, parent, center + new Vector2(0f, lift), new Vector2(size + 16f, size + 16f), Clear0, null);
        RectTransform badge = Local("Indicador", container, Vector2.zero, new Vector2(size, size), Clear0, null);

        Local("Sombra", badge, new Vector2(0f, -5f), new Vector2(size + 8f, size * 0.55f), new Color(0f, 0f, 0f, 0.35f), SpSoft);
        // Halo dorado detras del marcador: solo se enciende (con un pulso suave) en el lugar recomendado.
        RectTransform glow = Local("Brillo", container, Vector2.zero, new Vector2(size * 1.9f, size * 1.9f), Clear0, SpSoft);
        glow.SetAsFirstSibling();
        RectTransform ring = Local("Anillo", badge, Vector2.zero, new Vector2(size + 16f, size + 16f), Color.white, SpRing);
        ring.gameObject.SetActive(false);

        // Pista del lugar que sigue al recomendado (aun bloqueado): un anillo fino y tenue que respira, sin icono.
        RectTransform hintRing = Local("Pista", container, Vector2.zero, new Vector2(size * 0.95f, size * 0.95f), new Color(MapGold.r, MapGold.g, MapGold.b, 0f), SpRing);
        hintRing.gameObject.SetActive(false);

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
            quest = quest.GetComponent<Image>(), hint = hintRing.GetComponent<Image>()
        };
    }

    // Tres estados: disponible (borde dorado + espadas), bloqueado (azul grisaceo + candado)
    // y completado (mas discreto + verificacion). Sin brillos ni animaciones.
    private void RefreshMedallion(LocationView view, PlaceState state, bool evt, bool open, bool hover, bool hint)
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
        bool showRing = recommendedHere || ((hover || open) && state != PlaceState.Locked);
        if (view.ring.gameObject.activeSelf != showRing) view.ring.gameObject.SetActive(showRing);

        // Guia visual: el lugar que toca jugar respira con un halo dorado suave (sin movimiento ni particulas).
        // Lo que todavia no se ha descubierto no dibuja marcador ni candado. El nombre del lugar se mantiene visible, pero
        // sin interaccion (ni hover ni clic) hasta que el lugar este disponible.
        bool revealed = state != PlaceState.Locked;
        bool visible = revealed || hint;
        if (view.rect.gameObject.activeSelf != visible) view.rect.gameObject.SetActive(visible);

        // Con solo la pista visible: sin medallon ni clic; unicamente el anillo tenue que respira.
        bool showHint = hint && !revealed && view.hint != null;
        if (view.badge.gameObject.activeSelf != revealed) view.badge.gameObject.SetActive(revealed);
        if (view.button != null && view.button.targetGraphic != null && view.button.targetGraphic.raycastTarget != revealed)
            view.button.targetGraphic.raycastTarget = revealed;

        if (view.hint != null)
        {
            if (view.hint.gameObject.activeSelf != showHint) view.hint.gameObject.SetActive(showHint);

            if (showHint)
            {
                float breathe = Application.isPlaying ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.6f) : 0.5f;
                float s = 1f + 0.12f * breathe;
                view.hint.rectTransform.localScale = new Vector3(s, s, 1f);
                view.hint.color = new Color(MapGold.r, MapGold.g, MapGold.b, 0.38f + 0.37f * breathe);
            }
        }

        if (view.locationTag != null)
        {
            // La placa siempre recibe el cursor (para poder leer el nombre con claridad), pero solo abre el lugar,
            // crece y marca el hover del lugar cuando este esta disponible.
            Image tagBackground = view.locationTag.GetComponent<Image>();
            if (!tagBackground.raycastTarget) tagBackground.raycastTarget = true;

            Button tagButton = view.locationTag.GetComponent<Button>();
            if (tagButton != null && tagButton.interactable != revealed) tagButton.interactable = revealed;

            HoverScale tagGrow = view.locationTag.GetComponent<HoverScale>();
            if (tagGrow != null && tagGrow.enabled != revealed) tagGrow.enabled = revealed;

            MapHoverTarget tagHoverTarget = view.locationTag.GetComponent<MapHoverTarget>();
            if (tagHoverTarget != null && tagHoverTarget.enabled != revealed) tagHoverTarget.enabled = revealed;
        }

        float glowAlpha = 0f;
        float pulse = Application.isPlaying ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f) : 1f;

        if (recommendedHere)
        {
            glowAlpha = 0.50f + 0.25f * pulse;
        }

        // Anillo: en el lugar recomendado se expande y se desvanece como una onda lenta (2,4 s); al pasar el cursor
        // por cualquier lugar disponible o completado queda fijo y mas marcado.
        if (showRing)
        {
            float phase = Application.isPlaying ? Mathf.Repeat(Time.unscaledTime / 2.4f, 1f) : 0f;
            float scale = 1f;
            float alpha = 0.85f;

            if (recommendedHere && !(hover || open))
            {
                scale = 1f + 0.45f * phase;
                alpha = 0.85f * (1f - phase) * (1f - phase) + 0.1f;
            }

            view.ring.rectTransform.localScale = new Vector3(scale, scale, 1f);
            Color ringColor = new Color(1f, 0.86f, 0.40f, alpha);
            if (view.ring.color != ringColor) view.ring.color = ringColor;
        }

        if ((hover || open) && state != PlaceState.Locked && !recommendedHere)
            glowAlpha = 0.35f;

        Color glowColor = new Color(1f, 0.80f, 0.28f, glowAlpha);
        if (view.glow.color != glowColor) view.glow.color = glowColor;

        // Medallon y etiqueta de su lugar crecen juntos (la posicion no cambia).
        HoverScale badgeScale = view.badge.GetComponent<HoverScale>();
        if (badgeScale != null) badgeScale.ExternalHover = hover || open;

        if (view.locationTag != null)
        {
            HoverScale tagScale = view.locationTag.GetComponent<HoverScale>();
            if (tagScale != null) tagScale.ExternalHover = hover || open;

            // Al pasar el cursor el nombre se ilumina: texto dorado y fondo un punto mas claro.
            bool lit = (hover || open || recommendedHere) && revealed;

            // Jerarquia: solo el lugar activo y los disponibles tienen la placa completa; los bloqueados se atenuan.
            // Al pasar el cursor, MapTagHoverFade la muestra completa.
            MapTagHoverFade tagFade = view.locationTag.GetComponent<MapTagHoverFade>();
            float tagAlpha = revealed ? 1f : 0.72f;
            if (tagFade != null) tagFade.restAlpha = tagAlpha;
            Image tagBackground = view.locationTag.GetComponent<Image>();
            Color tagFill = lit ? new Color(0.098f, 0.129f, 0.188f, 0.97f) : MapNavyCard;
            if (tagBackground.color != tagFill) tagBackground.color = tagFill;

            TMP_Text tagText = view.locationTag.GetComponentInChildren<TMP_Text>();
            Color tagTextColor = lit ? MapGold : MapIvory;
            if (tagText != null && tagText.color != tagTextColor) tagText.color = tagTextColor;
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
