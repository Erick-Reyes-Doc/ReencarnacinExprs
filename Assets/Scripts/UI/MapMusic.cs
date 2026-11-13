using UnityEngine;

// Musica de fondo del mapa: en bucle, entra con un fundido suave (no de golpe) y baja con el fundido de salida de la escena.
// Respeta el volumen de musica de los ajustes (el del menu de pausa del mapa) y el volumen general.
public class MapMusic : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [Tooltip("Segundos que tarda la musica en subir desde el silencio al entrar al mapa.")]
    [SerializeField, Min(0.1f)] private float fadeInSeconds = 4f;
    [Tooltip("Pequeña espera antes de empezar a subir, para que el mapa ya se vea cuando entre la musica.")]
    [SerializeField, Min(0f)] private float startDelay = 0.4f;

    [Tooltip("Proporcion del volumen de musica de los ajustes con la que suena el mapa (1 = igual que el menu). Menos = mas de fondo.")]
    [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;   // igual que la musica del menu

    private AudioSource source;
    private SceneFadeIn sceneFade;
    private float elapsed;

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
    }

    private void Start()
    {
        sceneFade = FindAnyObjectByType<SceneFadeIn>();
    }

    private void Update()
    {
        if (source == null || clip == null)
            return;

        elapsed += Time.unscaledDeltaTime;

        float target = GameAudioSettings.HasMusicVolume ? GameAudioSettings.MusicVolume : GameAudioSettings.DefaultMusicVolume;
        float t = Mathf.Clamp01((elapsed - startDelay) / fadeInSeconds);
        float fadeIn = t * t * (3f - 2f * t);   // curva suave
        float fadeOut = sceneFade != null ? 1f - sceneFade.LeaveProgress : 1f;

        source.volume = target * volumeScale * fadeIn * fadeOut;
    }
}
