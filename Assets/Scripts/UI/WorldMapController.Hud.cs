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
    }

    [Header("HUD del mapa (Paso 89)")]
    [SerializeField] private TMP_FontAsset hudFont;
    [SerializeField] private Sprite hudFrame;
    [SerializeField] private Sprite hudFrameHover;
    [SerializeField] private Sprite hudMenuIcon;
    [SerializeField] private Sprite hudMenuIconHover;
    [SerializeField] private Sprite hudBanner;
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

        foreach (HudHero hero in hudHeroes)
        {
            if (hero.hpFill == null)
                continue;

            int health;
            int max;

            if (GameSession.TryGetMember(hero.unitId, out GameSession.PartyMemberState state))
            {
                health = state.health;
                max = state.maxHealth;
            }
            else
            {
                // Sin combates todavia: vida completa de nivel 1.
                max = hero.data != null ? hero.data.maxHealth : 1;
                health = max;
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
        BuildHudHeroes(root);
        BuildHudBottomBar(root);
        return root;
    }

    private void BuildHudTopLeft(RectTransform root)
    {
        RectTransform menu = HudRect("BotonMenu", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        // Mismo tamano visual que Inventario y Tienda (87 x 80) y mismo margen al borde (24).
        menu.anchoredPosition = new Vector2(24f, -24f);
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

    private void BuildHudHeroes(RectTransform root)
    {
        // Una sola fila de cuatro tarjetas en la parte alta del lateral derecho. Visible: de
        // x 966 a 1896 y de y 24 a 104 (pantalla de 1080), asi que queda a la derecha del
        // estandarte (que termina en x ~930) y muy por encima de Trono Sellado (y 184+).
        // Los rectangulos incluyen el margen transparente del marco (izq 6.5, der 5.4, arriba
        // 18.5, abajo 19.8 con multiplicador 10).
        RectTransform row = HudRect("FilaHeroes", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        row.anchoredPosition = new Vector2(-18.6f, -32.5f);   // centrada en la franja azul del estandarte (y ~91)
        HorizontalLayoutGroup cards = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        cards.spacing = 2f;                       // ~14 visibles entre tarjetas (el margen del marco suma ~12)
        cards.childAlignment = TextAnchor.UpperRight;
        cards.childControlWidth = cards.childControlHeight = true;
        cards.childForceExpandWidth = cards.childForceExpandHeight = false;
        ContentSizeFitter fitter = row.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        hudHeroes.RemoveAll(h => h == null);

        foreach (HudHero hero in hudHeroes)
        {
            RectTransform card = HudRect("Tarjeta_" + hero.displayName, row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            HudImage(card, hudFrame, Color.white, true, 10f, true);   // recibe el cursor para el hover
            card.gameObject.AddComponent<HoverScale>().HoverFactor = 1.04f;
            LayoutElement size = card.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = 234f;           // 222 visibles
            size.preferredHeight = 118f;          // 80 visibles, la altura comun de la interfaz

            // Hueco cuadrado del retrato: marco vacio, sin imagen.
            RectTransform slot = HudRect("Retrato", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            slot.anchoredPosition = new Vector2(-73f, 0f);
            slot.sizeDelta = new Vector2(52f, 52f);
            HudImage(slot, null, HudGold, false, 1f, false);
            RectTransform slotInner = HudRect("Fondo", slot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            slotInner.offsetMin = new Vector2(2f, 2f);
            slotInner.offsetMax = new Vector2(-2f, -2f);
            HudImage(slotInner, null, HudDark, false, 1f, false);

            RectTransform name = HudRect("Nombre", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f));
            name.anchoredPosition = new Vector2(-37f, 13f);
            name.sizeDelta = new Vector2(140f, 30f);
            hero.nameText = HudText(name, hero.displayName, 24f, HudIvory, TextAlignmentOptions.MidlineLeft);

            RectTransform track = HudRect("BarraVida", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f));
            track.anchoredPosition = new Vector2(-37f, -14f);
            track.sizeDelta = new Vector2(140f, 16f);
            HudImage(track, null, HudGold, false, 1f, false);

            RectTransform inner = HudRect("Fondo", track, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            inner.offsetMin = new Vector2(2f, 2f);
            inner.offsetMax = new Vector2(-2f, -2f);
            HudImage(inner, null, HudPanel, false, 1f, false);

            RectTransform fill = HudRect("Relleno", inner, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f));
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            HudImage(fill, null, HudHealth, false, 1f, false);
            hero.hpFill = fill;
        }
    }

    // Barra inferior: un contenedor a todo el ancho con tres grupos en la misma franja
    // (Coronas a la izquierda, progreso centrado, atajos a la derecha). Cada grupo es un
    // HorizontalLayoutGroup que se ajusta a su contenido.
    private void BuildHudBottomBar(RectTransform root)
    {
        RectTransform bar = HudRect("BarraInferior", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
        bar.offsetMin = new Vector2(BarMargin, 0f);
        bar.offsetMax = new Vector2(-BarMargin - 1f, BarHeight);

        // Izquierda: Coronas.
        RectTransform left = HudGroup("GrupoCoronas", bar, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
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

        // Centro: progreso por region (tres recuadros identicos).
        RectTransform center = HudGroup("GrupoProgreso", bar, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        for (int i = 0; i < HudZones.Length; i++)
        {
            RectTransform box = HudBarFrame("Progreso_" + HudZones[i], center, ProgressWidth, false);
            RectTransform label = HudRect("Texto", box, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            label.offsetMin = new Vector2(30f, 0f);
            label.offsetMax = new Vector2(-30f, 0f);
            hudZoneTexts[i] = HudText(label, ZoneName(HudZones[i]), 29f, HudIvory, TextAlignmentOptions.Midline);
            hudZoneTexts[i].characterSpacing = 2f;
        }

        // Derecha: Inventario y Tienda (cuadrados del mismo tamano, solo icono).
        RectTransform right = HudGroup("GrupoAtajos", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        inventoryButton = HudShortcut(right, "BotonInventario", hudInventoryIcon);
        shopButton = HudShortcut(right, "BotonTienda", hudShopIcon);
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
