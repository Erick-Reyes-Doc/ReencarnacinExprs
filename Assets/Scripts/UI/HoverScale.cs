using UnityEngine;
using UnityEngine.EventSystems;

// Agranda un elemento de UI un poco mientras el cursor esta encima (y lo devuelve a su tamano al salir), para que
// se note que es interactivo. Se mueve con tiempo no escalado, asi que funciona tambien con el juego en pausa.
// Solo cambia la escala local: no afecta al layout ni a las dimensiones (sizeDelta) del elemento.
public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float speed = 14f;

    private Vector3 baseScale = Vector3.one;
    private bool hovered;
    private bool ready;

    public float HoverFactor
    {
        get { return hoverScale; }
        set { hoverScale = value; }
    }

    // Permite que otro elemento (p. ej. el marcador de un lugar) active el mismo crecimiento sin que el cursor este encima.
    public bool ExternalHover { get; set; }

    private void OnEnable()
    {
        if (!ready)
        {
            baseScale = transform.localScale;
            ready = true;
        }

        hovered = false;
        ExternalHover = false;
        transform.localScale = baseScale;
    }

    private void OnDisable()
    {
        hovered = false;
        ExternalHover = false;

        if (ready)
            transform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
    }

    private void Update()
    {
        Vector3 target = baseScale * (hovered || ExternalHover ? hoverScale : 1f);

        if ((transform.localScale - target).sqrMagnitude < 0.000001f)
        {
            if (transform.localScale != target)
                transform.localScale = target;

            return;
        }

        transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
    }
}
