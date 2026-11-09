using System;
using System.Collections.Generic;
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

    [Header("Rediseno de etiquetas (Assets/Sprites/MenuUi)")]
    [Tooltip("MarcoUbicaciones: marco horizontal azul con bordes dorados (9-slice horizontal).")]
    [SerializeField] private Sprite hudLabelFrame;
    [Tooltip("Icono morado del jefe: sustituye al de combate en el Trono Sellado cuando esta disponible.")]
    [SerializeField] private Sprite bossIcon;
    [SerializeField] private List<LocationIcon> locationIcons = new List<LocationIcon>();

    // Icono propio de cada ubicacion. "id" es la clave de la etiqueta (camp, ruins, clearing, ...).
    [Serializable]
    public class LocationIcon
    {
        public string id;
        public Sprite sprite;
    }

    private Sprite LocationIconOf(string id)
    {
        foreach (LocationIcon entry in locationIcons)
            if (entry != null && entry.id == id)
                return entry.sprite;

        return null;
    }

    // Los iconos de MenuUi son insignias redondas con ~14 % de margen transparente por lado: el circulo visible mide
    // ~57 % del lienzo del sprite.
    private const float BadgeVisibleRatio = 0.574f;

    private const float MedallionSize = 54f;

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
        ("pavilion", "Altar de los Invocados", 395f, 337f, true),   // lugar solo narrativo (antes Pabellón de Caza): sin combates ni recompensas
        ("cathedral", "Catedral del Sol", 862f, 397f, true),
        ("wastes", "Nido de Wyverns", 1370f, 327f, true),   // el Nido es el lugar jugable de C1 y C2 (id interno "wastes")
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

    // Geometria de la etiqueta con MarcoUbicaciones (2048 x 683, 9-slice solo horizontal). Con el multiplicador
    // 683 / 96 el sprite completo mide 96 de alto: ~36 visibles, centrados, y los remates de las puntas (~20) no se
    // deforman al ensanchar. El icono circular se solapa con la punta izquierda; el texto va a su derecha.
    private const float LabelHeight = 96f;
    private const float LabelVisibleHeight = 36f;
    private const float LabelMultiplier = 683f / LabelHeight;
    private const float LabelMargin = 3.7f;        // margen transparente del sprite en cada punta
    private const float LabelIconCenter = 26f;     // centro del icono desde la punta izquierda visible
    private const float LabelTextStart = 56f;      // inicio del texto desde la punta izquierda visible
    private const float LabelRightPad = 26f;

    // Etiqueta de ubicacion: marco, nombre y (en las informativas) su icono. Los lugares jugables ponen el icono en
    // su medallon, que se acopla a la punta izquierda de esta etiqueta (LinkLocationTags).
    private RectTransform BuildLabelCard(string name, Transform parent, string text, float fontSize, Sprite icon)
    {
        RectTransform card = HudRect(name, parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        Image frame = HudImage(card, hudLabelFrame, Color.white, true, LabelMultiplier, true);
        float padding = (LabelHeight - LabelVisibleHeight) * 0.5f;
        frame.raycastPadding = new Vector4(LabelMargin, padding, LabelMargin, padding);   // solo el marco visible responde

        RectTransform textRect = HudRect("Texto", card, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        textRect.offsetMin = new Vector2(LabelMargin + LabelTextStart, 0f);
        textRect.offsetMax = new Vector2(-(LabelMargin + LabelRightPad), 0f);
        TMP_Text label = HudText(textRect, text, fontSize, MapIvory, TextAlignmentOptions.Center);

        float width = LabelTextStart + Mathf.Ceil(label.GetPreferredValues(text).x) + LabelRightPad + LabelMargin * 2f;
        card.sizeDelta = new Vector2(width, LabelHeight);

        // Iluminacion sutil al pasar el cursor: contorno dorado del marco (se activa en RefreshMedallion).
        Outline glow = card.gameObject.AddComponent<Outline>();
        glow.effectColor = new Color(MapGold.r, MapGold.g, MapGold.b, 0.55f);
        glow.effectDistance = new Vector2(2f, 2f);
        glow.enabled = false;

        if (icon != null)
        {
            RectTransform iconRect = HudRect("Icono", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            float iconSize = MedallionSize / BadgeVisibleRatio;
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(-width * 0.5f + LabelMargin + LabelIconCenter, 0f);
            Image iconImage = HudImage(iconRect, icon, Color.white, false, 1f, false);
            iconImage.preserveAspect = true;
        }

        return card;
    }

    private RectTransform BuildLocationLabels(RectTransform area)
    {
        RectTransform group = Group("EtiquetasUbicacion", area);

        foreach ((string key, string text, float x, float y, bool primary) tag in LocationTags)
        {
            RectTransform card;

            if (hudLabelFrame != null)
            {
                // Los lugares sin mision (ciudad, pabellon, catedral, nido) llevan su icono dentro de la etiqueta.
                Sprite icon = FindLocation(tag.key) == null ? LocationIconOf(tag.key) : null;
                card = BuildLabelCard("Etiqueta_" + tag.key, group, tag.text, 18f, icon);
            }
            else
            {
                card = InfoCard("Etiqueta_" + tag.key, group, tag.text, 17f);
                card.sizeDelta = new Vector2(card.sizeDelta.x, 30f);
            }

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
            float dockX = hudLabelFrame != null
                ? -(tagRect.sizeDelta.x * 0.5f) + LabelMargin + LabelIconCenter   // el icono se solapa con la punta izquierda
                : -(tagRect.sizeDelta.x * 0.5f + 31f);
            view.rect.anchoredPosition = tagRect.anchoredPosition + new Vector2(dockX, 0f);
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
        // sustituye a Marco y Cuerpo. Su anillo ocupa ~57 % del lienzo del sprite: se amplia para igualar el medallon.
        // En el Trono Sellado se usa el icono morado del jefe en lugar del de combate.
        bool isBoss = def.encounterIds.Contains(BossId);
        Sprite questSprite = isBoss && bossIcon != null ? bossIcon : (questIcon != null ? questIcon : IcStar);
        float questSize = size / (isBoss ? 0.60f : BadgeVisibleRatio);
        RectTransform quest = Local("Combate", badge, Vector2.zero, new Vector2(questSize, questSize), Color.white, questSprite);
        quest.GetComponent<Image>().preserveAspect = true;
        frame.gameObject.SetActive(false);   // estado inicial = disponible (en ejecucion lo fija RefreshMedallion)
        body.gameObject.SetActive(false);

        // Icono propio del lugar (MenuUi): se ve mientras el lugar esta bloqueado o completado. El Trono Sellado usa el
        // candado de su sprite mientras esta bloqueado.
        Sprite own = LocationIconOf(def.id);
        RectTransform insignia = null;
        RectTransform visto = null;

        if (own != null)
        {
            float insigniaSize = size / BadgeVisibleRatio;
            insignia = Local("Insignia", badge, Vector2.zero, new Vector2(insigniaSize, insigniaSize), Color.white, own);
            insignia.GetComponent<Image>().preserveAspect = true;

            visto = Local("Visto", badge, new Vector2(size * 0.38f, -size * 0.38f), new Vector2(size * 0.44f, size * 0.44f), MapNavy, SpCircle);
            Local("Marca", visto, Vector2.zero, new Vector2(size * 0.28f, size * 0.28f), MapGold, IcCheck);
            visto.gameObject.SetActive(false);
        }

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
            quest = quest.GetComponent<Image>(), hint = hintRing.GetComponent<Image>(),
            insignia = insignia != null ? insignia.GetComponent<Image>() : null,
            visto = visto != null ? visto.gameObject.GetComponent<Image>() : null,
            boss = isBoss
        };
    }

    // Tres estados: disponible (borde dorado + espadas), bloqueado (azul grisaceo + candado)
    // y completado (mas discreto + verificacion). Sin brillos ni animaciones.
    private void RefreshMedallion(LocationView view, PlaceState state, bool evt, bool open, bool hover, bool hint)
    {
        Color frame = MapGold, body = MapNavy, iconColor = MapIvory;
        Sprite icon = null;
        bool showQuest = false;

        // Lugares con icono propio (MenuUi): bloqueado = su icono apagado (el Trono Sellado, su candado); completado =
        // su icono con una marca; combate disponible = medallon de combate (el del jefe, morado) con los efectos
        // dorados de siempre; evento (La mascara rota) = medallon con mascara. Sin icono propio (Tierras Marchitas)
        // se conserva el medallon generico.
        bool hasOwn = view.insignia != null;
        bool showInsignia = false;
        bool showVisto = false;
        Sprite insigniaSprite = hasOwn ? LocationIconOf(view.id) : null;
        Color insigniaColor = Color.white;

        if (state == PlaceState.Completed)
        {
            frame = MapGoldDim;
            icon = IcCheck;
            iconColor = new Color(0.80f, 0.76f, 0.62f, 0.95f);

            if (hasOwn)
            {
                showInsignia = true;
                showVisto = true;
                insigniaColor = new Color(0.82f, 0.80f, 0.74f, 1f);

                if (view.boss && bossIcon != null)
                    insigniaSprite = bossIcon;
            }
        }
        else if (state == PlaceState.Locked)
        {
            frame = new Color(0.43f, 0.50f, 0.60f, 1f);
            body = new Color(0.18f, 0.22f, 0.29f, 1f);
            icon = IcLock;
            iconColor = new Color(0.72f, 0.78f, 0.88f, 1f);

            if (hasOwn)
            {
                showInsignia = true;
                insigniaColor = view.boss ? Color.white : new Color(0.62f, 0.65f, 0.72f, 1f);

                // Jefe descubierto (evento de Valdoria) pero aun bloqueado: icono morado del jefe, sin permitir el combate.
                if (view.boss && GameSession.IsBossDiscovered && !GameSession.BossRevealPending && bossIcon != null)
                    insigniaSprite = bossIcon;
            }
        }
        else if (evt)
        {
            icon = IcMask;
        }
        else
        {
            showQuest = true;
        }

        bool showGeneric = !showQuest && !showInsignia;
        view.frame.color = frame;
        view.body.color = body;

        if (view.quest.gameObject.activeSelf != showQuest) view.quest.gameObject.SetActive(showQuest);
        if (view.frame.gameObject.activeSelf != showGeneric) view.frame.gameObject.SetActive(showGeneric);
        if (view.body.gameObject.activeSelf != showGeneric) view.body.gameObject.SetActive(showGeneric);
        if (view.icon.gameObject.activeSelf != showGeneric) view.icon.gameObject.SetActive(showGeneric);

        if (showGeneric)
        {
            if (view.icon.sprite != icon) view.icon.sprite = icon;
            view.icon.color = iconColor;
        }

        if (hasOwn)
        {
            if (view.insignia.gameObject.activeSelf != showInsignia) view.insignia.gameObject.SetActive(showInsignia);
            if (view.visto.gameObject.activeSelf != showVisto) view.visto.gameObject.SetActive(showVisto);

            if (showInsignia)
            {
                if (view.insignia.sprite != insigniaSprite) view.insignia.sprite = insigniaSprite;
                if (view.insignia.color != insigniaColor) view.insignia.color = insigniaColor;
            }
        }

        bool recommendedHere = state == PlaceState.Recommended;
        bool showRing = recommendedHere || ((hover || open) && state != PlaceState.Locked);
        if (view.ring.gameObject.activeSelf != showRing) view.ring.gameObject.SetActive(showRing);

        // Guia visual: el lugar que toca jugar respira con un halo dorado suave (sin movimiento ni particulas).
        // Lo que todavia no se ha descubierto no dibuja marcador ni candado. El nombre del lugar se mantiene visible, pero
        // sin interaccion (ni hover ni clic) hasta que el lugar este disponible.
        bool revealed = state != PlaceState.Locked;
        bool visible = revealed || hint || hasOwn;
        if (view.rect.gameObject.activeSelf != visible) view.rect.gameObject.SetActive(visible);

        // Con solo la pista visible: sin medallon ni clic; unicamente el anillo tenue que respira. Los lugares con icono
        // propio muestran siempre su icono (apagado mientras estan bloqueados), pero sin clic ni hover.
        bool showHint = hint && !revealed && view.hint != null;
        bool showBadge = revealed || hasOwn;
        if (view.badge.gameObject.activeSelf != showBadge) view.badge.gameObject.SetActive(showBadge);
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

            if (hudLabelFrame != null)
            {
                // Marco de sprite: el color queda en blanco y la iluminacion es un contorno dorado sutil.
                if (tagBackground.color != Color.white) tagBackground.color = Color.white;
                Outline tagGlow = view.locationTag.GetComponent<Outline>();
                if (tagGlow != null && tagGlow.enabled != lit) tagGlow.enabled = lit;
            }
            else
            {
                Color tagFill = lit ? new Color(0.098f, 0.129f, 0.188f, 0.97f) : MapNavyCard;
                if (tagBackground.color != tagFill) tagBackground.color = tagFill;
            }

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
            font = GameFonts.Regular;

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
