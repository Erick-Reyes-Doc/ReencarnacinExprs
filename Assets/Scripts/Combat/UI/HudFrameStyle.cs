using UnityEngine;
using UnityEngine.EventSystems;

// Datos de presentacion de un elemento del HUD de combate con borde fino (Outline): color del borde en
// reposo e indicacion de si el cursor esta encima. CombatHudController lo consulta para pintar los estados
// Normal / Hover / Seleccionado / Deshabilitado. No contiene logica de combate.
public class HudFrameStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Color normalBorder = new Color(0.239f, 0.290f, 0.361f, 1f);

    public bool Hovered { get; private set; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Hovered = false;
    }

    // Si el boton se oculta con el cursor encima, no queda "enganchado".
    private void OnDisable()
    {
        Hovered = false;
    }
}
