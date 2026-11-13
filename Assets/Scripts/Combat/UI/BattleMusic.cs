using UnityEngine;

// Musica de los combates: empieza al entrar en la escena de combate, en bucle y con una entrada suave, y baja con un fundido
// cuando el combate termina (victoria o derrota). Respeta el volumen de musica de los ajustes y el volumen general.
public class BattleMusic : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField] private TurnManager turnManager;
    [Tooltip("Segundos que tarda la musica en subir desde el silencio al empezar el combate.")]
    [SerializeField, Min(0.1f)] private float fadeInSeconds = 2.5f;
    [Tooltip("Segundos que tarda en bajar al terminar el combate.")]
    [SerializeField, Min(0.1f)] private float fadeOutSeconds = 1.5f;
    [Tooltip("Proporcion del volumen de musica de los ajustes con la que suena el combate (1 = igual que el menu). Mas de 1 la sube por encima " +
             "del menu: el tema de combate esta masterizado bajo y debe dominar sobre los efectos de las habilidades.")]
    [SerializeField, Range(0f, 2f)] private float volumeScale = 1.5f;

    private AudioSource source;
    private float elapsed;
    private float fadeOutElapsed = -1f;

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;

        if (clip != null)
            source.Play();

        if (turnManager == null)
            turnManager = FindAnyObjectByType<TurnManager>();
    }

    private void OnEnable()
    {
        if (turnManager != null)
            turnManager.OnBattleEnding += OnBattleEnding;
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.OnBattleEnding -= OnBattleEnding;
    }

    private void OnBattleEnding(bool victory)
    {
        if (fadeOutElapsed < 0f)
            fadeOutElapsed = 0f;
    }

    private void Update()
    {
        if (source == null || clip == null)
            return;

        elapsed += Time.unscaledDeltaTime;

        float target = GameAudioSettings.HasMusicVolume ? GameAudioSettings.MusicVolume : GameAudioSettings.DefaultMusicVolume;
        float t = Mathf.Clamp01(elapsed / fadeInSeconds);
        float fadeIn = t * t * (3f - 2f * t);
        float fadeOut = 1f;

        if (fadeOutElapsed >= 0f)
        {
            fadeOutElapsed += Time.unscaledDeltaTime;
            fadeOut = 1f - Mathf.Clamp01(fadeOutElapsed / fadeOutSeconds);
        }

        source.volume = Mathf.Clamp01(target * volumeScale * fadeIn * fadeOut);
    }
}
