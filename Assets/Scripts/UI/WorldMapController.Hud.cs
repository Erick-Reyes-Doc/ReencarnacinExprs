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

            if (hero.view != null)
            {
                hero.view.SetData(hero.displayName, level, health, max);
                hero.view.SetSelected(hero.unitId == selectedHeroId);
            }

            if (hero.portrait != null && profile != null && hero.portrait.sprite == null)
            {
                hero.portrait.sprite = characters.PortraitOf(profile);
                hero.portrait.color = profile.tint;
                hero.portrait.enabled = hero.portrait.sprite != null;
            }

            float ratio = max > 0 ? Mathf.Clamp01((float)health / max) : 0f;
            Vector2 anchor = hero.hpFill.anchorMax;

            if (!Mathf.Approximately(anchor.x, ratio))
                hero.hpFill.anchorMax = new Vector2(ratio, anchor.y);

            if (hero.nameText != null)
                hero.nameText.color = health > 0 ? HudIvory : new Color32(0x55, 0x59, 0x65, 0xFF);
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
        RectTransform menu = HudRect("BotonMenu", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        // Mismo tamano visual que Inventario y Tienda (87 x 80) y mismo margen al borde (24).
        menu.anchoredPosition = new Vector2(24f, -50f);   // centrado en la franja azul del estandarte (y ~90)
        menu.sizeDelta = new Vector2(ButtonVisualWidth, ButtonVisualHeight);
        Image menuImage = HudImage(menu, hudMenuIcon, Color.white, false, 1f, true);
        menuImage.preserveAspect = true;
        menuButton = HudButton(menu, menuImage, hudMenuIconHover);

        RectTransform banner = HudRect("Estandarte", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        banner.anchoredPosition = new Vector2(136f, -8f);
        banner.sizeDelta = new Vector2(800f, 202f);
        Image bannerImage = HudImage(banner, hudBanner, Color.white, false, 1f, false);
        bannerImage.preserveAspect = true;

        // El rotulo ocupa la parte azul del estandarte (la bandera con el leon queda a la izquierda).
        RectTransform title = HudRect("NombreReino", banner, new Vector2(0.23f, 0.34f), new Vector2(0.90f, 0.84f), new Vector2(0.5f, 0.5f));
        title.offsetMin = title.offsetMax = Vector2.zero;
        HudText(title, "Reino Aethel", 54f, HudIvory, TextAlignmentOptions.Center);

        // Aviso de confirmacion del boton de menu (la accion pierde todo el progreso).
        RectTransform hint = HudRect("AvisoMenu", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        hint.anchoredPosition = new Vector2(24f, -222f);   // bajo la bandera del estandarte
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
    private const float ShortcutWidth = 102f;    // 87 visibles + margen del sprite

    // Barra inferior: un contenedor a todo el ancho con tres grupos en la misma franja
    // (Coronas a la izquierda, progreso centrado, atajos a la derecha). Cada grupo es un
    // HorizontalLayoutGroup que se ajusta a su contenido.
    private void BuildHudBottomBar(RectTransform root)
    {
        RectTransform bar = HudRect("BarraInferior", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        bar.offsetMin = new Vector2(BarMargin, 0f);
        bar.offsetMax = new Vector2(-BarMargin - 1f, BarHeight);

        // Coronas: esquina superior derecha (ya no van en la barra inferior).
        RectTransform left = HudGroup("GrupoCoronas", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        left.anchoredPosition = new Vector2(-17f, -26f);   // margen visible ~24 al borde, como el boton de menu
        RectTransform coins = HudBarFrame("PanelCoronas", left, CoinsWidth, false);
        HorizontalLayoutGroup coinsRow = coins.gameObject.AddComponent<HorizontalLayoutGroup>();
        coinsRow.padding = new RectOffset(36, 22, 0, 0);
        coinsRow.spacing = 8f;
        coinsRow.childAlignment = TextAnchor.MiddleLeft;
        coinsRow.childControlWidth = coinsRow.childControlHeight = true;
        coinsRow.childForceExpandWidth = coinsRow.childForceExpandHeight = false;

        RectTransform icon = HudRect("Icono", coins, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        Image iconImage = HudImage(icon, hudCoinsIcon, Color.white, false, 1f, false);
        iconImage.preserveAspect = true;
        LayoutElement iconSize = icon.gameObject.AddComponent<LayoutElement>();
        iconSize.preferredWidth = 66f;
        iconSize.preferredHeight = 54f;

        RectTransform amount = HudRect("Cantidad", coins, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        // Cifras centradas en el espacio que deja el icono, con algo de separacion entre ellas.
        hudCoinsText = HudText(amount, "0", 44f, HudGoldBright, TextAlignmentOptions.Midline);
        hudCoinsText.characterSpacing = 6f;
        LayoutElement amountSize = amount.gameObject.AddComponent<LayoutElement>();
        amountSize.flexibleWidth = 1f;
        amountSize.preferredHeight = 60f;

        // Centro: una tarjeta por personaje (retrato, nombre, nivel y vida). El progreso por region sigue en
        // MapProgression.ZoneProgress; solo se retiro de esta barra.
        RectTransform center = HudGroup("GrupoPersonajes", bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        BuildHeroCards(center);

        // Derecha: Inventario y Tienda (cuadrados del mismo tamano, solo icono).
        RectTransform right = HudGroup("GrupoAtajos", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        inventoryButton = HudShortcut(right, "BotonInventario", hudInventoryIcon);
        shopButton = HudShortcut(right, "BotonTienda", hudShopIcon);
    }

    // Tarjetas de la barra inferior (diseno limpio): retrato, nombre, nivel en un circulo pequeno y una barra de vida
    // roja y delgada sin numeros (los valores salen en un tooltip al pasar el cursor). Al pasar el cursor la
    // tarjeta se agranda un poco (HoverScale) y HeroCardView muestra el tooltip; el clic abre la ficha del personaje.
    private const float HeroCardWidth = 312f;
    private static readonly Color HudHealthDark = new Color32(0xA8, 0x32, 0x32, 0xFF);
    private static readonly Color HudHealthTrack = new Color32(0x0B, 0x0F, 0x16, 0xE6);

    private void BuildHeroCards(RectTransform group)
    {
        hudHeroes.RemoveAll(h => h == null);

        foreach (HudHero hero in hudHeroes)
        {
            RectTransform card = HudBarFrame("Tarjeta_" + hero.displayName, group, HeroCardWidth, true);
            Image cardImage = card.GetComponent<Image>();

            // Retrato sin marco ni fondo.
            RectTransform face = HudRect("Retrato", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            face.anchoredPosition = new Vector2(-118f, -2f);
            face.sizeDelta = new Vector2(64f, 64f);
            hero.portrait = HudImage(face, null, Color.white, false, 1f, false);
            hero.portrait.preserveAspect = true;
            hero.portrait.enabled = false;

            // Nivel: numero pequeno dentro de un circulo, en la esquina del retrato.
            RectTransform ring = HudRect("Nivel", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            ring.anchoredPosition = new Vector2(-92f, 22f);
            ring.sizeDelta = new Vector2(28f, 28f);
            HudImage(ring, hudCircle, HudGold, false, 1f, false);
            RectTransform disc = HudRect("Fondo", ring, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            disc.offsetMin = new Vector2(2f, 2f);
            disc.offsetMax = new Vector2(-2f, -2f);
            HudImage(disc, hudCircle, HudDark, false, 1f, false);
            RectTransform number = HudRect("Numero", ring, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            number.offsetMin = number.offsetMax = Vector2.zero;
            hero.levelText = HudText(number, "1", 17f, HudGoldBright, TextAlignmentOptions.Midline);

            RectTransform name = HudRect("Nombre", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f));
            name.anchoredPosition = new Vector2(-74f, 10f);
            name.sizeDelta = new Vector2(196f, 30f);
            hero.nameText = HudText(name, hero.displayName, 26f, HudIvory, TextAlignmentOptions.MidlineLeft);

            // Barra de vida delgada: fondo oscuro y relleno rojo oscuro, sin borde ni cifras.
            RectTransform track = HudRect("BarraVida", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f));
            track.anchoredPosition = new Vector2(-74f, -14f);
            track.sizeDelta = new Vector2(190f, 7f);
            HudImage(track, null, HudHealthTrack, false, 1f, false);

            RectTransform fill = HudRect("Relleno", track, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f));
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            HudImage(fill, null, HudHealthDark, false, 1f, false);
            hero.hpFill = fill;

            hero.button = HudButton(card, cardImage, hudFrameHover);
            hero.button.transition = Selectable.Transition.None;   // solo se agranda (HoverScale)
            HoverScale hover = card.GetComponent<HoverScale>();

            if (hover != null)
                hover.HoverFactor = 1.02f;

            hero.view = card.gameObject.AddComponent<HeroCardView>();
            hero.view.Configure(null, hudFont);
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

    private RectTransform HudBarFrame(string name, RectTransform parent, float width, bool raycast)
    {
        RectTransform frame = HudRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        HudImage(frame, hudFrame, Color.white, true, BarMultiplier, raycast);
        LayoutElement size = frame.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = width;
        size.preferredHeight = BarHeight;
        return frame;
    }

    private Button HudShortcut(RectTransform group, string name, Sprite icon)
    {
        RectTransform frame = HudBarFrame(name, group, ShortcutWidth, true);

        RectTransform iconRect = HudRect("Icono", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(58f, 58f);
        Image iconImage = HudImage(iconRect, icon, Color.white, false, 1f, false);
        iconImage.preserveAspect = true;

        return HudButton(frame, frame.GetComponent<Image>(), hudFrameHover);
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
