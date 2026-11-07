using UnityEngine;
using UnityEngine.EventSystems;

// Placa con el nombre de un lugar del mapa: en reposo puede estar atenuada (restAlpha) para no competir con la
// ilustracion; al pasar el cursor se muestra con total claridad y, al salir, vuelve a su atenuacion suave.
[RequireComponent(typeof(CanvasGroup))]
public class MapTagHoverFade : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Range(0f, 1f)] public float restAlpha = 1f;
    public float speed = 8f;

    private CanvasGroup group;
    private bool hovered;

    public bool Hovered => hovered;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = restAlpha;
    }

    private void OnDisable()
    {
        hovered = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        transform.SetAsLastSibling();   // por encima de las placas vecinas mientras se lee
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
    }

    private void Update()
    {
        if (group == null)
            return;

        float target = hovered ? 1f : restAlpha;
        group.alpha = Mathf.MoveTowards(group.alpha, target, speed * Time.unscaledDeltaTime);
    }
}
