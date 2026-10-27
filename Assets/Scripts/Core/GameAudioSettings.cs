using UnityEngine;

// Ajustes de audio compartidos entre escenas (menu principal y menu de pausa del mapa).
// El volumen general ya es global (AudioListener.volume y sobrevive a los cambios de escena).
// El volumen de la musica vivia solo en el AudioSource del menu, asi que aqui se guarda el
// ultimo valor elegido para poder mostrarlo y respetarlo en cualquier escena.
public static class GameAudioSettings
{
    // Volumen con el que arranca la musica del menu (el del AudioSource de Menu.unity).
    public const float DefaultMusicVolume = 0.4f;

    private static float musicVolume = DefaultMusicVolume;

    public static bool HasMusicVolume { get; private set; }

    public static float MusicVolume
    {
        get => musicVolume;
        set
        {
            musicVolume = Mathf.Clamp01(value);
            HasMusicVolume = true;
        }
    }

    public static float MasterVolume
    {
        get => AudioListener.volume;
        set => AudioListener.volume = Mathf.Clamp01(value);
    }
}
