using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla de creditos de la demo (escena "Creditos"): fondo oscuro con el logo, texto que sube despacio y al terminar
// (o al omitir con Espacio / Esc / clic) funde a negro, cierra la sesion y vuelve al menu principal.
// Todo se construye en Start (sin dependencias de otras escenas); la musica es opcional.
public class CreditsController : MonoBehaviour
{
    [SerializeField] private Sprite logo;
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private AudioClip music;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.3f;
    [SerializeField] private float scrollSpeed = 70f;
    [SerializeField] private float fadeSeconds = 1.5f;
    [SerializeField] private string menuScene = "Menu";

    private static readonly Color Gold = new Color(0.831f, 0.686f, 0.416f, 1f);
    private static readonly Color Ivory = new Color(0.93f, 0.90f, 0.82f, 1f);

    private RectTransform content;
    private Image blackOverlay;
    private AudioSource musicSource;
    private bool leaving;
    private float musicGain;

    private void Start()
    {
        Time.timeScale = 1f;
        BuildUi();

        if (music != null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = music;
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = 0f;
            musicSource.Play();
        }

        StartCoroutine(Run());
    }

    private void Update()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume * musicGain;

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        bool skip = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame ||
                                          keyboard.enterKey.wasPressedThisFrame)) ||
                    (mouse != null && mouse.leftButton.wasPressedThisFrame);

        if (skip && !leaving)
            StartCoroutine(Leave());
    }

    private IEnumerator Run()
    {
        // Entrada: del negro a la pantalla y la musica sube.
        for (float t = 0f; t < fadeSeconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
        {
            float k = Mathf.SmoothStep(0f, 1f, t / fadeSeconds);
            SetOverlay(1f - k);
            musicGain = k;
            yield return null;
        }

        SetOverlay(0f);
        musicGain = 1f;

        // El texto sube hasta salir por arriba.
        float end = content.rect.height + 1080f;

        while (!leaving && content.anchoredPosition.y < end)
        {
            content.anchoredPosition += new Vector2(0f, scrollSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        if (!leaving)
            yield return Leave();
    }

    private IEnumerator Leave()
    {
        if (leaving)
            yield break;

        leaving = true;
        float start = musicGain;

        for (float t = 0f; t < fadeSeconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
        {
            float k = Mathf.SmoothStep(0f, 1f, t / fadeSeconds);
            SetOverlay(Mathf.Max(blackOverlay.color.a, k));
            musicGain = start * (1f - k);
            yield return null;
        }

        SetOverlay(1f);
        musicGain = 0f;

        // Fin de la demo: la sesion termina y se vuelve al menu principal.
        GameSession.EndSession();
        VictorySummary.Clear();

        if (GameSession.IsSceneInBuild(menuScene))
            SceneManager.LoadScene(menuScene);
    }

    private void SetOverlay(float alpha)
    {
        if (blackOverlay != null)
            blackOverlay.color = new Color(0f, 0f, 0f, alpha);
    }

    // ------------------------------------------------------------
    // Interfaz
    // ------------------------------------------------------------

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("CanvasCreditos", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image bg = NewImage("Fondo", canvasGo.transform, new Color(0.04f, 0.05f, 0.08f, 1f));
        Stretch(bg.rectTransform);

        // Contenedor que sube: logo, titulo y bloques de texto, apilados de arriba abajo.
        GameObject contentGo = new GameObject("Contenido", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(canvasGo.transform, false);
        content = (RectTransform)contentGo.transform;
        content.anchorMin = new Vector2(0.5f, 0f);
        content.anchorMax = new Vector2(0.5f, 0f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(1300f, 0f);
        content.anchoredPosition = Vector2.zero;   // empieza justo bajo la pantalla (la parte superior en y = 0)

        VerticalLayoutGroup layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 14f;
        layout.padding = new RectOffset(0, 0, 0, 240);
        contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (logo != null)
        {
            Image logoImage = NewImage("Logo", contentGo.transform, Color.white);
            logoImage.sprite = logo;
            logoImage.preserveAspect = true;
            LayoutElement le = logoImage.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 360f;
            le.minHeight = 360f;
        }

        AddText("FIN DE LA DEMO", titleFont, 64f, Gold, 40f, 20f);
        AddText("La corrupción del bosque se ha desvanecido.\nPero la guerra contra el Rey Demonio continúa…", bodyFont, 34f, Ivory, 0f, 120f, true);

        // Estadisticas de la partida (GameStats): tabla centrada, etiqueta a la izquierda del eje y valor a la derecha.
        AddSection("Estadísticas de la partida");
        AddStat("Tiempo de juego", GameStats.FormatTime(GameStats.ElapsedSeconds));
        AddStat("Combates ganados", GameStats.Victories.ToString());
        AddStat("Enemigos derrotados", GameStats.EnemiesDefeated.ToString());
        AddStat("Habilidades usadas", GameStats.AbilitiesUsed.ToString());
        AddStat("Daño infligido", GameStats.DamageDealt.ToString("N0"));
        AddStat("Daño recibido", GameStats.DamageTaken.ToString("N0"));
        AddStat("Curación recibida", GameStats.HealingDone.ToString("N0"));
        AddStat("Héroes caídos", GameStats.HeroesDowned.ToString());
        AddStat("Niveles ganados", GameStats.LevelsGained.ToString());
        AddStat("Coronas obtenidas", GameStats.CoinsEarned.ToString("N0"));
        AddStat("Reliquias halladas", GameStats.RelicsFound.ToString());
        AddText("", bodyFont, 10f, Ivory, 0f, 70f);

        AddSection("Equipo de desarrollo");
        AddText("Santana Suaste Roger Emanuel", bodyFont, 34f, Ivory, 0f, 0f);
        AddText("Concepción Jaimes Lizbeth", bodyFont, 34f, Ivory, 0f, 0f);
        AddText("Reyes Chan Erick Gilberto", bodyFont, 34f, Ivory, 0f, 0f);
        AddText("Herrera Pérez Ángel Daniel", bodyFont, 34f, Ivory, 0f, 0f);
        AddText("Romano Chávez Steven", bodyFont, 34f, Ivory, 0f, 80f);

        AddSection("Un videojuego de");
        AddText("Estudio 5", titleFont, 48f, Ivory, 0f, 100f);

        AddText("Gracias por jugar", titleFont, 54f, Gold, 40f, 40f);
        AddText("Reencarnación Express", bodyFont, 36f, Ivory, 0f, 0f);

        blackOverlay = NewImage("FundidoNegro", canvasGo.transform, Color.black);
        Stretch(blackOverlay.rectTransform);
        blackOverlay.raycastTarget = false;
    }

    // Fila de estadistica: una sola linea centrada ("etiqueta  valor"), como el resto de los textos de los creditos.
    private void AddStat(string label, string value)
    {
        string goldHex = ColorUtility.ToHtmlStringRGB(Gold);
        AddText(label + "   <b><color=#" + goldHex + ">" + value + "</color></b>", bodyFont, 34f, Ivory, 0f, 6f);
    }

    private void AddSection(string title)
    {
        AddText(title.ToUpperInvariant(), titleFont, 30f, Gold, 30f, 10f);
    }

    private TMP_Text AddText(string text, TMP_FontAsset font, float size, Color color, float spaceBefore, float spaceAfter, bool italic = false)
    {
        GameObject go = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(content, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();

        if (font != null)
            t.font = font;

        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = italic ? FontStyles.Italic : FontStyles.Normal;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;

        // Espacios entre bloques: relleno sobre el propio elemento (el layout solo tiene un espaciado fijo).
        t.margin = new Vector4(0f, spaceBefore, 0f, spaceAfter);
        return t;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
