using UnityEngine;
using UnityEngine.EventSystems;

// Indica si el cursor esta sobre una tarjeta de producto de la tienda. MapShopController lo consulta para
// mostrar el marco de hover (o mantenerlo si la tarjeta esta seleccionada).
public class ShopCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public bool Hovered { get; private set; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Hovered = false;
    }

    // Si la tarjeta se desactiva con el cursor encima (p. ej. al cerrar la tienda), no queda "enganchado".
    private void OnDisable()
    {
        Hovered = false;
    }
}
