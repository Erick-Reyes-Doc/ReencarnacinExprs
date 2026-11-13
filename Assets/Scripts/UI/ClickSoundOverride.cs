using UnityEngine;

// Marca un boton para que su clic suene con un sonido propio en lugar del clic global de interfaz (UiHoverSound).
// El hover sigue siendo el global. Uso: botones de habilidad del combate (sonido de seleccion de habilidad).
public class ClickSoundOverride : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.1f;

    public AudioClip Clip => clip;
    public float Volume => volume;

    private void Awake()
    {
        // Carga previa de los datos de audio para que el primer uso suene al instante.
        if (clip != null)
            clip.LoadAudioData();
    }
}
