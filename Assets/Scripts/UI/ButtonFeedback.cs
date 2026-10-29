using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Estados visuales unificados de los botones (menu principal, pausa, mapa, tienda e inventario).
//
// Problema que resuelve: unos botones cambiaban de sprite (menuNormal -> menuHover, MapBNormal -> MapBHover) y
// esos sprites no tienen el mismo marco visible (el hover del menu mide 2096 x 310 frente a 2064 x 338 del
// normal), asi que al pasar el cursor el boton parecia aplastado; otros usaban Color Tint con valores distintos
// (0,88 / 0,92 / 0,94 de partida), con lo que unos brillaban mas que otros.
//
// Ahora todos usan el sprite Normal en todos los estados y un "velo" (hijo con la misma forma) cuyo color indica
// el estado. Nunca se cambia la escala, el tamano ni el sprite:
//   Normal     = apariencia original (velo transparente)
//   Hover      = ligera luz calida sobre borde y fondo
//   Pulsado    = velo oscuro suave
//   Deshabilit = velo oscuro marcado
//
// Los botones se adaptan solos (ButtonFeedbackScanner), sin configurar nada a mano en cada uno. Los que ya
// gestionan su propio aspecto (transicion None: tarjetas, casillas y HUD de combate) no se tocan.
public static class ButtonFeedbackStyle
{
    public static readonly Color Hover = new Color(1.00f, 0.93f, 0.76f, 0.16f);
    public static readonly Color Pressed = new Color(0.00f, 0.00f, 0.00f, 0.22f);
    public static readonly Color Disabled = new Color(0.02f, 0.03f, 0.06f, 0.50f);
    public static readonly Color Clear = new Color(1f, 1f, 1f, 0f);
}

public class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private const string OverlayName = "EstadoVelo";

    private Button button;
    private Image target;
    private Image overlay;
    private bool inside;
    private bool down;

    // Prepara el boton: fija el sprite Normal, desactiva la transicion de Unity y crea el velo.
    public static bool Applies(Button candidate)
    {
        if (candidate == null || candidate.targetGraphic as Image == null)
            return false;

        // Los que ya pintan sus propios estados (None) y los nodos del mapa conservan su aspecto.
        if (candidate.transition == Selectable.Transition.None || candidate.targetGraphic.name == "Cuerpo")
            return false;

        return candidate.GetComponent<HudFrameStyle>() == null;
    }

    private static bool IsMenuFamily(Button candidate)
    {
        Sprite hover = candidate.spriteState.highlightedSprite;
        Sprite disabled = candidate.spriteState.disabledSprite;
        return (hover != null && hover.name == "menuHover") || (disabled != null && disabled.name == "menuDisable");
    }

    private void Setup()
    {
        if (overlay != null)
            return;

        button = GetComponent<Button>();
        target = button != null ? button.targetGraphic as Image : null;

        if (target == null)
            return;

        // Los botones del menu (los que cambiaban entre menuNormal / menuHover / menuDisable) usan el sprite de su
        // rol en todos los estados: principal = dorado, secundario = cafe oscuro. Asi no quedan "congelados" en el
        // sprite de otro estado si se prepararon mientras estaban ocultos o desactivados.
        ButtonStyleSet styles = ButtonStyleSet.Instance;

        if (styles != null && IsMenuFamily(button))
        {
            bool secondary = styles.IsSecondary(name);
            Sprite roleSprite = secondary ? styles.secondary : styles.primary;

            if (roleSprite != null)
            {
                target.sprite = roleSprite;

                // Cada sprite lleva sus propios bordes 9-slice: el secundario usa su escala para no deformar las esquinas.
                if (secondary && styles.secondaryMultiplier > 0f)
                {
                    target.type = Image.Type.Sliced;
                    target.pixelsPerUnitMultiplier = styles.secondaryMultiplier;
                }
            }
        }

        // Sin transicion de Unity y color original: se descarta cualquier tinte de estado que haya quedado aplicado
        // (p. ej. el de "deshabilitado", que dejaba la X de PAUSA semitransparente).
        button.transition = Selectable.Transition.None;
        target.color = Color.white;
        target.canvasRenderer.SetColor(Color.white);

        // El Sprite Swap de Unity deja un "override" (p. ej. el sprite de deshabilitado) que se sigue dibujando aunque
        // la transicion se desactive: se limpia para que se vea el sprite asignado al rol.
        target.overrideSprite = null;

        GameObject go = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();   // por debajo del texto y de los iconos del boton
        go.GetComponent<LayoutElement>().ignoreLayout = true;

        overlay = go.GetComponent<Image>();
        overlay.sprite = target.sprite;
        overlay.type = target.type;
        overlay.preserveAspect = target.preserveAspect;
        overlay.pixelsPerUnitMultiplier = target.pixelsPerUnitMultiplier;
        overlay.fillCenter = target.fillCenter;
        overlay.raycastTarget = false;
        overlay.color = ButtonFeedbackStyle.Clear;
    }

    private void OnEnable()
    {
        Setup();
    }

    private void OnDisable()
    {
        inside = false;
        down = false;

        if (overlay != null)
            overlay.color = ButtonFeedbackStyle.Clear;
    }

    private void Update()
    {
        if (overlay == null || button == null)
            return;

        // Si otro script cambia el sprite del boton, el velo conserva la misma forma.
        if (overlay.sprite != target.sprite)
            overlay.sprite = target.sprite;

        Color color;

        if (!button.IsInteractable())
            color = ButtonFeedbackStyle.Disabled;
        else if (inside && down)
            color = ButtonFeedbackStyle.Pressed;
        else if (inside)
            color = ButtonFeedbackStyle.Hover;
        else
            color = ButtonFeedbackStyle.Clear;

        if (overlay.color != color)
            overlay.color = color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        inside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        inside = false;
        down = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        down = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        down = false;
    }
}

// Recorre los botones de la escena (tambien los que se crean despues o estan ocultos) y les anade ButtonFeedback.
public class ButtonFeedbackScanner : MonoBehaviour
{
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        GameObject go = new GameObject("ButtonFeedbackScanner");
        DontDestroyOnLoad(go);
        go.AddComponent<ButtonFeedbackScanner>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan)
            return;

        nextScan = Time.unscaledTime + 0.4f;

        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (button.GetComponent<ButtonFeedback>() == null && ButtonFeedback.Applies(button))
                button.gameObject.AddComponent<ButtonFeedback>();
        }
    }
}
