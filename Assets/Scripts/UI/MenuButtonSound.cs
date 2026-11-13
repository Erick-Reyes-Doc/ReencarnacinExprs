using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Sonidos propios de un boton del menu principal: uno al entrar el cursor (una sola vez por entrada) y otro al hacer clic.
// Si el boton lleva este componente, el sonido global de hover (UiHoverSound) se omite para no sonar doble.
// Los sonidos se reproducen desde un emisor temporal que sobrevive al cambio de escena, asi un clic largo
// (p. ej. el de Nueva partida) termina de sonar durante la transicion en vez de cortarse.
public class MenuButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerClickHandler
{
    // El boton puede cerrar su propio panel (Volver / X) en su evento de clic, dejando la interfaz no interactuable ANTES de que
    // este componente reciba el clic. Por eso se comprueba si estaba activo al PRESIONAR, no al soltar.
    private bool pressedWhileInteractable;

    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.65f;

    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();

        // Carga previa de los datos de audio para que el primer uso suene al instante.
        if (hoverClip != null) hoverClip.LoadAudioData();
        if (clickClip != null) clickClip.LoadAudioData();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Play(hoverClip, hoverVolume);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressedWhileInteractable = selectable == null || selectable.IsInteractable();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !pressedWhileInteractable)
            return;

        pressedWhileInteractable = false;
        Play(clickClip, clickVolume, false);
    }

    private void Play(AudioClip clip, float volume, bool requireInteractable = true)
    {
        if (clip == null)
            return;

        if (requireInteractable && selectable != null && !selectable.IsInteractable())
            return;

        GameObject go = new GameObject("SonidoBoton_" + clip.name);
        DontDestroyOnLoad(go);
        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.clip = clip;
        src.volume = volume;
        src.Play();
        Destroy(go, clip.length + 0.2f);
    }
}
