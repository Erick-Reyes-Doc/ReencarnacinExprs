using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Estado visual de un boton del menu con la paleta oficial. El Button solo
// aplica la tinta de color (normal / pulsado / deshabilitado); este componente
// anade lo que una unica transicion no cubre:
//   - color del texto: normal #E8DCC6 | hover, seleccionado o pulsado #D4AF6A | deshabilitado #555965
//   - un resplandor dorado tenue (hoverOverlay) que solo se ve con hover, seleccion o pulsacion.
[RequireComponent(typeof(Button))]
public class MenuButtonLabel : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private TMP_Text label;
    [Tooltip("Imagen hija (sin raycast) que se muestra con hover/seleccion. Opcional.")]
    [SerializeField] private Graphic hoverOverlay;
    [SerializeField] private Color normalColor = new Color32(0xE8, 0xDC, 0xC6, 0xFF);
    [SerializeField] private Color highlightedColor = new Color32(0xD4, 0xAF, 0x6A, 0xFF);
    [SerializeField] private Color disabledColor = new Color32(0x55, 0x59, 0x65, 0xFF);

    private Button button;
    private bool hovered;
    private bool selected;
    private bool pressed;
    private bool lastInteractable = true;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);
    }

    private void OnEnable()
    {
        hovered = selected = pressed = false;
        Refresh();
    }

    private void Update()
    {
        // Detecta cambios de interactable hechos por otros scripts.
        if (button != null && button.interactable != lastInteractable)
            Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; Refresh(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; Refresh(); }
    public void OnSelect(BaseEventData eventData) { selected = true; Refresh(); }
    public void OnDeselect(BaseEventData eventData) { selected = false; Refresh(); }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; Refresh(); }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; Refresh(); }

    private void Refresh()
    {
        if (button == null)
            return;

        lastInteractable = button.interactable;
        bool active = lastInteractable && (hovered || selected || pressed);

        if (label != null)
            label.color = !lastInteractable ? disabledColor : (active ? highlightedColor : normalColor);

        if (hoverOverlay != null && hoverOverlay.gameObject.activeSelf != active)
            hoverOverlay.gameObject.SetActive(active);
    }
}
