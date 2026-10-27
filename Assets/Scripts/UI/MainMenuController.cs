
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Panel de opciones")]
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private CanvasGroup optionsCanvasGroup;
    [SerializeField] private RectTransform optionsWindow;

    [Header("Animación")]
    [SerializeField] private float animationDuration = 0.3f;

    [Header("Audio")]
    [SerializeField] private AudioSource menuMusic;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;

    [Header("Nueva partida")]
    [Tooltip("Escena a la que entra Nueva partida (el mapa). Debe estar en Build Settings.")]
    [SerializeField] private string newGameScene = "Map";

    [Header("Transiciones")]
    [SerializeField] private float musicFadeInDuration = 2f;
    [SerializeField] private float newGameFadeDuration = 2f;
    [Tooltip("Imagen negra a pantalla completa (Canvas Overlay por encima de toda la interfaz).")]
    [SerializeField] private Image transitionOverlay;

    private Coroutine currentAnimation;

    // Volumen de musica elegido por el jugador; el fade solo escala el AudioSource, nunca este valor.
    private float musicTargetVolume;
    private float musicFadeFactor = 1f;
    private bool transitioning;

    private void Awake()
    {
        // La musica arranca en silencio (el AudioSource tiene Play On Awake) y sube con el fade in.
        if (menuMusic != null)
        {
            musicTargetVolume = menuMusic.volume;
            menuMusic.volume = 0f;
        }

        musicFadeFactor = 0f;
    }

    private void Start()
    {
        // Llegar al menu (por cualquier via) descarta la partida anterior.
        GameSession.EndSession();

        optionsCanvasGroup.alpha = 0f;
        optionsCanvasGroup.interactable = false;
        optionsCanvasGroup.blocksRaycasts = false;

        optionsWindow.localScale = Vector3.one * 0.95f;
        optionsPanel.SetActive(false);

        // Si el volumen de musica se cambio en otra escena (pausa del mapa), se respeta aqui.
        if (GameAudioSettings.HasMusicVolume)
            musicTargetVolume = GameAudioSettings.MusicVolume;

        if (transitionOverlay != null)
        {
            Color clear = transitionOverlay.color;
            clear.a = 0f;
            transitionOverlay.color = clear;
            transitionOverlay.raycastTarget = false;
        }

        masterVolumeSlider.SetValueWithoutNotify(AudioListener.volume);
        musicVolumeSlider.SetValueWithoutNotify(musicTargetVolume);

        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);

        StartCoroutine(FadeMusicIn());
    }

    // Fade in unico al abrir el menu: la musica ya esta en bucle (loop del AudioSource), asi que
    // las repeticiones no vuelven a pasar por aqui.
    private IEnumerator FadeMusicIn()
    {
        float duration = Mathf.Max(musicFadeInDuration, 0.01f);

        // El primer fotograma tras cargar la escena arrastra el tiempo de carga: el reloj de
        // 2 segundos empieza un fotograma despues.
        yield return null;

        float startTime = Time.unscaledTime;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (menuMusic == null)
                yield break;

            elapsed = Time.unscaledTime - startTime;
            musicFadeFactor = Mathf.Clamp01(elapsed / duration);
            menuMusic.volume = musicTargetVolume * musicFadeFactor;
            yield return null;
        }

        musicFadeFactor = 1f;

        if (menuMusic != null)
            menuMusic.volume = musicTargetVolume;
    }

    public void NewGame()
    {
        if (transitioning)
            return;

        // Si la escena de destino no esta en Build Settings no hay transicion: PrepareNewGame
        // registra el error igual que antes.
        if (!GameSession.IsSceneInBuild(newGameScene))
        {
            PrepareNewGame();
            return;
        }

        StartCoroutine(NewGameTransition());
    }

    // Fade out de la musica y pantalla negra sincronizados (tiempo no escalado); al terminar ambos
    // se ejecuta el flujo existente de Nueva partida.
    private IEnumerator NewGameTransition()
    {
        transitioning = true;

        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }

        // Bloquea cualquier interaccion durante la transicion.
        if (transitionOverlay != null)
            transitionOverlay.raycastTarget = true;

        float startVolume = menuMusic != null ? menuMusic.volume : 0f;
        float duration = Mathf.Max(newGameFadeDuration, 0.01f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (menuMusic != null)
                menuMusic.volume = startVolume * (1f - t);

            if (transitionOverlay != null)
            {
                Color c = transitionOverlay.color;
                c.a = t;
                transitionOverlay.color = c;
            }

            yield return null;
        }

        if (menuMusic != null)
            menuMusic.volume = 0f;

        if (transitionOverlay != null)
        {
            Color c = transitionOverlay.color;
            c.a = 1f;
            transitionOverlay.color = c;
        }

        if (!PrepareNewGame())
        {
            // No deberia ocurrir (se comprobo antes); se devuelve el menu a su estado normal.
            if (transitionOverlay != null)
            {
                Color c = transitionOverlay.color;
                c.a = 0f;
                transitionOverlay.color = c;
                transitionOverlay.raycastTarget = false;
            }

            if (menuMusic != null)
                menuMusic.volume = musicTargetVolume;

            transitioning = false;
            yield break;
        }

        SceneManager.LoadScene(newGameScene);
    }

    // Empieza una sesion nueva desde cero si la escena de destino esta en
    // Build Settings. No carga la escena (lo hace NewGame).
    public bool PrepareNewGame()
    {
        if (!GameSession.IsSceneInBuild(newGameScene))
        {
            Debug.LogError(
                "Nueva partida: la escena '" + newGameScene +
                "' no esta en Build Settings."
            );
            return false;
        }

        GameSession.StartNewGame();
        return true;
    }

    public void OpenOptions()
    {
        if (transitioning)
            return;

        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        optionsPanel.SetActive(true);

        currentAnimation = StartCoroutine(
            AnimateOptions(true)
        );
    }

    public void CloseOptions()
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        currentAnimation = StartCoroutine(
            AnimateOptions(false)
        );
    }

    private IEnumerator AnimateOptions(bool opening)
    {
        float startAlpha = optionsCanvasGroup.alpha;
        float targetAlpha = opening ? 1f : 0f;

        Vector3 startScale = optionsWindow.localScale;
        Vector3 targetScale = Vector3.one *
            (opening ? 1f : 0.95f);

        optionsCanvasGroup.interactable = false;
        optionsCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        float duration = Mathf.Max(animationDuration, 0.01f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            t = t * t * (3f - 2f * t);

            optionsCanvasGroup.alpha =
                Mathf.Lerp(startAlpha, targetAlpha, t);

            optionsWindow.localScale =
                Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        optionsCanvasGroup.alpha = targetAlpha;
        optionsWindow.localScale = targetScale;

        if (opening)
        {
            optionsCanvasGroup.interactable = true;
            optionsCanvasGroup.blocksRaycasts = true;
        }
        else
        {
            optionsCanvasGroup.blocksRaycasts = false;
            optionsPanel.SetActive(false);
        }

        currentAnimation = null;
    }

    public void SetMasterVolume(float volume)
    {
        AudioListener.volume = volume;
    }

    public void SetMusicVolume(float volume)
    {
        musicTargetVolume = volume;
        GameAudioSettings.MusicVolume = volume;

        // Durante el fade in el volumen audible es el objetivo escalado por el progreso del fade.
        if (menuMusic != null && !transitioning)
            menuMusic.volume = volume * musicFadeFactor;
    }

    public void QuitGame()
    {
        if (transitioning)
            return;

        Debug.Log("Saliendo de Reencarnación Express...");
        Application.Quit();
    }

    private void OnDestroy()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(SetMusicVolume);
    }
}
