using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Cinematica de introduccion (IntroCinematic.unity). Se abre desde Nueva partida (el menu ya inicio la sesion con
// GameSession.StartNewGame; aqui NO se vuelve a iniciar) y al terminar u omitirse carga el mapa.
//
// - 11 imagenes fijas 16:9 con fundido cruzado; sin movimiento de camara ni de personajes.
// - Una sola linea de tiempo (reloj propio, tiempo no escalado): imagenes, voces, efectos, ducking, logo y fin
//   salen de ella. Pausa = el reloj y TODO el audio se detienen; al reanudar continua exactamente donde iba.
// - Todo es editable desde el Inspector: duracion y fundido de cada escena, retraso de voz, efectos (escena de
//   inicio, desfase, bucle, escena final, volumen) y volumenes de musica, voces y efectos.
// - Las voces nunca se cortan al cambiar de imagen: cada una suena en su propia fuente hasta terminar.
public class IntroCinematicController : MonoBehaviour
{
    [Serializable]
    public class IntroShot
    {
        public string name;
        public Sprite image;
        [Tooltip("Duracion de la escena en segundos.")]
        public float duration = 10f;
        [Tooltip("Duracion del fundido cruzado desde la imagen anterior (0 = corte directo).")]
        public float fadeIn = 1f;
        public AudioClip voice;
        [Tooltip("Retraso de la voz respecto al inicio de la escena (s).")]
        public float voiceDelay;
        [Range(0f, 1f)] public float voiceVolume = 1f;
    }

    [Serializable]
    public class IntroEffect
    {
        public string name;
        public AudioClip clip;
        [Tooltip("Escena (1-11) en la que empieza.")]
        public int startShot = 1;
        [Tooltip("Desfase respecto al inicio de esa escena (s). Puede ser negativo o mayor que la escena.")]
        public float offset;
        [Tooltip("En bucle hasta el final de 'Escena final'. Si no, suena una sola vez.")]
        public bool loop;
        [Tooltip("Ultima escena (1-11) en la que suena el bucle; se apaga con fundido al terminar.")]
        public int endShot = 1;
        public float fadeIn = 0.5f;
        public float fadeOut = 1.5f;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Ambiente: baja con la narracion y con el impacto. Los efectos puntuales no se atenuan.")]
        public bool ambient = true;
    }

    // Subtitulo: se ancla a la voz de una escena (usa su retraso real), asi que sigue cualquier ajuste de tiempos
    // sin temporizadores propios. start/end son segundos desde el inicio del audio de esa escena.
    [Serializable]
    public class SubtitleCue
    {
        [Tooltip("Escena (1-10) cuya narracion acompana.")]
        public int shot = 1;
        [Tooltip("Segundo del audio en que empieza la frase.")]
        public float start;
        [Tooltip("Segundo del audio en que termina la frase.")]
        public float end;
        [Tooltip("Quien habla: NARRADOR o DIOS (vacio = sin etiqueta).")]
        public string speaker;
        [TextArea(1, 3)] public string text;
    }

    [Header("Escenas (1-11)")]
    public List<IntroShot> shots = new List<IntroShot>();

    [Header("Efectos de sonido")]
    public List<IntroEffect> effects = new List<IntroEffect>();

    [Header("Subtitulos")]
    public List<SubtitleCue> subtitles = new List<SubtitleCue>();
    public CanvasGroup subtitleGroup;
    public RectTransform subtitleBox;
    public TMP_Text subtitleText;
    [Tooltip("Etiqueta de quien habla, sobre el texto.")]
    public TMP_Text subtitleSpeaker;
    public string divineSpeaker = "DIOS";
    public float speakerHeight = 30f;
    public Color narratorColor = new Color(0.831f, 0.686f, 0.416f, 1f);   // dorado #D4AF6A
    public Color divineColor = new Color(0.949f, 0.851f, 0.553f, 1f);     // dorado claro
    [Tooltip("Fundido de aparicion y desaparicion (s).")]
    public float subtitleFade = 0.2f;
    public float subtitleMaxWidth = 1380f;
    public Vector2 subtitlePadding = new Vector2(44f, 20f);

    [Header("Musica")]
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.7f;

    [Header("Volumenes independientes")]
    [Range(0f, 1f)] public float voiceVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Narracion: la musica y los ambientes bajan mientras habla la voz")]
    [Range(0f, 1f)] public float musicDuck = 0.3f;
    [Range(0f, 1f)] public float ambientDuck = 0.7f;
    public float duckFade = 0.6f;

    [Header("Impacto del accidente: caida breve de musica y ambientes")]
    [Tooltip("Escena (1-11) del impacto.")]
    public int impactShot = 5;
    [Tooltip("Momento del golpe, en segundos desde el inicio de esa escena. Aqui cae la pantalla negra.")]
    public float impactOffset = 9f;
    [Tooltip("Pantalla negra del choque: tiempo de caida a negro (s).")]
    public float blackoutRise = 0.06f;
    [Tooltip("Negro hasta el final de la escena del impacto; se aclara en este tiempo (s) al empezar la siguiente.")]
    public float blackoutRelease = 0.5f;
    [Range(0f, 1f)] public float impactDepth = 0.06f;
    public float impactHold = 1.2f;
    public float impactRelease = 1.5f;

    [Header("Logo (escena final)")]
    public Image logoImage;
    public int logoShot = 11;
    public float logoDelay = 0.4f;
    public float logoFadeIn = 1.5f;
    [Tooltip("Ancho del logo a escala 1 (pixeles del lienzo 1920x1080). Conserva sus proporciones.")]
    public float logoBaseWidth = 900f;
    public float logoStartScale = 0.85f;
    [Tooltip("Escala final de la deriva lenta durante la pausa (1 = sin movimiento).")]
    public float logoHoldScale = 1.03f;
    [Tooltip("Zoom corto durante el fundido final.")]
    public float logoExitScale = 1.6f;

    [Header("Interfaz")]
    public Image[] layers = new Image[2];
    public AspectRatioFitter[] fitters = new AspectRatioFitter[2];
    public Image blackOverlay;
    public Button skipButton;
    public Button pauseButton;
    public TMP_Text pauseButtonLabel;
    public GameObject pausePanel;
    public Button resumeButton;
    public Button pauseSkipButton;

    [Header("Transiciones")]
    [Tooltip("Fundido desde negro al empezar.")]
    public float openFade = 1.2f;
    [Tooltip("Fundido a negro antes de cargar el mapa al terminar normalmente.")]
    public float endFade = 2f;
    [Tooltip("Fundido a negro (imagen y audio) al omitir.")]
    public float skipFade = 0.8f;
    [Tooltip("Escena a la que se pasa al terminar u omitir (debe estar en Build Settings).")]
    public string nextScene = "Map";

    private class FxRun
    {
        public IntroEffect data;
        public AudioSource source;
        public float start;
        public float stop = -1f;
        public bool started;
        public bool done;
    }

    private struct Cue
    {
        public float time;
        public Action action;
    }

    private readonly List<Cue> cues = new List<Cue>();
    private readonly List<FxRun> runs = new List<FxRun>();
    private readonly List<AudioSource> sources = new List<AudioSource>();
    private readonly List<Vector2> voiceWindows = new List<Vector2>();
    private float[] starts;
    private float total;
    private float clock;
    private int cueIndex;
    private int shownShot = -1;
    private int top;
    private float crossFade = 1f;
    private float crossDuration = 1f;
    private float duck;
    private float skipProgress;
    private bool paused;
    private bool skipping;
    private bool finished;
    private bool loading;
    private AudioSource musicSource;
    private float impactTime = -1f;
    private float blackoutEnd = -1f;
    private float logoStartTime;
    private Image glow;

    public float Clock => clock;
    public float TotalDuration => total;
    public int CurrentShot => shownShot + 1;
    public bool IsPaused => paused;
    public bool IsLoadingNextScene => loading;

    private void Awake()
    {
        Time.timeScale = 1f;
    }

    private void Start()
    {
        // La sesion ya la inicio el menu (una sola vez). Solo si esta escena se abre directamente en el editor
        // se crea la sesion de desarrollo, igual que hace el mapa.
        GameSession.EnsureSession();

        if (shots.Count == 0)
        {
            Debug.LogError("Introduccion: no hay escenas configuradas; se carga el mapa.");
            LoadNextScene();
            return;
        }

        BuildTimeline();
        WireButtons();

        if (blackOverlay != null)
        {
            blackOverlay.color = Color.black;
            blackOverlay.raycastTarget = false;
        }

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (logoImage != null)
            logoImage.gameObject.SetActive(false);
        CreateGlow();

        for (int i = 0; i < layers.Length; i++)
            if (layers[i] != null)
                layers[i].enabled = false;

        if (music != null && musicSource != null)
        {
            musicSource.clip = music;
            musicSource.loop = false;
            musicSource.volume = 0f;
            musicSource.Play();
        }

        ShowShot(0);
        UpdateVisuals();
    }

    private AudioSource NewSource(string sourceName)
    {
        GameObject go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        sources.Add(source);
        return source;
    }

    private float ShotStart(int shotNumber)
    {
        int i = Mathf.Clamp(shotNumber - 1, 0, shots.Count - 1);
        return starts[i];
    }

    private float ShotEnd(int shotNumber)
    {
        int i = Mathf.Clamp(shotNumber - 1, 0, shots.Count - 1);
        return starts[i] + shots[i].duration;
    }

    private void BuildTimeline()
    {
        starts = new float[shots.Count];
        float t = 0f;

        for (int i = 0; i < shots.Count; i++)
        {
            starts[i] = t;
            t += Mathf.Max(0.1f, shots[i].duration);
        }

        total = t;
        musicSource = NewSource("Musica");

        for (int i = 0; i < shots.Count; i++)
        {
            IntroShot shot = shots[i];

            if (shot.voice == null)
                continue;

            AudioSource source = NewSource("Voz_" + (i + 1));
            AudioClip clip = shot.voice;
            float volume = shot.voiceVolume;
            float at = starts[i] + shot.voiceDelay;
            voiceWindows.Add(new Vector2(at, at + clip.length));
            cues.Add(new Cue
            {
                time = at,
                action = () =>
                {
                    source.clip = clip;
                    source.volume = volume * voiceVolume;
                    source.Play();
                }
            });
        }

        foreach (IntroEffect effect in effects)
        {
            if (effect.clip == null)
            {
                Debug.LogWarning("Introduccion: el efecto '" + effect.name + "' no tiene audio.");
                continue;
            }

            FxRun run = new FxRun { data = effect, source = NewSource("SFX_" + effect.name) };
            run.start = ShotStart(effect.startShot) + effect.offset;

            if (effect.loop)
                run.stop = ShotEnd(Mathf.Max(effect.endShot, effect.startShot));

            runs.Add(run);
            cues.Add(new Cue
            {
                time = run.start,
                action = () =>
                {
                    run.source.clip = run.data.clip;
                    run.source.loop = run.data.loop;
                    run.source.volume = 0f;
                    run.source.Play();
                    run.started = true;
                }
            });
        }

        impactTime = ShotStart(impactShot) + impactOffset;
        blackoutEnd = ShotEnd(impactShot);
        logoStartTime = ShotStart(logoShot) + logoDelay;
        cues.Sort((a, b) => a.time.CompareTo(b.time));
    }

    private void WireButtons()
    {
        if (skipButton != null)
            skipButton.onClick.AddListener(Skip);

        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(() => SetPaused(false));

        if (pauseSkipButton != null)
            pauseSkipButton.onClick.AddListener(Skip);
    }

    // ------------------------------------------------------------
    // Control
    // ------------------------------------------------------------

    public void TogglePause()
    {
        SetPaused(!paused);
    }

    public void SetPaused(bool value)
    {
        if (skipping || finished || paused == value)
            return;

        paused = value;

        foreach (AudioSource source in sources)
        {
            if (source == null)
                continue;

            if (paused)
                source.Pause();
            else
                source.UnPause();
        }

        if (pausePanel != null)
            pausePanel.SetActive(paused);

        if (pauseButtonLabel != null)
            pauseButtonLabel.text = paused ? "REANUDAR" : "PAUSA";
    }

    // Omitir: fundido corto de imagen y audio y carga del mismo destino que el final normal.
    public void Skip()
    {
        if (skipping || finished)
            return;

        skipping = true;

        if (skipButton != null)
            skipButton.interactable = false;

        if (pauseButton != null)
            pauseButton.interactable = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void LoadNextScene()
    {
        if (loading)
            return;

        loading = true;
        finished = true;

        foreach (AudioSource source in sources)
            if (source != null)
                source.Stop();

        if (!GameSession.IsSceneInBuild(nextScene))
        {
            Debug.LogError("Introduccion: la escena '" + nextScene + "' no esta en Build Settings.");
            return;
        }

        SceneManager.LoadScene(nextScene);
    }

    // ------------------------------------------------------------
    // Bucle principal
    // ------------------------------------------------------------

    private void Update()
    {
        if (shots.Count == 0 || starts == null || finished)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && !skipping && (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            TogglePause();

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

        if (skipping)
        {
            skipProgress = Mathf.Clamp01(skipProgress + dt / Mathf.Max(skipFade, 0.01f));
            UpdateVisuals();

            if (!paused)
                ApplyVolumes(dt);

            if (skipProgress >= 1f)
                LoadNextScene();

            return;
        }

        if (paused)
            return;

        clock += dt;

        while (cueIndex < cues.Count && cues[cueIndex].time <= clock)
            cues[cueIndex++].action();

        // Si la musica se desfasa de la linea de tiempo (carga lenta, cambio de ventana), se reajusta.
        if (musicSource != null && musicSource.isPlaying && musicSource.clip != null &&
            clock < musicSource.clip.length - 0.1f && Mathf.Abs(musicSource.time - clock) > 0.35f)
            musicSource.time = clock;

        ApplyVolumes(dt);
        UpdateVisuals();

        if (clock >= total)
            LoadNextScene();
    }

    private int ShotIndexAt(float time)
    {
        int index = 0;

        for (int i = 0; i < starts.Length; i++)
            if (starts[i] <= time)
                index = i;

        return index;
    }

    private void ShowShot(int index)
    {
        IntroShot shot = shots[index];
        Image oldTop = layers[top];
        bool hadImage = shownShot >= 0 && oldTop != null && oldTop.enabled;

        // La imagen que estaba arriba pasa a ser el fondo del fundido.
        top = 1 - top;
        Image next = layers[top];
        shownShot = index;

        if (next == null)
            return;

        next.sprite = shot.image;
        next.enabled = shot.image != null;
        next.transform.SetAsLastSibling();

        if (fitters[top] != null && shot.image != null)
            fitters[top].aspectRatio = shot.image.rect.width / shot.image.rect.height;

        crossDuration = Mathf.Max(0f, shot.fadeIn);
        crossFade = (crossDuration <= 0f || !hadImage) ? 1f : 0f;
        SetAlpha(next, crossFade);

        if (oldTop != null)
        {
            oldTop.enabled = hadImage;

            if (hadImage)
                SetAlpha(oldTop, 1f);
        }
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    private void UpdateVisuals()
    {
        int index = ShotIndexAt(clock);

        if (index != shownShot && !skipping)
            ShowShot(index);

        Image next = layers[top];
        Image under = layers[1 - top];

        if (crossFade < 1f)
        {
            crossFade = crossDuration <= 0f ? 1f : Mathf.Clamp01(crossFade + Mathf.Min(Time.unscaledDeltaTime, 0.1f) / crossDuration);

            if (!paused || skipping)
                SetAlpha(next, Mathf.SmoothStep(0f, 1f, crossFade));

            if (crossFade >= 1f && under != null)
                under.enabled = false;
        }

        UpdateLogo();
        UpdateSubtitles();

        // Negro: apertura, cierre normal y omitir se combinan en una sola capa.
        float black = 1f - Mathf.Clamp01(clock / Mathf.Max(openFade, 0.01f));
        black = Mathf.Max(black, Mathf.Clamp01((clock - (total - endFade)) / Mathf.Max(endFade, 0.01f)));
        if (impactTime >= 0f && clock >= impactTime)
        {
            float crash = Mathf.Clamp01((clock - impactTime) / Mathf.Max(blackoutRise, 0.01f));
            crash = Mathf.Min(crash, 1f - Mathf.Clamp01((clock - blackoutEnd) / Mathf.Max(blackoutRelease, 0.01f)));
            black = Mathf.Max(black, crash);
        }

        black = Mathf.Max(black, skipProgress);
        SetAlpha(blackOverlay, black);
    }

    // Subtitulos: se calculan solo con el reloj de la introduccion (pausa y omitir los respetan).
    private string shownSubtitle;

    private void UpdateSubtitles()
    {
        if (subtitleGroup == null || subtitleText == null)
            return;

        float best = 0f;
        SubtitleCue bestCue = null;
        float fade = Mathf.Max(subtitleFade, 0.01f);

        foreach (SubtitleCue cue in subtitles)
        {
            if (cue == null || cue.shot < 1 || cue.shot > shots.Count)
                continue;

            float at = ShotStart(cue.shot) + shots[cue.shot - 1].voiceDelay;
            float s = at + cue.start;
            float e = at + cue.end;
            float inA = Mathf.Clamp01((clock - s) / fade);
            float outA = clock <= e ? 1f : 1f - Mathf.Clamp01((clock - e) / fade);
            float a = clock < s ? 0f : Mathf.Min(inA, outA);

            if (a > best)
            {
                best = a;
                bestCue = cue;
            }
        }

        string cueKey = bestCue != null ? bestCue.speaker + "|" + bestCue.text : null;

        if (bestCue != null && shownSubtitle != cueKey)
        {
            shownSubtitle = cueKey;
            bool divine = !string.IsNullOrEmpty(bestCue.speaker) && bestCue.speaker == divineSpeaker;
            subtitleText.fontStyle = divine ? FontStyles.Italic : FontStyles.Normal;
            subtitleText.text = bestCue.text;
            bool hasSpeaker = subtitleSpeaker != null && !string.IsNullOrEmpty(bestCue.speaker);
            float labelHeight = hasSpeaker ? speakerHeight : 0f;

            if (subtitleSpeaker != null)
            {
                subtitleSpeaker.gameObject.SetActive(hasSpeaker);
                subtitleSpeaker.text = bestCue.speaker;
                subtitleSpeaker.color = divine ? divineColor : narratorColor;
            }

            subtitleText.rectTransform.offsetMax = new Vector2(-subtitlePadding.x, -(subtitlePadding.y + labelHeight));
            // Si no cabe en una linea, se busca el ancho mas estrecho que siga en dos lineas: quedan equilibradas
            // y no sobra una palabra suelta.
            float oneLine = subtitleText.GetPreferredValues(bestCue.text, 100000f, 0f).y;
            float width = subtitleMaxWidth;

            if (subtitleText.GetPreferredValues(bestCue.text, subtitleMaxWidth, 0f).y > oneLine * 1.5f)
            {
                for (float candidate = subtitleMaxWidth; candidate >= 500f; candidate -= 20f)
                {
                    if (subtitleText.GetPreferredValues(bestCue.text, candidate, 0f).y > oneLine * 2.5f)
                        break;

                    width = candidate;
                }
            }

            Vector2 size = subtitleText.GetPreferredValues(bestCue.text, width, 0f);
            float w = Mathf.Min(size.x + 6f, width);

            if (hasSpeaker)
                w = Mathf.Max(w, subtitleSpeaker.GetPreferredValues(bestCue.speaker, 100000f, 0f).x + 6f);

            Vector2 box = new Vector2(w + subtitlePadding.x * 2f, size.y + labelHeight + subtitlePadding.y * 2f);

            if (subtitleBox != null)
                subtitleBox.sizeDelta = box;
        }

        // Al omitir, o al acercarse el cierre, el texto se va con el resto de la pantalla.
        float gone = 1f - Mathf.Clamp01(skipProgress * 4f);
        subtitleGroup.alpha = best * gone;
    }

    private void UpdateLogo()
    {
        if (logoImage == null || logoImage.sprite == null)
            return;

        bool visible = clock >= logoStartTime && ShotIndexAt(clock) >= logoShot - 1;

        if (logoImage.gameObject.activeSelf != visible)
            logoImage.gameObject.SetActive(visible);

        if (glow != null && glow.gameObject.activeSelf != visible)
            glow.gameObject.SetActive(visible);

        if (!visible)
            return;

        float local = clock - logoStartTime;
        float appear = Mathf.Clamp01(local / Mathf.Max(logoFadeIn, 0.01f));
        float appearEase = Mathf.SmoothStep(0f, 1f, appear);

        // Aparicion: 0.85 -> 1. Pausa: deriva casi imperceptible hasta 1.03. Salida: zoom corto durante el fundido final.
        float holdSpan = Mathf.Max(ShotEnd(logoShot) - endFade - (logoStartTime + logoFadeIn), 0.01f);
        float hold = Mathf.Clamp01((local - logoFadeIn) / holdSpan);
        float scale = Mathf.Lerp(logoStartScale, 1f, appearEase) * Mathf.Lerp(1f, logoHoldScale, Mathf.SmoothStep(0f, 1f, hold));
        float exit = Mathf.Clamp01((clock - (ShotEnd(logoShot) - endFade)) / Mathf.Max(endFade, 0.01f));
        scale = Mathf.Lerp(scale, logoExitScale, exit * exit);

        float aspect = logoImage.sprite.rect.height / logoImage.sprite.rect.width;
        RectTransform rect = logoImage.rectTransform;
        rect.sizeDelta = new Vector2(logoBaseWidth, logoBaseWidth * aspect);
        rect.localScale = new Vector3(scale, scale, 1f);
        SetAlpha(logoImage, appear);

        if (glow != null)
        {
            // Destello dorado breve detras del logo mientras aparece.
            float flash = Mathf.Clamp01(1f - Mathf.Abs(local - logoFadeIn * 0.45f) / (logoFadeIn * 0.8f));
            glow.rectTransform.localScale = new Vector3(scale * 1.4f, scale * 1.4f, 1f);
            SetAlpha(glow, flash * flash * 0.55f);
        }
    }

    private void CreateGlow()
    {
        if (logoImage == null || glow != null)
            return;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }

        tex.Apply();
        GameObject go = new GameObject("LogoDestello", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(logoImage.transform.parent, false);
        go.transform.SetSiblingIndex(logoImage.transform.GetSiblingIndex());
        glow = go.GetComponent<Image>();
        glow.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        glow.color = new Color(0.831f, 0.686f, 0.416f, 0f);
        glow.raycastTarget = false;
        RectTransform r = glow.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(logoBaseWidth, logoBaseWidth * 0.8f);
        go.SetActive(false);
    }

    // ------------------------------------------------------------
    // Audio: volumenes independientes, ducking y caida del impacto
    // ------------------------------------------------------------

    private bool VoiceActive()
    {
        foreach (Vector2 window in voiceWindows)
            if (clock >= window.x && clock < window.y)
                return true;

        return false;
    }

    private float ImpactFactor()
    {
        if (impactTime < 0f || clock < impactTime)
            return 1f;

        float t = clock - impactTime;
        float envelope;

        if (t < impactHold)
            envelope = 1f;
        else
            envelope = 1f - Mathf.Clamp01((t - impactHold) / Mathf.Max(impactRelease, 0.01f));

        return Mathf.Lerp(1f, impactDepth, Mathf.SmoothStep(0f, 1f, envelope));
    }

    private void ApplyVolumes(float dt)
    {
        float duckTarget = VoiceActive() ? 1f : 0f;
        duck = Mathf.MoveTowards(duck, duckTarget, dt / Mathf.Max(duckFade, 0.01f));
        float impact = ImpactFactor();
        float endProgress = Mathf.Clamp01((clock - (total - endFade)) / Mathf.Max(endFade, 0.01f));
        float master = (1f - skipProgress) * (1f - endProgress);

        if (musicSource != null)
            musicSource.volume = musicVolume * Mathf.Lerp(1f, musicDuck, duck) * impact * master;

        float ambientFactor = Mathf.Lerp(1f, ambientDuck, duck) * impact;

        foreach (FxRun run in runs)
        {
            if (!run.started || run.done || run.source == null)
                continue;

            float volume = run.data.volume * sfxVolume * master;

            if (run.data.ambient)
                volume *= ambientFactor;

            if (run.data.loop)
            {
                float fadeIn = Mathf.Clamp01((clock - run.start) / Mathf.Max(run.data.fadeIn, 0.01f));
                float fadeOut = 1f;

                if (run.stop >= 0f)
                    fadeOut = 1f - Mathf.Clamp01((clock - run.stop) / Mathf.Max(run.data.fadeOut, 0.01f));

                volume *= fadeIn * fadeOut;

                if (run.stop >= 0f && clock >= run.stop + run.data.fadeOut)
                {
                    run.source.Stop();
                    run.done = true;
                    continue;
                }
            }
            else if (!run.source.isPlaying && !paused)
            {
                run.done = true;
                continue;
            }

            run.source.volume = volume;
        }

        foreach (AudioSource source in sources)
        {
            if (source == null || source == musicSource || !source.isPlaying || !source.name.StartsWith("Voz_"))
                continue;

            int shotNumber;

            if (int.TryParse(source.name.Substring(4), out shotNumber) && shotNumber >= 1 && shotNumber <= shots.Count)
                source.volume = shots[shotNumber - 1].voiceVolume * voiceVolume * master;
        }
    }
}
