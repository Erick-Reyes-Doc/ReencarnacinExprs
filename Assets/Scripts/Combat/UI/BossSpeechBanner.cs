using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Frases del jefe con protagonismo: un rotulo grande (nombre del hablante + cita) en el centro de la pantalla, por encima de la
// interfaz de combate, con aparicion y desvanecimiento suaves. Antes las frases se perdian en el cuadro de texto de acciones.
// Solo presentacion: no cambia ninguna regla. Al hablar, el jefe lanza su animacion "Hablar" si la tiene.
public class BossSpeechBanner : MonoBehaviour
{
    private const float FadeIn = 0.25f;
    private const float FadeOut = 0.45f;
    private const float SecondsPerChar = 0.045f;
    private const float MinSeconds = 2.6f;
    private const float MaxSeconds = 5f;

    private static BossSpeechBanner instance;

    private CanvasGroup group;
    private Text speakerText;
    private Text lineText;
    private Coroutine routine;
    private RectTransform canvasRect;
    private RectTransform bubble;
    private RectTransform tail;

    private const float BubbleWidth = 560f;
    private const float BubbleHeight = 170f;
    private const float TailGap = 34f;   // separacion entre la punta de la cola y la boca

    // Coloca el globo junto a la boca del hablante, hacia el lado con mas espacio, con la cola apuntando a la boca.
    private void Place(BattleUnit speaker)
    {
        Camera cam = Camera.main;
        Vector2 half = canvasRect != null ? canvasRect.rect.size * 0.5f : new Vector2(960f, 540f);
        Vector2 mouth = new Vector2(0f, 120f);

        if (speaker != null && cam != null && canvasRect != null)
        {
            // Boca del jefe: ~2.1 unidades sobre su pivote a la escala de 0.28 (cabeza de calavera en lo alto de la figura).
            Vector3 world = speaker.transform.position + Vector3.up * (2.1f * speaker.transform.lossyScale.y / 0.28f);
            Vector2 screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out mouth);
        }

        bool bubbleOnLeft = mouth.x > 0f;   // hablante en la mitad derecha: el globo va a su izquierda
        float cx = mouth.x + (bubbleOnLeft ? -1f : 1f) * (TailGap + BubbleWidth * 0.5f + 8f);
        float cy = mouth.y + 40f;

        cx = Mathf.Clamp(cx, -half.x + BubbleWidth * 0.5f + 12f, half.x - BubbleWidth * 0.5f - 12f);
        cy = Mathf.Clamp(cy, -half.y + BubbleHeight * 0.5f + 12f, half.y - BubbleHeight * 0.5f - 12f);
        bubble.anchoredPosition = new Vector2(cx, cy);

        float edgeX = cx + (bubbleOnLeft ? 1f : -1f) * BubbleWidth * 0.5f;
        float tailY = Mathf.Clamp(mouth.y, cy - BubbleHeight * 0.5f + 34f, cy + BubbleHeight * 0.5f - 34f);
        tail.anchoredPosition = new Vector2(edgeX + (bubbleOnLeft ? 1f : -1f) * 10f, tailY);
    }

    // Muestra 'line' dicha por 'speaker' (nombre en mayusculas arriba). 'font' = la fuente de la interfaz de combate.
    public static void Show(BattleUnit speaker, string line, Font font)
    {
        if (string.IsNullOrEmpty(line))
            return;

        if (instance == null)
            instance = Build(font);

        string name = speaker != null && speaker.Data != null ? speaker.Data.unitName : "";
        instance.Place(speaker);
        instance.Display(name, line);

        // El jefe mueve la boca y hace un gesto mientras habla (si tiene la animacion).
        UnitAnimationDriver driver = speaker != null ? speaker.GetComponent<UnitAnimationDriver>() : null;

        if (driver != null)
            driver.PlayTalk();
    }

    private static BossSpeechBanner Build(Font font)
    {
        GameObject root = new GameObject("FraseDelJefe", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BossSpeechBanner banner = root.AddComponent<BossSpeechBanner>();
        banner.group = root.GetComponent<CanvasGroup>();
        banner.group.alpha = 0f;
        banner.group.blocksRaycasts = false;
        banner.group.interactable = false;

        banner.canvasRect = (RectTransform)root.transform;
        Color fill = new Color(0.04f, 0.05f, 0.09f, 1f);   // opaco: con transparencia el borde dorado se tine de marron
        Color gold = new Color(0.78f, 0.60f, 0.28f, 1f);

        // Cola del globo: rombo girado 45 grados que sale por el borde del globo hacia la boca del hablante.
        // Se dibuja ANTES que el globo, que tapa su mitad interior; asi solo asoma la punta con borde dorado.
        GameObject tail = new GameObject("Cola", typeof(RectTransform), typeof(Image), typeof(Outline));
        tail.transform.SetParent(root.transform, false);
        RectTransform tr = (RectTransform)tail.transform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.sizeDelta = new Vector2(46f, 46f);
        tr.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tail.GetComponent<Image>().color = fill;
        tail.GetComponent<Outline>().effectColor = gold;
        tail.GetComponent<Outline>().effectDistance = new Vector2(3f, -3f);
        banner.tail = tr;

        // Globo de dialogo junto a la cabeza del hablante (se coloca en Place).
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(root.transform, false);
        RectTransform pr = (RectTransform)panel.transform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(BubbleWidth, BubbleHeight);
        panel.GetComponent<Image>().color = fill;
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = gold;
        outline.effectDistance = new Vector2(3f, -3f);
        banner.bubble = pr;

        banner.speakerText = MakeText(panel.transform, font, "Hablante", 22, FontStyle.Bold, new Color(0.95f, 0.72f, 0.33f), TextAnchor.MiddleCenter,
            new Vector2(0.03f, 0.70f), new Vector2(0.97f, 0.95f));
        banner.speakerText.resizeTextForBestFit = true;
        banner.speakerText.resizeTextMinSize = 13;
        banner.speakerText.resizeTextMaxSize = 22;
        banner.lineText = MakeText(panel.transform, font, "Frase", 34, FontStyle.Italic, new Color(0.96f, 0.93f, 0.85f), TextAnchor.MiddleCenter,
            new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.68f));
        banner.lineText.resizeTextForBestFit = true;
        banner.lineText.resizeTextMinSize = 18;
        banner.lineText.resizeTextMaxSize = 34;

        // Linea fina entre nombre y frase.
        GameObject rule = new GameObject("Linea", typeof(RectTransform), typeof(Image));
        rule.transform.SetParent(panel.transform, false);
        RectTransform rr = (RectTransform)rule.transform;
        rr.anchorMin = new Vector2(0.15f, 0.69f);
        rr.anchorMax = new Vector2(0.85f, 0.69f);
        rr.sizeDelta = new Vector2(0f, 2f);
        rule.GetComponent<Image>().color = new Color(0.78f, 0.60f, 0.28f, 0.7f);

        return banner;
    }

    private static Text MakeText(Transform parent, Font font, string name, int size, FontStyle style, Color color, TextAnchor anchor, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = min;
        r.anchorMax = max;
        r.offsetMin = r.offsetMax = Vector2.zero;
        Text t = go.GetComponent<Text>();
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        Outline o = go.GetComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.85f);
        o.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    private void Display(string speaker, string line)
    {
        speakerText.text = speaker.ToUpperInvariant();
        lineText.text = line.Trim(' ', '«', '»');   // algunas frases de uso ya traen comillas angulares

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(Run(Mathf.Clamp(line.Length * SecondsPerChar, MinSeconds, MaxSeconds)));
    }

    private IEnumerator Run(float hold)
    {
        for (float t = 0f; t < FadeIn; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.SmoothStep(0f, 1f, t / FadeIn);
            yield return null;
        }

        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(hold);

        for (float t = 0f; t < FadeOut; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / FadeOut);
            yield return null;
        }

        group.alpha = 0f;
        routine = null;
    }

    // Oculta la frase al momento (cambio de escena / fin del combate).
    public static void Hide()
    {
        if (instance != null && instance.group != null)
        {
            if (instance.routine != null)
                instance.StopCoroutine(instance.routine);

            instance.routine = null;
            instance.group.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
