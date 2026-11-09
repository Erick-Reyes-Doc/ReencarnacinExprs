using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Interfaz fija del mapa (Paso 89), segun el boceto de Reino Aethel:
//   arriba izquierda  menu + estandarte con el nombre del reino
//   derecha           panel "Heroes" con la vida real del equipo
//   abajo izquierda   Coronas
//   abajo centro      progreso por region
//   abajo derecha     Inventario y Tienda (solo icono)
// Solo presenta: la logica sigue en GameSession, MapProgression y MapShopController.
// El HUD se guarda en la escena (Map.unity); BuildMapHud() lo genera y WorldMapController
// lo conecta con los campos shopButton / inventoryButton / menuButton de siempre.
public partial class WorldMapController
{
    [System.Serializable]
    public class HudHero
    {
        [Tooltip("Nombre del GameObject del personaje en el combate (clave de GameSession.Party).")]
        public string unitId;
        public string displayName;
        [Tooltip("Vida maxima de nivel 1: solo se usa mientras no existe foto del equipo.")]
        public UnitData data;
        [HideInInspector] public TMP_Text nameText;
        [HideInInspector] public RectTransform hpFill;
        [HideInInspector] public TMP_Text levelText;
        [HideInInspector] public TMP_Text hpText;   // cantidad de vida ("35 / 35")
        [HideInInspector] public HeroCardView view;
        [HideInInspector] public Image portrait;
        [HideInInspector] public Button button;
    }

    [Header("HUD del mapa (Paso 89)")]
    [SerializeField] private TMP_FontAsset hudFont;
    [SerializeField] private Sprite hudFrame;
    [SerializeField] private Sprite hudFrameHover;
    [SerializeField] private Sprite hudMenuIcon;
    [SerializeField] private Sprite hudMenuIconHover;
    [SerializeField] private Sprite hudBanner;
    [SerializeField] private Sprite hudCircle;   // circulo del indicador de nivel de las tarjetas
    [SerializeField] private Sprite hudCoinsIcon;
    [SerializeField] private Sprite hudInventoryIcon;
    [SerializeField] private Sprite hudShopIcon;
    [Header("Rediseno del mapa (Assets/Sprites/MenuUi)")]
    [SerializeField] private Sprite hudCoinsFrame;    // MarcoMonedas
    [SerializeField] private Sprite hudBottomFrame;   // MarcoInferior
    [SerializeField] private Sprite hudSeparator;     // SeparadorDorado
    [SerializeField] private List<HudHero> hudHeroes = new List<HudHero>();
    [SerializeField] private TMP_Text hudCoinsText;
    [SerializeField] private TMP_Text hudMenuHint;
    [SerializeField] private TMP_Text[] hudZoneTexts = new TMP_Text[3];

    // Paleta del menu principal.
    private static readonly Color HudDark = new Color32(0x10, 0x15, 0x1E, 0xFF);
    private static readonly Color HudPanel = new Color32(0x17, 0x1F, 0x2B, 0xFF);
    private static readonly Color HudGold = new Color32(0xB7, 0x95, 0x5B, 0xFF);
    private static readonly Color HudGoldBright = new Color32(0xD4, 0xAF, 0x6A, 0xFF);
    private static readonly Color HudIvory = new Color32(0xE8, 0xDC, 0xC6, 0xFF);
    private static readonly Color HudHealth = new Color32(0xC2, 0x3B, 0x3B, 0xFF);

    private static readonly string[] HudZones =
    {
        MapProgression.Meadow, MapProgression.Forest, MapProgression.Corruption
    };

    // ------------------------------------------------------------
    // Presentacion (se llama desde Refresh)
    // ------------------------------------------------------------

    private void RefreshHud()
    {
        if (hudCoinsText != null)
            SetTmp(hudCoinsText, GameSession.Coins.ToString("N0", CultureInfo.InvariantCulture));

        for (int i = 0; i < HudZones.Length && i < hudZoneTexts.Length; i++)
        {
            MapProgression.ZoneProgress(HudZones[i], out int done, out int total);
            SetTmp(hudZoneTexts[i], ZoneName(HudZones[i]) + " " + done + "/" + total);
        }

        // La tarjeta seleccionada es la de la ficha abierta; al cerrarla conserva la ultima elegida.
        if (characters != null && characters.IsOpen && !string.IsNullOrEmpty(characters.CurrentHeroId))
            selectedHeroId = characters.CurrentHeroId;

        foreach (HudHero hero in hudHeroes)
        {
            if (hero.hpFill == null)
                continue;

            int health;
            int max;
            int level = 1;
            HeroProfile profile = characters != null ? characters.Profile(hero.unitId) : null;

            if (profile != null && profile.unit != null)
            {
                // Misma formula que el combate: base + nivel + reliquias equipadas.
                HeroStats stats = CharacterStats.Current(profile);
                health = stats.health;
                max = stats.maxHealth;
                level = stats.level;
            }
            else if (GameSession.TryGetMember(hero.unitId, out GameSession.PartyMemberState state))
            {
                health = state.health;
                max = state.maxHealth;
                level = state.level;
            }
            else
            {
                // Sin combates todavia: vida completa de nivel 1.
                max = hero.data != null ? hero.data.maxHealth : 1;
                health = max;
            }

            SetTmp(hero.levelText, level.ToString(CultureInfo.InvariantCulture));
            SetTmp(hero.hpText, health.ToString(CultureInfo.InvariantCulture) + " / " + max.ToString(CultureInfo.InvariantCulture));

            if (hero.view != null)
            {
                hero.view.SetData(hero.displayName, level, health, max);
                hero.view.SetSelected(hero.unitId == selectedHeroId);
            }

            if (hero.portrait != null && profile != null && hero.portrait.sprite == null)
            {
                hero.portrait.sprite = characters.PortraitOf(profile);
                hero.portrait.color = profile.IconTint;
                hero.portrait.enabled = hero.portrait.sprite != null;
            }

            float ratio = max > 0 ? Mathf.Clamp01((float)health / max) : 0f;
            Vector2 anchor = hero.hpFill.anchorMax;

            if (!Mathf.Approximately(anchor.x, ratio))
                hero.hpFill.anchorMax = new Vector2(ratio, anchor.y);

            Color32 textColor = health > 0 ? HudIvory : new Color32(0x55, 0x59, 0x65, 0xFF);

            if (hero.nameText != null)
                hero.nameText.color = textColor;

            if (hero.hpText != null)
                hero.hpText.color = health > 0 ? HudGoldBright : textColor;
        }

        // El aviso es el panel que contiene el texto.
        if (hudMenuHint != null)
        {
            GameObject hint = hudMenuHint.transform.parent.gameObject;

            if (hint.activeSelf != confirmMenu)
                hint.SetActive(confirmMenu);
        }
    }

    private static void SetTmp(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

    // ------------------------------------------------------------
    // Construccion (editor y respaldo en ejecucion)
    // ------------------------------------------------------------

#if UNITY_EDITOR
    [ContextMenu("Reconstruir HUD del mapa")]
    public void RebuildHudInEditor()
    {
        if (canvas == null)
            return;

        int index = canvas.transform.childCount;
        Transform old = canvas.transform.Find("BarraSuperior");

        if (old != null)
        {
            index = old.GetSiblingIndex();
            DestroyImmediate(old.gameObject);
        }

        Transform previous = canvas.transform.Find("HudMapa");

        if (previous != null)
        {
            index = previous.GetSiblingIndex();
            DestroyImmediate(previous.gameObject);
        }

        RectTransform hud = BuildMapHud(canvas.transform);
        hud.SetSiblingIndex(index);

        // Textos con los datos reales de la sesion actual y posiciones de los Layout Groups ya calculadas.
        RefreshHud();
        LayoutRebuilder.ForceRebuildLayoutImmediate(hud);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private RectTransform BuildMapHud(Transform parent)
    {
        RectTransform root = HudRect("HudMapa", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        root.offsetMin = root.offsetMax = Vector2.zero;

        BuildHudTopLeft(root);
        BuildHudBottomBar(root);
        return root;
    }

    private void BuildHudTopLeft(RectTransform root)
    {
        Vector2 topLeft = new Vector2(0f, 1f);

        // Titulo: el sprite TituloReino ya trae el rotulo "Reino Aethel". Su parte visible mide ~633 x 82 y queda
        // centrada a y ~93 del borde superior (el rectangulo incluye un margen transparente de ~5 % por lado). Su punta
        // izquierda queda por debajo del escudo, como el estandarte original.
        RectTransform banner = HudRect("Estandarte", root, topLeft, topLeft, topLeft);
        banner.anchoredPosition = new Vector2(99f, 22f);
        banner.sizeDelta = new Vector2(703f, 234f);
        Image bannerImage = HudImage(banner, hudBanner, Color.white, false, 1f, false);
        bannerImage.preserveAspect = true;

        // Escudo del leon: es el boton de menu (no hay otro). Va encima del titulo, en el extremo izquierdo, solapando
        // su punta. Su parte visible mide ~116 x 152 y su centro queda a y ~93, la misma linea que el titulo; el cursor
        // solo reacciona sobre el escudo (raycastPadding recorta el margen transparente).
        RectTransform menu = HudRect("BotonMenu", root, topLeft, topLeft, topLeft);
        menu.anchoredPosition = new Vector2(8f, -10f);
        menu.sizeDelta = new Vector2(191f, 179f);
        Image menuImage = HudImage(menu, hudMenuIcon, Color.white, false, 1f, true);
        menuImage.preserveAspect = true;
        menuImage.raycastPadding = new Vector4(38f, 21f, 38f, 7f);
        menuButton = HudButton(menu, menuImage, hudMenuIconHover);

        // Hover discreto (crece un 5 %) y pulsacion (se oscurece un poco).
        menuButton.transition = Selectable.Transition.ColorTint;
        ColorBlock tint = menuButton.colors;
        tint.normalColor = tint.highlightedColor = tint.selectedColor = Color.white;
        tint.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        tint.fadeDuration = 0.08f;
        menuButton.colors = tint;
        menu.GetComponent<HoverScale>().HoverFactor = 1.05f;

        // Aviso de confirmacion del boton de menu (la accion pierde todo el progreso).
        RectTransform hint = HudRect("AvisoMenu", root, topLeft, topLeft, topLeft);
        hint.anchoredPosition = new Vector2(24f, -185f);   // bajo el escudo
        hint.sizeDelta = new Vector2(420f, 44f);
        HudImage(hint, null, new Color(HudDark.r, HudDark.g, HudDark.b, 0.92f), false, 1f, false);
        RectTransform hintText = HudRect("Texto", hint, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        hintText.offsetMin = new Vector2(10f, 0f);
        hintText.offsetMax = new Vector2(-10f, 0f);
        hudMenuHint = HudText(hintText, "¿Perder todo? Pulsa otra vez", 26f, HudGoldBright, TextAlignmentOptions.Center);
        hint.gameObject.SetActive(false);
    }

    // Medidas de los marcos. Los sprites de marco (menuNormal / menuHover) llevan un margen
    // transparente que crece al reducir el multiplicador de pixeles: con el multiplicador M
    // los margenes visibles son izq 65/M, der 54/M, arriba 185/M, abajo 198/M (en unidades).
    // Todas las piezas de la barra inferior comparten multiplicador y alto, asi su parte
    // visible mide lo mismo y comparten linea base.
    private const float BarMultiplier = 8f;
    private const float BarHeight = 128f;        // alto del rectangulo -> ~80 visibles
    private const float BarSpacing = -1f;        // el margen transparente de los marcos (~15) ya deja ~14 visibles entre ellos
    private const float BarMargin = 16f;         // + ~8 de margen del sprite = ~24 al borde
    private const float CoinsWidth = 315f;
    private const float ProgressWidth = 285f;
    // Medida visual comun de los tres botones (menu, inventario, tienda): 87 x 80, la proporcion
    // del sprite del boton de menu. Los marcos de Inventario y Tienda anaden ~15 de margen
    // transparente al rectangulo.
    private const float ButtonVisualWidth = 87f;
    private const float ButtonVisualHeight = 80f;
    private const float ShortcutSize = 70f;      // icono sin marco (cofre y tienda)

    // Barra inferior (sprite MarcoInferior, 3:1 con margen transparente): ocupa casi todo el ancho de 1920 y su parte
    // visible mide ~93 de alto, a 61 del borde inferior. Todas las posiciones se miden desde el centro de la barra.
    private const float BottomBarWidth = 1900f;
    private const float BottomBarVisibleHeight = 93f;
    private const float BottomBarBottom = 61f;
    private const float SeparatorClipHeight = 82f;  // altura interior de la barra, dentro de sus bordes dorados
    private const float HeroSegmentStart = 55f;     // x del primer segmento (lienzo 1920)
    private const float HeroSegmentWidth = 387f;    // cuatro segmentos hasta x ~1603, donde empiezan los atajos
    private const float ShortcutsCenterX = 1733f;

    // Esquina superior derecha: contador de Coronas. Barra inferior: personajes, separadores y atajos.
    private void BuildHudBottomBar(RectTransform root)
    {
        Vector2 mid = new Vector2(0.5f, 0.5f);

        // Coronas (sprite MarcoMonedas, 9-slice horizontal): icono a la izquierda y cifra a la derecha. La cifra es la de
        // GameSession.Coins y se actualiza en RefreshHud. Mide lo mismo de alto que el titulo (~82 visibles), esta en la
        // misma linea (centro a y ~93) y su margen visible al borde derecho (46) es el del escudo al izquierdo. Con el
        // multiplicador 683 / 148 el sprite completo mide 148 de alto y solo se estira a lo ancho (~300 visibles).
        Vector2 topRight = new Vector2(1f, 1f);
        RectTransform coins = HudRect("GrupoCoronas", root, topRight, topRight, topRight);
        coins.anchoredPosition = new Vector2(-26f, -19f);
        coins.sizeDelta = new Vector2(340f, 148f);
        HudImage(coins, hudCoinsFrame, Color.white, true, 683f / 148f, false);

        RectTransform icon = HudRect("Icono", coins, mid, mid, mid);
        icon.anchoredPosition = new Vector2(-62f, 0f);
        icon.sizeDelta = new Vector2(66f, 54f);
        Image iconImage = HudImage(icon, hudCoinsIcon, Color.white, false, 1f, false);
        iconImage.preserveAspect = true;

        RectTransform amount = HudRect("Cantidad", coins, mid, mid, mid);
        amount.anchoredPosition = new Vector2(44f, 0f);
        amount.sizeDelta = new Vector2(130f, 64f);
        hudCoinsText = HudText(amount, "0", 44f, HudGoldBright, TextAlignmentOptions.Midline);
        hudCoinsText.characterSpacing = 4f;

        // Barra inferior: el marco es solo decoracion (sin raycast) para no bloquear los clics del mapa.
        RectTransform bar = HudRect("BarraInferior", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), mid);
        bar.anchoredPosition = new Vector2(0f, BottomBarBottom);
        bar.sizeDelta = new Vector2(BottomBarWidth, BottomBarVisibleHeight);

        RectTransform frame = HudRect("Marco", bar, mid, mid, mid);
        frame.anchoredPosition = new Vector2(0f, -6f);
        frame.sizeDelta = new Vector2(BottomBarWidth, BottomBarWidth / 3f);
        Image frameImage = HudImage(frame, hudBottomFrame, Color.white, false, 1f, false);
        frameImage.preserveAspect = true;

        // Centro: cuatro segmentos iguales, uno por personaje (retrato, nivel, nombre y vida). El progreso por region
        // sigue en MapProgression.ZoneProgress; solo se retiro de esta barra.
        RectTransform center = HudRect("GrupoPersonajes", bar, mid, mid, mid);
        center.anchoredPosition = Vector2.zero;
        center.sizeDelta = new Vector2(BottomBarWidth, BottomBarVisibleHeight);
        BuildHeroCards(center);

        // Separadores dorados entre segmentos y antes de los atajos.
        // El sprite es una aguja muy fina y larga: se recorta (RectMask2D) a la altura interior de la barra para que no
        // se salga del contenedor.
        RectTransform separators = HudRect("Separadores", bar, mid, mid, mid);
        separators.anchoredPosition = Vector2.zero;
        separators.sizeDelta = new Vector2(BottomBarWidth, SeparatorClipHeight);
        separators.gameObject.AddComponent<RectMask2D>();

        for (int i = 1; i <= 4; i++)
        {
            RectTransform separator = HudRect("Separador_" + i, separators, mid, mid, mid);
            separator.anchoredPosition = new Vector2(HeroSegmentStart + HeroSegmentWidth * i - 960f, 0f);
            separator.sizeDelta = new Vector2(70f, 210f);   // aguja muy fina: solo el centro (rombo y tramo grueso) queda dentro del recorte
            Image separatorImage = HudImage(separator, hudSeparator, Color.white, false, 1f, false);
            separatorImage.preserveAspect = true;
        }

        // Derecha: Inventario (cofre) y Tienda, dentro del marco y sin tocar los segmentos de personajes.
        RectTransform right = HudGroup("GrupoAtajos", bar, mid, mid, mid);
        right.anchoredPosition = new Vector2(ShortcutsCenterX - 960f, 0f);
        right.GetComponent<HorizontalLayoutGroup>().spacing = 22f;
        inventoryButton = HudShortcut(right, "BotonInventario", hudInventoryIcon);
        shopButton = HudShortcut(right, "BotonTienda", hudShopIcon);
    }

    // Tarjetas de la barra inferior (diseno limpio): retrato, nombre, nivel en un circulo pequeno y una barra de vida
    // roja y delgada sin numeros (los valores salen en un tooltip al pasar el cursor). Al pasar el cursor la
    // tarjeta se agranda un poco (HoverScale) y HeroCardView muestra el tooltip; el clic abre la ficha del personaje.
    private static readonly Color HudHealthDark = new Color32(0xA8, 0x32, 0x32, 0xFF);
    private static readonly Color HudHealthTrack = new Color32(0x0B, 0x0F, 0x16, 0xE6);

    // Cada personaje ocupa un segmento de la barra (sin marco propio: el marco es el de la barra). Las posiciones son
    // relativas al centro de la tarjeta: retrato, circulo de nivel y nombre en una linea y la barra de vida debajo.
    private void BuildHeroCards(RectTransform group)
    {
        hudHeroes.RemoveAll(h => h == null);
        Vector2 mid = new Vector2(0.5f, 0.5f);

        for (int index = 0; index < hudHeroes.Count; index++)
        {
            HudHero hero = hudHeroes[index];
            float segmentStart = HeroSegmentStart + HeroSegmentWidth * index;
            RectTransform card = HudRect("Tarjeta_" + hero.displayName, group, mid, mid, mid);
            card.anchoredPosition = new Vector2(segmentStart + HeroSegmentWidth * 0.5f - 960f, 0f);
            card.sizeDelta = new Vector2(HeroSegmentWidth, BottomBarVisibleHeight);
            Image cardImage = HudImage(card, null, new Color(1f, 1f, 1f, 0f), false, 1f, true);

            // Retrato sin marco ni fondo.
            RectTransform face = HudRect("Retrato", card, mid, mid, mid);
            face.anchoredPosition = new Vector2(-124.5f, 0f);
            face.sizeDelta = new Vector2(68f, 68f);
            hero.portrait = HudImage(face, null, Color.white, false, 1f, false);
            hero.portrait.preserveAspect = true;
            hero.portrait.enabled = false;

            // Sin nivel ni recuadro emergente: retrato a la izquierda y, a su derecha, una linea con el nombre (izquierda) y
            // la cantidad de vida (derecha, mas pequena y dorada) sobre la barra, con aire entre ambas.
            RectTransform name = HudRect("Nombre", card, mid, mid, new Vector2(0f, 0.5f));
            name.anchoredPosition = new Vector2(-86.5f, 15f);
            name.sizeDelta = new Vector2(150f, 30f);
            hero.nameText = HudText(name, hero.displayName, 26f, HudIvory, TextAlignmentOptions.MidlineLeft);

            RectTransform life = HudRect("Vida", card, mid, mid, new Vector2(1f, 0.5f));
            life.anchoredPosition = new Vector2(148.5f, 14f);
            life.sizeDelta = new Vector2(100f, 26f);
            hero.hpText = HudText(life, "", 22f, HudGoldBright, TextAlignmentOptions.MidlineRight);

            // Barra de vida delgada: fondo oscuro y relleno rojo oscuro, sin borde ni cifras.
            RectTransform track = HudRect("BarraVida", card, mid, mid, new Vector2(0f, 0.5f));
            track.anchoredPosition = new Vector2(-86.5f, -16f);
            track.sizeDelta = new Vector2(235f, 8f);
            HudImage(track, null, HudHealthTrack, false, 1f, false);

            RectTransform fill = HudRect("Relleno", track, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f));
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            HudImage(fill, null, HudHealthDark, false, 1f, false);
            hero.hpFill = fill;

            hero.button = HudButton(card, cardImage, null);
            hero.button.transition = Selectable.Transition.None;   // solo se agranda (HoverScale)
            HoverScale hover = card.GetComponent<HoverScale>();

            if (hover != null)
                hover.HoverFactor = 1.02f;

            // Sin HeroCardView: no hay recuadro emergente con nombre, nivel y vida al pasar el cursor.
            hero.view = null;
        }
    }

    private static RectTransform HudGroup(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        RectTransform group = HudRect(name, parent, anchorMin, anchorMax, pivot);
        group.anchoredPosition = Vector2.zero;
        HorizontalLayoutGroup row = group.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = BarSpacing;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        ContentSizeFitter fitter = group.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return group;
    }

    private RectTransform HudBarFrame(string name, RectTransform parent, float width, bool raycast, float height = BarHeight)
    {
        RectTransform frame = HudRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        HudImage(frame, hudFrame, Color.white, true, BarMultiplier, raycast);
        LayoutElement size = frame.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = width;
        size.preferredHeight = height;
        return frame;
    }

    private Button HudShortcut(RectTransform group, string name, Sprite icon)
    {
        // Solo el icono, sin marco: la raiz es una zona de clic transparente y el icono (hijo "Icono", que tambien busca
        // la tienda) recibe el hover (crece) y la pulsacion (se oscurece un poco).
        Vector2 mid = new Vector2(0.5f, 0.5f);
        RectTransform root = HudRect(name, group, mid, mid, mid);
        HudImage(root, null, new Color(1f, 1f, 1f, 0f), false, 1f, true);
        LayoutElement size = root.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = ShortcutSize;
        size.preferredHeight = ShortcutSize;

        RectTransform iconRect = HudRect("Icono", root, mid, mid, mid);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(ShortcutSize, ShortcutSize);
        Image iconImage = HudImage(iconRect, icon, Color.white, false, 1f, false);
        iconImage.preserveAspect = true;

        Button button = HudButton(root, iconImage, null);
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock tint = button.colors;
        tint.normalColor = tint.highlightedColor = tint.selectedColor = Color.white;
        tint.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        tint.fadeDuration = 0.08f;
        button.colors = tint;
        root.GetComponent<HoverScale>().HoverFactor = 1.1f;
        return button;
    }

    // ---------------- Utilidades del HUD ----------------

    private static RectTransform HudRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
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

    private static Image HudImage(RectTransform rect, Sprite sprite, Color color, bool sliced, float pixelsPerUnitMultiplier, bool raycast)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        image.raycastTarget = raycast;
        return image;
    }

    private TMP_Text HudText(RectTransform rect, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();

        if (hudFont != null)
            label.font = hudFont;

        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    // Boton con cambio de sprite (normal -> hover/pulsado), igual que los del menu principal.
    private static Button HudButton(RectTransform rect, Image target, Sprite hover)
    {
        Button button = rect.gameObject.AddComponent<Button>();
        rect.gameObject.AddComponent<HoverScale>();   // se agranda un poco al pasar el cursor
        button.targetGraphic = target;
        button.transition = Selectable.Transition.SpriteSwap;
        SpriteState state = new SpriteState
        {
            highlightedSprite = hover,
            selectedSprite = hover,
            pressedSprite = hover,
            disabledSprite = target.sprite
        };
        button.spriteState = state;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        return button;
    }
}
