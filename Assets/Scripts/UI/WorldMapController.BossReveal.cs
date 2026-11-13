using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Revelacion cinematografica del Trono Sellado: al volver del evento de Valdoria el mapa aparece con el Trono todavia sin
// descubrir; se bloquea la interaccion, la vista se acerca al Trono (el Canvas es Screen Space, asi que se desplaza y escala
// el contenido de "MapArea", no una camara), suena el efecto oscuro, el icono pasa al estado morado con un resplandor breve,
// se mantiene ~1 s y la vista regresa a su posicion original. Nada se modifica de forma permanente.
public partial class WorldMapController
{
    [Header("Revelacion del Trono Sellado")]
    [SerializeField] private AudioClip bossRevealClip;
    [SerializeField, Range(0f, 1f)] private float bossRevealVolume = 0.22f;
    [SerializeField] private float revealPanSeconds = 1.8f;
    [SerializeField] private float revealHoldSeconds = 1f;
    [SerializeField] private float revealReturnSeconds = 1.5f;
    [Tooltip("Zoom ligero durante la revelacion (1 = sin zoom). El desplazamiento se limita para no mostrar fuera del mapa.")]
    [SerializeField, Range(1f, 2f)] private float revealZoom = 1.5f;

    private bool revealPlaying;

    // Interaccion del mapa bloqueada: dialogo narrativo en curso o revelacion del Trono Sellado.
    private bool InputLocked => DialogueController.IsPlaying || revealPlaying;

    public bool BossRevealPlaying => revealPlaying;

    private void StartBossRevealIfPending()
    {
        if (!GameSession.BossRevealPending || revealPlaying)
            return;

        LocationView boss = null;

        foreach (LocationView view in locationViews)
            if (view != null && view.boss)
                boss = view;

        if (boss == null || boss.rect == null)
        {
            GameSession.ConsumeBossReveal();   // sin Trono en el mapa: no se queda pendiente
            return;
        }

        revealPlaying = true;   // bloquea el mapa desde ya, antes de que el fundido termine
        StartCoroutine(BossRevealRoutine(boss));
    }

    private IEnumerator BossRevealRoutine(LocationView boss)
    {
        // Contenido que se desplaza: MapArea (fondo, niebla y marcadores). El HUD queda fuera y no se mueve.
        RectTransform area = boss.rect;

        while (area != null && area.name != "MapArea")
            area = area.parent as RectTransform;

        Canvas rootCanvas = canvas != null ? canvas : GetComponentInParent<Canvas>();

        if (area == null || rootCanvas == null)
        {
            GameSession.ConsumeBossReveal();
            revealPlaying = false;
            yield break;
        }

        // Bloqueo de clics sobre todo el mapa (HUD incluido) mientras dura la secuencia.
        GameObject blockerGo = new GameObject("BloqueoRevelacion", typeof(RectTransform), typeof(Image));
        blockerGo.transform.SetParent(rootCanvas.transform, false);
        RectTransform blockerRect = (RectTransform)blockerGo.transform;
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
        Image blocker = blockerGo.GetComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f);
        blocker.raycastTarget = true;

        // Espera a que termine el fundido de entrada del mapa.
        SceneFadeIn fade = FindAnyObjectByType<SceneFadeIn>(FindObjectsInactive.Include);

        while (fade != null && fade.IsFading)
            yield return null;

        yield return new WaitForSecondsRealtime(0.35f);

        // Destino: el Trono centrado si es posible; el desplazamiento se limita para no ver fuera del mapa.
        Vector2 startPos = area.anchoredPosition;
        Vector3 startScale = area.localScale;
        Vector2 bossLocal = area.InverseTransformPoint(boss.rect.position);
        float zoom = revealZoom;
        Vector2 half = (zoom - 1f) * 0.5f * area.rect.size;
        Vector2 targetPos = new Vector2(
            Mathf.Clamp(-bossLocal.x * zoom, -half.x, half.x),
            Mathf.Clamp(-bossLocal.y * zoom, -half.y, half.y));

        // Sonido: su momento mas fuerte coincide con la aparicion del icono morado (al llegar).
        AudioSource sfx = null;
        float peak = 0f;

        if (bossRevealClip != null)
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.loop = false;
            sfx.spatialBlend = 0f;
            sfx.clip = bossRevealClip;
            sfx.volume = bossRevealVolume;
            peak = ClipPeakTime(bossRevealClip);
        }

        float soundDelay = Mathf.Max(0f, revealPanSeconds - peak);
        bool soundStarted = false;

        for (float t = 0f; t < revealPanSeconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
        {
            if (sfx != null && !soundStarted && t >= soundDelay)
            {
                soundStarted = true;
                sfx.Play();
            }

            ApplyRevealView(area, startPos, startScale, targetPos, zoom, Mathf.SmoothStep(0f, 1f, t / revealPanSeconds));
            yield return null;
        }

        ApplyRevealView(area, startPos, startScale, targetPos, zoom, 1f);

        if (sfx != null && !soundStarted)
            sfx.Play();

        // El Trono se descubre: icono morado permanente + resplandor breve.
        GameSession.ConsumeBossReveal();
        Refresh();
        StartCoroutine(BossGlowRoutine(boss));

        yield return new WaitForSecondsRealtime(revealHoldSeconds);

        for (float t = 0f; t < revealReturnSeconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
        {
            ApplyRevealView(area, startPos, startScale, targetPos, zoom, 1f - Mathf.SmoothStep(0f, 1f, t / revealReturnSeconds));
            yield return null;
        }

        // Vista original exacta.
        area.anchoredPosition = startPos;
        area.localScale = startScale;

        if (blockerGo != null)
            Destroy(blockerGo);

        // El sonido termina solo (sin cortarlo); la fuente se retira cuando acaba.
        if (sfx != null)
            Destroy(sfx, sfx.clip.length + 0.5f);

        revealPlaying = false;
    }

    private static void ApplyRevealView(RectTransform area, Vector2 startPos, Vector3 startScale, Vector2 targetPos, float zoom, float k)
    {
        area.anchoredPosition = Vector2.Lerp(startPos, targetPos, k);
        area.localScale = Vector3.Lerp(startScale, startScale * zoom, k);
    }

    // Momento del clip con mas energia (ventana de 50 ms); si no se puede leer, ~40 % de su duracion.
    private static float ClipPeakTime(AudioClip clip)
    {
        float fallback = clip.length * 0.4f;

        try
        {
            int channels = clip.channels;
            float[] data = new float[clip.samples * channels];

            if (!clip.GetData(data, 0))
                return fallback;

            int window = Mathf.Max(1, Mathf.RoundToInt(clip.frequency * 0.05f)) * channels;
            double best = 0;
            int bestStart = 0;

            for (int i = 0; i + window <= data.Length; i += window)
            {
                double sum = 0;

                for (int j = i; j < i + window; j++)
                    sum += data[j] * data[j];

                if (sum > best)
                {
                    best = sum;
                    bestStart = i;
                }
            }

            return (bestStart / (float)channels) / clip.frequency;
        }
        catch (System.Exception)
        {
            return fallback;
        }
    }

    // Resplandor morado discreto sobre el icono del Trono (se crea y se destruye aqui, sin tocar el medallon).
    private IEnumerator BossGlowRoutine(LocationView boss)
    {
        GameObject go = new GameObject("ResplandorMorado", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(boss.rect, false);
        go.transform.SetAsFirstSibling();
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = boss.badge != null ? boss.badge.anchoredPosition : Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.sprite = SpSoft != null ? SpSoft : SpCircle;
        image.raycastTarget = false;
        Color purple = new Color(0.62f, 0.30f, 0.95f, 0f);
        const float seconds = 1.4f;

        for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
        {
            float k = t / seconds;
            purple.a = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.7f;   // sube y baja una sola vez
            image.color = purple;
            float size = Mathf.Lerp(MedallionSize * 1.6f, MedallionSize * 3.2f, Mathf.SmoothStep(0f, 1f, k));
            rect.sizeDelta = new Vector2(size, size);
            yield return null;
        }

        Destroy(go);
    }
}
