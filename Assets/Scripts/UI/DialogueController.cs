using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Conversacion estilo novela visual sobre el mapa. Se reproduce sola, una vez, al llegar al mapa tras la
// introduccion (GameSession.AltarDialoguePending). Controles: Espacio completa el texto que se escribe y, si ya
// esta completo, avanza; tras la ultima linea cierra. Mientras dura, IsPlaying bloquea las interacciones del mapa.
// Los textos viven en un DialogueData (ScriptableObject) y los retratos se buscan por el nombre del hablante.
public class DialogueController : MonoBehaviour
{
    [Serializable]
    public class PortraitSlot
    {
        public string speaker;
        public Image image;
    }

    // true mientras el mapa debe ignorar clics y atajos (fundido de entrada, conversacion y fundido de salida).
    public static bool IsPlaying { get; private set; }

    [SerializeField] private DialogueData dialogue;
    [SerializeField] private CanvasGroup panel;            // retratos + cuadro + placa
    [SerializeField] private CanvasGroup portraitsGroup;   // solo los retratos (se retiran al empezar el tutorial)
    [SerializeField] private Image blocker;                // captura los clics mientras dura
    [SerializeField] private Image blackOverlay;           // fundido desde negro al llegar al mapa
    [SerializeField] private GameObject namePlate;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private List<PortraitSlot> portraits = new List<PortraitSlot>();

    [Header("Escena")]
    [Tooltip("Escena que se carga al terminar la conversacion (tras fundir a negro). Vacio = no cambia de escena.")]
    [SerializeField] private string nextScene = "Map";
    [Tooltip("Si no esta vacio, al terminar se inicia este encuentro (p. ej. P1) en lugar de volver al mapa.")]
    [SerializeField] private string startEncounterId = "";
    [Tooltip("Con encuentro inicial: activa el tutorial interactivo de combate.")]
    [SerializeField] private bool combatTutorial = true;

    [Header("Evento narrativo (p. ej. Valdoria)")]
    [Tooltip("Si no esta vacio, es un nodo de la progresion: la escena solo corre si aun no esta completado, lo marca al terminar y vuelve al mapa con fundido.")]
    [SerializeField] private string eventId = "";
    [Tooltip("Aviso breve al terminar la conversacion (vacio = ninguno).")]
    [SerializeField] private string noticeMessage = "";
    [SerializeField] private CanvasGroup noticeGroup;
    [SerializeField] private TMP_Text noticeText;
    [SerializeField] private float noticeSeconds = 2.4f;

    [Header("Narracion y luz (p. ej. La mascara rota)")]
    [Tooltip("Las lineas sin hablante retiran los retratos (tutorial de la introduccion). Desactivar para que sean narracion con los personajes en pantalla.")]
    [SerializeField] private bool hidePortraitsOnEmptySpeaker = true;
    [Tooltip("Las lineas sin hablante se muestran en cursiva (narracion).")]
    [SerializeField] private bool narrationItalic = false;
    [Tooltip("Capa de luz a pantalla completa para el efecto de las lineas con 'light'.")]
    [SerializeField] private Image lightImage;
    [SerializeField] private float lightBurstAlpha = 0.55f;
    [SerializeField] private float lightRestAlpha = 0.14f;
    [Tooltip("Al terminar, libera la etapa de Noelle como hacia el evento original (la mecanica de combate no cambia).")]
    [SerializeField] private bool liberateNoelle = false;

    [Header("Retratos de Noelle y sonido (La mascara rota)")]
    [Tooltip("Hablante cuyo retrato cambia (Noelle).")]
    [SerializeField] private string maskSpeaker = "Noelle";
    [SerializeField] private Sprite crackedSprite;
    [SerializeField] private Sprite maskOffSprite;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip crackClip;
    [Tooltip("Volumen del sonido de la grieta (se multiplica por el volumen del AudioSource de efectos).")]
    [SerializeField, Range(0f, 1f)] private float crackVolume = 0.4f;
    [Tooltip("Efecto que suena una sola vez cuando empieza a aparecer el resplandor dorado (linea con luz Flash).")]
    [SerializeField] private AudioClip lightClip;
    [SerializeField, Range(0f, 1f)] private float lightSfxVolume = 0.22f;
    [Tooltip("Pausa dramatica (s) tras la grieta antes de poder avanzar.")]
    [SerializeField] private float crackPause = 1.4f;

    [Header("Musica de fondo (opcional)")]
    [SerializeField] private AudioSource musicSource;
    [Tooltip("Volumen de la musica de fondo (0-1). Suave para no tapar las voces ni el sonido de la mascara.")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.3f;
    [SerializeField] private float musicFadeSeconds = 2.5f;
    [Tooltip("Solape (s) entre el final de la pista y su reinicio: el bucle se funde sin corte.")]
    [SerializeField] private float musicLoopCrossfade = 3f;

    [Header("Sonido de escritura")]
    [Tooltip("Blip corto que suena mientras el texto aparece letra a letra. Vacio = sin sonido.")]
    [SerializeField] private AudioClip typeClip;
    [SerializeField, Range(0f, 1f)] private float typeVolume = 0.06f;
    [Tooltip("Suena una vez cada N letras visibles (los espacios y signos no cuentan ni suenan).")]
    [SerializeField, Min(1)] private int lettersPerBlip = 3;
    [Tooltip("Tiempo minimo (s) entre dos sonidos, para que no se acumulen.")]
    [SerializeField] private float blipMinInterval = 0.08f;
    [SerializeField] private float blipPitchMin = 0.95f;
    [SerializeField] private float blipPitchMax = 1.05f;

    [Header("Habilidades nuevas (al final del evento, p. ej. La mascara rota)")]
    [Tooltip("Panel que presenta las habilidades. Vacio = no se muestra nada.")]
    [SerializeField] private CanvasGroup abilitiesPanel;
    [SerializeField] private List<AbilityShowcaseRow> abilityRows = new List<AbilityShowcaseRow>();
    [Tooltip("Espera minima (s) antes de que Espacio cierre el panel, para dar tiempo a leerlo.")]
    [SerializeField] private float abilitiesMinSeconds = 1.5f;

    // Una fila del panel: los textos salen del AbilityData (asi coinciden siempre con el combate); "usage" explica para que sirve.
    [Serializable]
    public class AbilityShowcaseRow
    {
        public AbilityData ability;
        public Sprite icon;
        [TextArea(2, 3)] public string usage;
        public Image iconImage;
        public TMP_Text titleText;
        public TMP_Text bodyText;
    }

    [Header("Tiempos")]
    [Tooltip("Fundido del mapa desde negro y de la interfaz de conversacion (0,8 a 1,2 s).")]
    [SerializeField] private float fadeSeconds = 1f;
    [Tooltip("Caracteres por segundo de la escritura progresiva.")]
    [SerializeField] private float charsPerSecond = 45f;
    [SerializeField] private float dimmedBrightness = 0.45f;

    private bool running;

    private void Start()
    {
        // El mapa nunca arranca sin sesion; en pruebas directas sobre el mapa se crea una nueva (y el dialogo se muestra).
        GameSession.EnsureSession();

        if (panel != null)
            panel.alpha = 0f;

        if (blocker != null)
            blocker.raycastTarget = false;

        bool pending = string.IsNullOrEmpty(eventId)
            ? GameSession.AltarDialoguePending
            : !GameSession.IsEncounterCompleted(eventId);   // evento: solo una vez

        if (noticeGroup != null)
            noticeGroup.alpha = 0f;

        if (pending && dialogue != null && dialogue.lines.Count > 0 && !running)
        {
            running = true;
            IsPlaying = true;

            if (blackOverlay != null)
            {
                blackOverlay.gameObject.SetActive(true);
                SetOverlay(1f);
            }

            if (blocker != null)
                blocker.raycastTarget = true;

            StartCoroutine(Run());
        }
        else
        {
            // Sin dialogo pendiente (p. ej. al probar la escena suelta): no se queda en negro ni atascada.
            if (blackOverlay != null)
                blackOverlay.gameObject.SetActive(false);

            LoadNext();
        }
    }

    private void LoadNext()
    {
        if (string.IsNullOrEmpty(nextScene) || !GameSession.IsSceneInBuild(nextScene))
            return;

        // Primer combate guiado: el encuentro se prepara igual que desde el mapa (misma progresion y recompensas).
        if (!string.IsNullOrEmpty(startEncounterId) && GameSession.SetPendingEncounter(startEncounterId) && combatTutorial)
            GameSession.RequestCombatTutorial();

        SceneManager.LoadScene(nextScene);
    }

    private void OnDestroy()
    {
        IsPlaying = false;
    }

    private IEnumerator Run()
    {
        if (musicSource != null && musicSource.clip != null)
        {
            StartCoroutine(MusicLoop());
            StartCoroutine(FadeMusic(1f, musicFadeSeconds));
        }

        // 1. El mapa aparece desde negro (continuacion del cierre de la cinematica).
        yield return Fade(t => SetOverlay(1f - t));

        if (blackOverlay != null)
            blackOverlay.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(0.25f);

        // 2. Aparece la interfaz de conversacion.
        ShowSpeaker(null);
        SetLine(null, "");
        yield return Fade(t => panel.alpha = t);

        // 3. Lineas en orden.
        float inputBlockedUntil = Time.unscaledTime + 0.3f;

        bool portraitsHidden = false;

        foreach (DialogueData.Line line in dialogue.lines)
        {
            // Las lineas sin hablante son el tutorial: los personajes salen de escena y queda solo el cuadro.
            if (hidePortraitsOnEmptySpeaker && string.IsNullOrEmpty(line.speaker) && !portraitsHidden && portraitsGroup != null)
            {
                portraitsHidden = true;
                StartCoroutine(Fade(t => portraitsGroup.alpha = 1f - t));
            }

            if (line.light != DialogueLight.None && lightImage != null)
            {
                if (lightRoutine != null)
                    StopCoroutine(lightRoutine);

                lightRoutine = StartCoroutine(LightEffect(line.light));

                // El efecto 'luz' acompana al inicio del resplandor (una vez por linea, mismo AudioSource de efectos).
                if (line.light == DialogueLight.Flash && lightClip != null && sfxSource != null)
                    sfxSource.PlayOneShot(lightClip, lightSfxVolume);
            }

            RunAction(line.action);

            if (line.action == DialogueAction.MaskCrack)
                inputBlockedUntil = Mathf.Max(inputBlockedUntil, Time.unscaledTime + crackPause);

            ShowSpeaker(line.speaker);
            SetLine(line.speaker, line.text);
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;
            float shown = 0f;
            int revealed = 0;      // letras ya procesadas para el sonido
            int letterCount = 0;   // letras (no espacios ni signos) vistas en esta linea

            while (true)
            {
                bool space = Time.unscaledTime >= inputBlockedUntil && SpacePressed();

                if (shown < total)
                {
                    if (space)
                    {
                        shown = total;                       // completa la frase
                        revealed = total;                    // sin sonidos al completar de golpe
                    }
                    else
                    {
                        shown = Mathf.Min(total, shown + charsPerSecond * Time.unscaledDeltaTime);
                    }

                    int visible = Mathf.CeilToInt(shown);
                    bodyText.maxVisibleCharacters = visible;

                    // Sonido de escritura: cada N letras visibles; los espacios y la puntuacion no suenan.
                    TMP_TextInfo info = bodyText.textInfo;

                    for (; revealed < visible && revealed < info.characterCount; revealed++)
                    {
                        if (!char.IsLetterOrDigit(info.characterInfo[revealed].character))
                            continue;

                        letterCount++;

                        if (letterCount % lettersPerBlip == 0)
                            PlayBlip();
                    }
                }
                else if (space)
                {
                    break;                                   // ya estaba completa: siguiente
                }

                yield return null;
            }

            // La pulsacion que avanzo la linea sigue activa en este frame: se espera uno para que no complete
            // tambien la linea siguiente de golpe (la escritura progresiva debe verse en todas las lineas).
            yield return null;

            // El brillo dura solo mientras se muestra su linea: al avanzar se apaga rapido.
            if (line.light != DialogueLight.None && lightImage != null)
            {
                if (lightRoutine != null)
                    StopCoroutine(lightRoutine);

                lightRoutine = StartCoroutine(LightEffect(DialogueLight.FadeOut, 0.5f));
            }
        }

        // 4. La interfaz se desvanece, se presentan las habilidades nuevas (si hay panel) y la escena funde a negro.
        yield return Fade(t => panel.alpha = 1f - t);

        if (abilitiesPanel != null && abilityRows.Count > 0)
            yield return ShowAbilities();

        if (musicSource != null && musicPlaying)
            StartCoroutine(FadeMusic(0f, musicFadeSeconds));   // la musica sigue sonando durante todo el evento y termina con este fundido

        if (string.IsNullOrEmpty(eventId))
        {
            GameSession.CompleteAltarDialogue();
        }
        else
        {
            // Evento: queda registrado en la sesion (desbloquea lo siguiente de la cadena) y el mapa vuelve con fundido.
            if (liberateNoelle)
            {
                GameSession.TryLiberateNoelle();

                // La mascara rota: unica forma de obtener el Fragmento de la Mascara (una sola vez, al terminar).
                RelicRewards.GrantNoelleFragment();
            }   // marca tambien el evento; si no hay datos del equipo (prueba suelta) se completa igual abajo

            // Valdoria: el mapa revelara el Trono Sellado con una secuencia propia (una sola vez por partida).
            if (eventId == GameSession.ValdoriaEventId)
                RelicRewards.GrantValdoriaReward();   // objeto de personaje del evento (una sola vez)

            if (eventId == GameSession.ValdoriaEventId && !GameSession.IsBossDiscovered)
                GameSession.RequestBossReveal();

            GameSession.MarkEncounterCompleted(eventId);
            GameSession.RequestMapFadeIn();

            if (noticeGroup != null && noticeText != null && !string.IsNullOrEmpty(noticeMessage))
            {
                noticeText.text = noticeMessage;
                yield return Fade(t => noticeGroup.alpha = t);
                yield return new WaitForSecondsRealtime(noticeSeconds);
                yield return Fade(t => noticeGroup.alpha = 1f - t);
            }
        }

        if (blackOverlay != null)
        {
            blackOverlay.gameObject.SetActive(true);
            yield return Fade(t => SetOverlay(t));
        }

        // La musica termina su fundido antes de cambiar de escena (no se corta de golpe).
        while (musicPlaying)
            yield return null;

        IsPlaying = false;
        running = false;
        LoadNext();
    }

    // Sonidos de escritura reproducidos (solo para pruebas).
    public static int BlipsPlayed { get; private set; }

    // Varias fuentes en rotacion: cada blip usa su propia fuente y su propio tono, asi un sonido nuevo nunca
    // corta ni cambia el tono de otro que sigue sonando (eso producia la interferencia / vibrato).
    private const int BlipVoices = 3;
    private AudioSource[] blipSources;
    private int nextBlipVoice;
    private float lastBlipTime = -10f;

    // Un sonido corto por llamada; fuentes propias para no tocar la musica ni el efecto de la mascara.
    private void PlayBlip()
    {
        if (typeClip == null || !isActiveAndEnabled)
            return;

        if (Time.unscaledTime - lastBlipTime < blipMinInterval)
            return;

        if (blipSources == null)
        {
            blipSources = new AudioSource[BlipVoices];

            for (int k = 0; k < BlipVoices; k++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 0f;
                s.clip = typeClip;
                blipSources[k] = s;
            }
        }

        AudioSource voice = blipSources[nextBlipVoice];
        nextBlipVoice = (nextBlipVoice + 1) % BlipVoices;
        lastBlipTime = Time.unscaledTime;
        voice.pitch = UnityEngine.Random.Range(blipPitchMin, blipPitchMax);
        voice.volume = typeVolume;
        voice.Play();
        BlipsPlayed++;
    }

    // Se llama una sola vez por linea (el bucle de Run no repite la linea al pulsar Espacio).
    private void RunAction(DialogueAction action)
    {
        if (action == DialogueAction.None)
            return;

        Image noelle = null;

        foreach (PortraitSlot slot in portraits)
            if (slot.speaker == maskSpeaker)
                noelle = slot.image;

        if (action == DialogueAction.MaskCrack)
        {
            if (noelle != null && crackedSprite != null)
                noelle.sprite = crackedSprite;

            if (sfxSource != null && crackClip != null)
                sfxSource.PlayOneShot(crackClip, crackVolume);
        }
        else if (action == DialogueAction.MaskOff && noelle != null && maskOffSprite != null)
        {
            noelle.sprite = maskOffSprite;
        }
    }

    // Musica en bucle sin cortes: dos fuentes que se solapan (la segunda entra mientras la primera termina).
    // El fundido general (musicGain) controla la entrada al empezar y la salida al terminar el evento.
    private float musicGain;
    private bool musicPlaying;

    private IEnumerator MusicLoop()
    {
        AudioSource a = musicSource;
        AudioSource b = a.gameObject.AddComponent<AudioSource>();
        b.clip = a.clip;
        b.playOnAwake = false;
        b.spatialBlend = 0f;
        a.loop = b.loop = false;
        a.volume = b.volume = 0f;

        float length = a.clip.length;
        float cross = Mathf.Clamp(musicLoopCrossfade, 0.1f, length * 0.4f);
        musicPlaying = true;
        a.time = 0f;
        a.Play();
        float aLevel = 1f, bLevel = 0f;
        AudioSource active = a, other = b;

        while (musicPlaying)
        {
            float remaining = length - active.time;

            if (!other.isPlaying && (remaining <= cross || !active.isPlaying))
            {
                other.time = 0f;
                other.Play();
            }

            // Solape: la que termina baja y la que entra sube.
            if (other.isPlaying)
            {
                float step = Time.unscaledDeltaTime / cross;
                float outLevel = Mathf.Max(0f, (active == a ? aLevel : bLevel) - step);
                float inLevel = Mathf.Min(1f, (other == a ? aLevel : bLevel) + step);

                if (active == a) { aLevel = outLevel; bLevel = inLevel; } else { bLevel = outLevel; aLevel = inLevel; }

                if (outLevel <= 0f)
                {
                    active.Stop();
                    AudioSource tmp = active;
                    active = other;
                    other = tmp;
                }
            }
            else if (!active.isPlaying)
            {
                active.Play();
            }

            a.volume = musicVolume * musicGain * aLevel;
            b.volume = musicVolume * musicGain * bLevel;
            yield return null;
        }

        a.Stop();
        b.Stop();
    }

    // Panel de habilidades nuevas: aparece con fundido, Espacio lo cierra (tras una espera minima para leerlo).
    private IEnumerator ShowAbilities()
    {
        foreach (AbilityShowcaseRow row in abilityRows)
        {
            if (row == null)
                continue;

            if (row.iconImage != null && row.icon != null)
                row.iconImage.sprite = row.icon;

            if (row.ability == null)
                continue;

            if (row.titleText != null)
                row.titleText.text = row.ability.abilityName;

            if (row.bodyText != null)
            {
                string cooldown = row.ability.cooldown > 0
                    ? "Enfriamiento: " + row.ability.cooldown + (row.ability.cooldown == 1 ? " turno" : " turnos")
                    : "Sin enfriamiento";
                row.bodyText.text = row.ability.description + "\n<i>" + row.usage + "</i>\n<color=#B7955B>Se usa desde " +
                                    PositionsText(row.ability.usablePositions) + "  ·  " + cooldown + "</color>";
            }
        }

        abilitiesPanel.gameObject.SetActive(true);
        abilitiesPanel.alpha = 0f;
        yield return Fade(t => abilitiesPanel.alpha = t);

        float readyAt = Time.unscaledTime + abilitiesMinSeconds;

        // La pulsacion que cerro la ultima linea sigue activa en este frame: se espera uno antes de escuchar Espacio.
        yield return null;

        while (!(Time.unscaledTime >= readyAt && SpacePressed()))
            yield return null;

        yield return Fade(t => abilitiesPanel.alpha = 1f - t);
        abilitiesPanel.gameObject.SetActive(false);
    }

    // "P1-P3", "P2", "cualquier posicion"...
    private static string PositionsText(bool[] positions)
    {
        if (positions == null || positions.Length == 0)
            return "cualquier posición";

        int count = 0;

        foreach (bool p in positions)
            if (p) count++;

        if (count == 0 || count == positions.Length)
            return "cualquier posición";

        List<string> parts = new List<string>();
        int i = 0;

        while (i < positions.Length)
        {
            if (!positions[i])
            {
                i++;
                continue;
            }

            int j = i;

            while (j + 1 < positions.Length && positions[j + 1])
                j++;

            parts.Add(i == j ? "P" + (i + 1) : "P" + (i + 1) + "–P" + (j + 1));
            i = j + 1;
        }

        return string.Join(", ", parts);
    }

    private IEnumerator FadeMusic(float target, float seconds)
    {
        float from = musicGain;

        for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
        {
            musicGain = Mathf.Lerp(from, target, t / seconds);
            yield return null;
        }

        musicGain = target;

        if (target <= 0f)
            musicPlaying = false;   // MusicLoop detiene las dos fuentes
    }

    private Coroutine lightRoutine;

    // Destello dorado que se queda como resplandor suave y se apaga lentamente al final.
    private IEnumerator LightEffect(DialogueLight effect, float fadeOutSeconds = 3f)
    {
        Color c = lightImage.color;
        float start = c.a;

        if (effect == DialogueLight.Flash)
        {
            for (float t = 0f; t < 0.35f; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
            {
                SetLight(Mathf.Lerp(start, lightBurstAlpha, t / 0.35f));
                yield return null;
            }

            for (float t = 0f; t < 1.1f; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
            {
                SetLight(Mathf.Lerp(lightBurstAlpha, lightRestAlpha, Mathf.SmoothStep(0f, 1f, t / 1.1f)));
                yield return null;
            }

            SetLight(lightRestAlpha);
        }
        else
        {
            float seconds = fadeOutSeconds;

            for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
            {
                SetLight(Mathf.Lerp(start, 0f, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }

            SetLight(0f);
        }
    }

    private void SetLight(float alpha)
    {
        Color c = lightImage.color;
        c.a = alpha;
        lightImage.color = c;
    }

    private IEnumerator Fade(Action<float> apply)
    {
        float duration = Mathf.Max(fadeSeconds, 0.01f);

        for (float time = 0f; time < duration; time += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
        {
            apply(Mathf.SmoothStep(0f, 1f, time / duration));
            yield return null;
        }

        apply(1f);
    }

    private void SetOverlay(float alpha)
    {
        if (blackOverlay == null)
            return;

        Color c = blackOverlay.color;
        c.a = alpha;
        blackOverlay.color = c;
    }

    private static bool SpacePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }

    // Quien habla se ve iluminado y los demas atenuados. Los retratos no cambian de posicion. Sin hablante
    // (tutorial) o con "Dios" (sin retrato) todos quedan visibles y ligeramente atenuados.
    private void ShowSpeaker(string speaker)
    {
        foreach (PortraitSlot slot in portraits)
        {
            if (slot == null || slot.image == null)
                continue;

            bool speaking = !string.IsNullOrEmpty(speaker) &&
                            string.Equals(slot.speaker, speaker, StringComparison.OrdinalIgnoreCase);
            float v = speaking ? 1f : dimmedBrightness;
            slot.image.color = new Color(v, v, v, 1f);
        }
    }

    private void SetLine(string speaker, string text)
    {
        bool hasName = !string.IsNullOrEmpty(speaker);

        if (namePlate != null && namePlate.activeSelf != hasName)
            namePlate.SetActive(hasName);

        if (nameText != null)
            nameText.text = hasName ? speaker : "";

        if (bodyText != null)
        {
            bodyText.text = text;
            bodyText.fontStyle = (!hasName && narrationItalic) ? FontStyles.Italic : FontStyles.Normal;
        }
    }
}
