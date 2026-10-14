using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Avisa cuando el cursor entra o sale de un elemento de UI (marcadores del
// mapa). Los delegados se conectan en tiempo de ejecucion.
public class MapHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action entered;
    public Action exited;

    public void OnPointerEnter(PointerEventData eventData)
    {
        entered?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        exited?.Invoke();
    }
}
