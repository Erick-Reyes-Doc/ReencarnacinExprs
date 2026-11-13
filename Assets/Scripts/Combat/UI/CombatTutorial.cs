using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Tutorial interactivo del primer combate (Un mal dia para cobrar). Se activa solo cuando la escena de dialogo inicial
// lo pide (GameSession.CombatTutorialPending). Oscurece el HUD dejando iluminada la zona que se explica:
//  - pasos "Explicar": el resto queda bloqueado y se avanza con ESPACIO;
//  - pasos "Actuar": solo la zona iluminada responde a clics (el jugador usa su habilidad de verdad).
// Solo lee el estado del combate y escucha dos eventos del HUD; no cambia reglas, enemigos ni recompensas.
public class CombatTutorial : MonoBehaviour
{
    [SerializeField] private CombatHudController hud;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private Sprite cardSprite;
    [SerializeField] private TMP_FontAsset regularFont;
    [SerializeField] private TMP_FontAsset boldFont;
    [SerializeField] private int sortingOrder = 350;
    [Header("Boton Omitir (sprites del menu)")]
    [SerializeField] private Sprite skipNormal;
    [SerializeField] private Sprite skipHover;

    private static readonly Color Dim = new Color(0.02f, 0.03f, 0.06f, 0.72f);
    private static readonly Color Gold = new Color(0.96f, 0.80f, 0.40f, 1f);
    private static readonly Color Ivory = new Color(0.93f, 0.90f, 0.82f, 1f);

    private RectTransform root;
    private Canvas uiCanvas;
    private readonly Image[] dims = new Image[4];       // arriba, abajo, izquierda, derecha del hueco
    private readonly Image[] frame = new Image[4];      // borde dorado del hueco
    private Image holeBlocker;                          // bloquea clics dentro del hueco (pasos Explicar)
    private RectTransform card;
    private TMP_Text cardText;
    private TMP_Text cardHint;

    private string currentArea;
    private bool showHole;
    private bool blockHole;

    // Intencion del guion: el cartel se oculta mientras el combate esta en pausa y vuelve al reanudar.
    private bool tutorialVisible;

    private bool abilityTargeting;
    private bool abilityUsed;
    private UnityEngine.Object lastPlayerUnit;

    private void Start()
    {
        if (!GameSession.ConsumeCombatTutorial() || hud == null || turnManager == null)
        {
            enabled = false;
            return;
        }

        BuildUi();
        hud.TargetSelectionStarted += OnTargetSelectionStarted;
        hud.AbilityUsed += OnAbilityUsed;
        StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        if (hud != null)
        {
            hud.TargetSelectionStarted -= OnTargetSelectionStarted;
            hud.AbilityUsed -= OnAbilityUsed;
        }
    }

    private void OnTargetSelectionStarted(AbilityData ability) { abilityTargeting = true; }
    private void OnAbilityUsed() { abilityUsed = true; }

    // ---------------- Guion ----------------

    private IEnumerator Run()
    {
        yield return WaitUntil(() => turnManager.BattleActive && turnManager.CurrentUnit != null);
        yield return new WaitForSecondsRealtime(0.8f);

        yield return Explain("orden",
            "Este es el orden de turno. Cada retrato es una unidad y actúan de izquierda a derecha, según su velocidad. Los marcos rojos son enemigos.");

        yield return Explain("ejercitos",
            "Tus héroes están a la izquierda y los enemigos a la derecha. La barra roja es la vida: si la de un héroe llega a cero, cae. La posición (P1 a P4) importa: cada habilidad solo funciona desde ciertas posiciones.");

        yield return WaitUntil(PlayerCanAct);

        yield return Explain("habilidades",
            "Este es el turno de tu héroe. Abajo están sus cuatro habilidades. El número es su tiempo de recarga. Cada turno puedes hacer una sola acción.");

        abilityTargeting = false;
        abilityUsed = false;
        yield return Act("habilidades",
            "Elige una habilidad con un clic. Pasa el cursor sobre ella si quieres leer qué hace.",
            () => abilityTargeting || abilityUsed || Finished);

        if (!abilityUsed && !Finished)
        {
            yield return Explain("info",
                "Aquí ves el daño real de la habilidad, desde qué posiciones se puede usar y a cuáles alcanza.");

            AbilityData chosen = hud.SelectedAbilityForTutorial;
            bool attack = chosen == null || chosen.abilityType == AbilityType.Attack;
            yield return Act(attack ? "enemigos" : "aliados",
                attack
                    ? "Ahora haz clic en un enemigo marcado con borde rojo para atacarlo."
                    : "Ahora haz clic en el aliado que recibirá la habilidad.",
                () => abilityUsed || Finished);
        }

        if (Finished)
            yield break;

        // Pasan los demas turnos.
        yield return Banner("¡Bien hecho! Usar una habilidad gasta tu acción y el turno pasa a la siguiente unidad del orden.",
            () => Finished || PlayerCanAct());

        yield return WaitUntil(PlayerCanAct);
        yield return new WaitForSecondsRealtime(0.5f);

        yield return Explain("mover",
            "Mover cambia la posición de tu héroe una casilla hacia delante o hacia atrás. También cuenta como tu acción del turno.");

        yield return Explain("consumibles",
            "Aquí están tus consumibles: la poción cura vida y el antídoto quita el sangrado, la quemadura y el veneno. Usar uno también gasta tu acción.");

        yield return Explain("terminar",
            "Si no quieres actuar, Terminar turno pasa al siguiente. Cuando una unidad llega a cero de vida, cae del combate.");

        yield return Explain(null,
            "Derrota a todos los enemigos para ganar. Recibirás experiencia y coronas. ¡Buena suerte!");

        Hide();
    }

    // Omitir: detiene el guion y quita el tutorial; el combate sigue con normalidad.
    public void Skip()
    {
        StopAllCoroutines();
        Hide();
        enabled = false;
    }

    private bool Finished => turnManager == null || turnManager.BattleFinished;

    private bool PlayerCanAct()
    {
        if (Finished || !turnManager.BattleActive)
            return false;

        BattleUnit unit = turnManager.CurrentUnit;
        return unit != null && unit.IsAlive && unit.Side == TeamSide.Player && hud.ActivePlayerForTutorial != null;
    }

    private IEnumerator WaitUntil(Func<bool> condition)
    {
        while (!condition() && !Finished)
            yield return null;
    }

    // ---------------- Pasos ----------------

    private IEnumerator Explain(string area, string text)
    {
        if (Finished)
            yield break;

        Show(area, text, true, true);
        cardHint.text = "ESPACIO para continuar";
        float guard = Time.unscaledTime + 0.35f;

        while (!Finished)
        {
            Keyboard keyboard = Keyboard.current;

            if (Time.unscaledTime >= guard && !hud.IsPaused && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                break;

            yield return null;
        }

        Hide();
        yield return null;
    }

    private IEnumerator Act(string area, string text, Func<bool> done)
    {
        if (Finished)
            yield break;

        Show(area, text, true, false);
        cardHint.text = "";

        while (!done())
            yield return null;

        Hide();
        yield return null;
    }

    // Aviso sin bloqueo (los turnos siguen).
    private IEnumerator Banner(string text, Func<bool> done)
    {
        if (Finished)
            yield break;

        Show(null, text, false, false);
        cardHint.text = "";
        float minimum = Time.unscaledTime + 2.5f;

        while (!done() || Time.unscaledTime < minimum)
        {
            if (Finished)
                break;

            yield return null;
        }

        Hide();
    }

    private void Show(string area, string text, bool dim, bool explain)
    {
        currentArea = area;
        showHole = dim;
        blockHole = explain;
        cardText.text = text;
        tutorialVisible = true;
        root.gameObject.SetActive(!hud.IsPaused);

        if (root.gameObject.activeSelf)
            Layout();
    }

    private void Hide()
    {
        currentArea = null;
        showHole = false;
        blockHole = false;
        tutorialVisible = false;

        if (root != null)
            root.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (root == null)
            return;

        // En pausa el tutorial se oculta (su capa oscura tapaba el menu y bloqueaba sus botones); al reanudar vuelve.
        bool want = tutorialVisible && !hud.IsPaused;

        if (root.gameObject.activeSelf != want)
            root.gameObject.SetActive(want);

        if (want)
            Layout();
    }

    // ---------------- Interfaz ----------------

    private void Layout()
    {
        Rect half = root.rect;
        float hw = half.width * 0.5f, hh = half.height * 0.5f;

        Rect hole = Rect.zero;
        bool hasHole = showHole && hud.TutorialBounds(currentArea, out Rect screen) && ToLocal(screen, out hole);

        if (!showHole)
        {
            SetDims(false, 0, 0, 0, 0, hw, hh);
            SetFrame(false, hole);
            holeBlocker.gameObject.SetActive(false);
        }
        else if (!hasHole)
        {
            // Sin zona concreta (mensaje general): se oscurece todo y se bloquea.
            SetDims(true, 0, 0, 0, 0, hw, hh);
            SetFrame(false, hole);
            holeBlocker.gameObject.SetActive(false);
        }
        else
        {
            const float pad = 10f;
            hole = new Rect(hole.xMin - pad, hole.yMin - pad, hole.width + pad * 2f, hole.height + pad * 2f);
            SetDims(true, hole.xMin, hole.xMax, hole.yMin, hole.yMax, hw, hh);
            SetFrame(true, hole);
            holeBlocker.gameObject.SetActive(blockHole);

            if (blockHole)
            {
                RectTransform r = holeBlocker.rectTransform;
                r.anchoredPosition = hole.center;
                r.sizeDelta = hole.size;
            }
        }

        // Tarjeta: justo bajo el encabezado; si la zona iluminada es el propio encabezado, baja al centro para no taparla.
        bool highlightTop = hasHole && hole.center.y > hh - 170f;
        card.anchoredPosition = new Vector2(0f, highlightTop ? hh - 430f : hh - 218f);
    }

    private void SetDims(bool visible, float xMin, float xMax, float yMin, float yMax, float hw, float hh)
    {
        for (int i = 0; i < 4; i++)
            dims[i].gameObject.SetActive(visible);

        if (!visible)
            return;

        bool full = xMin == 0f && xMax == 0f && yMin == 0f && yMax == 0f;

        if (full)
        {
            Place(dims[0], -hw, hw, -hh, hh);
            for (int i = 1; i < 4; i++) dims[i].gameObject.SetActive(false);
            return;
        }

        Place(dims[0], -hw, hw, yMax, hh);          // arriba
        Place(dims[1], -hw, hw, -hh, yMin);         // abajo
        Place(dims[2], -hw, xMin, yMin, yMax);      // izquierda
        Place(dims[3], xMax, hw, yMin, yMax);       // derecha
    }

    private void SetFrame(bool visible, Rect hole)
    {
        for (int i = 0; i < 4; i++)
            frame[i].gameObject.SetActive(visible);

        if (!visible)
            return;

        const float t = 3f;
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
        Color c = new Color(Gold.r, Gold.g, Gold.b, pulse);

        Place(frame[0], hole.xMin, hole.xMax, hole.yMax - t, hole.yMax);
        Place(frame[1], hole.xMin, hole.xMax, hole.yMin, hole.yMin + t);
        Place(frame[2], hole.xMin, hole.xMin + t, hole.yMin, hole.yMax);
        Place(frame[3], hole.xMax - t, hole.xMax, hole.yMin, hole.yMax);

        for (int i = 0; i < 4; i++)
            frame[i].color = c;
    }

    private static void Place(Image image, float xMin, float xMax, float yMin, float yMax)
    {
        RectTransform r = image.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
        r.sizeDelta = new Vector2(Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
    }

    private bool ToLocal(Rect screen, out Rect local)
    {
        local = Rect.zero;
        Vector2 a, b;
        Camera cam = uiCanvas != null && uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? uiCanvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(screen.xMin, screen.yMin), cam, out a) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(screen.xMax, screen.yMax), cam, out b))
            return false;

        local = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        return true;
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("TutorialCombateCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        uiCanvas = canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)canvasGo.transform;

        for (int i = 0; i < 4; i++)
        {
            dims[i] = NewImage("Oscuro_" + i, Dim, true);
            frame[i] = NewImage("Borde_" + i, Gold, false);
        }

        holeBlocker = NewImage("BloqueoZona", new Color(0f, 0f, 0f, 0f), true);

        // Tarjeta de texto (sprite del cuadro de dialogo; si falta, un fondo azul marino).
        GameObject cardGo = new GameObject("Tarjeta", typeof(RectTransform), typeof(Image));
        cardGo.transform.SetParent(root, false);
        card = (RectTransform)cardGo.transform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        Image cardImage = cardGo.GetComponent<Image>();
        cardImage.raycastTarget = false;

        if (cardSprite != null)
        {
            const float W = 1360f;
            card.sizeDelta = new Vector2(W, W * 724f / 2172f);
            cardImage.sprite = cardSprite;
            cardImage.preserveAspect = true;
        }
        else
        {
            card.sizeDelta = new Vector2(1200f, 200f);
            cardImage.color = new Color(0.07f, 0.10f, 0.16f, 0.96f);
        }

        // Texto centrado (horizontal y vertical) dentro del marco visible; deja sitio abajo para la pista.
        cardText = NewText("Texto", card, Ivory, regularFont, 28f, TextAlignmentOptions.Center);
        cardText.rectTransform.sizeDelta = new Vector2(1160f, 104f);
        cardText.rectTransform.anchoredPosition = new Vector2(0f, 12f);
        cardText.textWrappingMode = TextWrappingModes.Normal;

        cardHint = NewText("Pista", card, Gold, boldFont, 20f, TextAlignmentOptions.BottomRight);
        cardHint.rectTransform.sizeDelta = new Vector2(1240f, 130f);
        cardHint.rectTransform.anchoredPosition = new Vector2(0f, 4f);

        // Omitir: cierra el tutorial en cualquier paso (igual que el boton de la introduccion).
        if (skipNormal != null)
            SkipButtonFactory.Create(card, "OMITIR TUTORIAL", skipNormal, skipHover != null ? skipHover : skipNormal, boldFont, Skip,
                new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), new Vector2(620f, -118f), 330f, 100f);

        root.gameObject.SetActive(false);
    }

    private Image NewImage(string name, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static TMP_Text NewText(string name, RectTransform parent, Color color, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();

        if (font != null)
            t.font = font;

        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.richText = false;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        return t;
    }
}
