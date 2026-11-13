using UnityEngine;
using UnityEngine.UI;

// Cambia el sprite de un Image (iconos de la interfaz) o de un SpriteRenderer (cuerpo en combate) por 'afterEvent'
// cuando ya se completo el evento indicado (por defecto "La mascara rota": Noelle sin mascara). Se aplica cada vez que
// el objeto se activa, asi que sirve en las pantallas de Victoria, Derrota, el mapa y el combate sin tocar su logica.
public class NoelleIconSwap : MonoBehaviour
{
    [SerializeField] private Sprite afterEvent;
    [SerializeField] private string eventId = GameSession.NoelleEventId;

    [Tooltip("Solo cuerpo en combate: Animator que sustituye al de antes del evento. Si el objeto tiene Animator y esto esta vacio, " +
             "tras el evento se le quita el controlador (para que no pise el sprite sin mascara) hasta que exista su animacion.")]
    [SerializeField] private RuntimeAnimatorController afterEventController;

    private Sprite original;
    private RuntimeAnimatorController originalController;

    private void Awake()
    {
        original = Current();

        Animator animator = GetComponent<Animator>();
        originalController = animator != null ? animator.runtimeAnimatorController : null;
    }

    private Sprite Current()
    {
        Image image = GetComponent<Image>();

        if (image != null)
            return image.sprite;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        return renderer != null ? renderer.sprite : null;
    }

    private void OnEnable()
    {
        if (afterEvent == null)
            return;

        if (original == null)
            original = Current();

        bool completed = GameSession.IsEncounterCompleted(eventId);
        Sprite wanted = completed ? afterEvent : original;

        // Cuerpo animado en combate: la animacion debe corresponder al aspecto actual (con o sin mascara).
        Animator animator = GetComponent<Animator>();

        if (animator != null)
            animator.runtimeAnimatorController = completed ? afterEventController : originalController;

        Image image = GetComponent<Image>();

        if (image != null)
        {
            image.sprite = wanted;
            return;
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();

        if (renderer != null)
            renderer.sprite = wanted;
    }
}
