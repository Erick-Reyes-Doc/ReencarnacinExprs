using UnityEngine;

// Visual de un HEROE CAIDO (vida 0): el sprite se tumba en el suelo, se apaga un poco y deja de ocupar su casilla de la
// formacion (PositionManager lo coloca en las casillas libres de la retaguardia). Los heroes no mueren: siguen en el combate
// sin turno ni objetivo hasta que se les cura (pocion o curacion de Noelle).
//
// PROVISIONAL: la pose es el propio sprite girado 90 grados y atenuado. Cuando exista la animacion de "caido", basta con
// sustituir Begin / Restore (por ejemplo lanzando un trigger de Animator) manteniendo PlaceAt, que solo coloca el cuerpo.
public class UnitDownedVisual : MonoBehaviour
{
    // Angulo con que se tumba (positivo = cae hacia atras: la cabeza queda a la izquierda).
    private const float LieAngle = 90f;
    private const float FallSeconds = 0.35f;
    private static readonly Color DownedTint = new Color(0.55f, 0.50f, 0.55f, 1f);

    private SpriteRenderer body;
    private Quaternion standRotation;
    private Color standColor;
    private int standOrder;
    private float groundDelta;     // base del sprite de pie respecto a su transform (para apoyarlo en el suelo al tumbarse)
    private float progress;        // 0 = de pie, 1 = tumbado
    private bool active;
    private bool hasSlot;
    private bool animated;     // true = la pose de derrota la dibuja el Animator (UnitAnimationDriver.PlayDowned): no se gira el sprite
    private Vector3 slot;

    // Empieza la caida de 'unit' (la deja tumbada donde esta hasta que se le asigne casilla con PlaceAt).
    public static void Play(BattleUnit unit)
    {
        if (unit == null)
            return;

        UnitDownedVisual visual = unit.GetComponent<UnitDownedVisual>();

        if (visual == null)
            visual = unit.gameObject.AddComponent<UnitDownedVisual>();

        visual.Begin();
    }

    // Casilla (posicion de mundo en el suelo) donde debe quedar tumbado el cuerpo.
    public static void PlaceAt(BattleUnit unit, Vector3 worldSlot)
    {
        UnitDownedVisual visual = unit != null ? unit.GetComponent<UnitDownedVisual>() : null;

        if (visual != null && visual.active)
        {
            visual.slot = worldSlot;
            visual.hasSlot = true;
        }
    }

    // Levanta al heroe: restaura rotacion, color y orden de dibujo (la posicion la fija despues PositionManager).
    public static void Stop(BattleUnit unit)
    {
        UnitDownedVisual visual = unit != null ? unit.GetComponent<UnitDownedVisual>() : null;

        if (visual != null)
            visual.Restore();
    }

    public static bool IsActive(BattleUnit unit)
    {
        UnitDownedVisual visual = unit != null ? unit.GetComponent<UnitDownedVisual>() : null;
        return visual != null && visual.active;
    }

    private void Begin()
    {
        body = GetComponent<SpriteRenderer>();

        if (body == null)
            return;

        if (!active)
        {
            standRotation = transform.rotation;
            standColor = body.color;
            standOrder = body.sortingOrder;
            groundDelta = body.bounds.min.y - transform.position.y;
            progress = 0f;
        }

        active = true;
        hasSlot = false;
        enabled = true;

        // Regla: el heroe caido no se mueve de su lugar. Si tiene animacion de caida, hace su pose de derrota (de rodillas,
        // agachado...) sin girarse ni desplazarse; si no, se mantiene el efecto anterior de tumbarse (pero en su casilla).
        UnitAnimationDriver driver = GetComponent<UnitAnimationDriver>();
        animated = driver != null && driver.PlayDowned();
    }

    private void Restore()
    {
        if (!active)
            return;

        active = false;
        hasSlot = false;

        if (animated)
        {
            UnitAnimationDriver driver = GetComponent<UnitAnimationDriver>();

            if (driver != null)
                driver.StandUp();

            animated = false;
        }

        if (body != null)
        {
            transform.rotation = standRotation;
            body.color = standColor;
            body.sortingOrder = standOrder;
        }

        progress = 0f;
    }

    private void LateUpdate()
    {
        if (!active || body == null)
            return;

        progress = Mathf.Min(1f, progress + Time.deltaTime / FallSeconds);
        float eased = progress * progress * (3f - 2f * progress);

        // Pose de derrota animada: se queda exactamente en su casilla, sin girar; solo se apaga un poco (tinte suave).
        if (animated)
        {
            Color dim = new Color(0.78f * standColor.r, 0.76f * standColor.g, 0.82f * standColor.b, standColor.a);
            body.color = Color.Lerp(standColor, dim, eased);

            if (hasSlot)
                transform.position = new Vector3(slot.x, slot.y, transform.position.z);

            return;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, LieAngle * eased) * standRotation;
        body.color = Color.Lerp(standColor, new Color(DownedTint.r * standColor.r, DownedTint.g * standColor.g, DownedTint.b * standColor.b, standColor.a), eased);
        body.sortingOrder = standOrder - 1;   // detras de los heroes en pie

        if (!hasSlot)
            return;

        // Apoya el cuerpo tumbado en su casilla: centrado en X y con su base en el suelo. Los desplazamientos de los limites
        // no dependen de la posicion, asi que se calcula el destino y se acerca suavemente.
        Vector3 center = body.bounds.center - transform.position;
        float lowest = body.bounds.min.y - transform.position.y;
        float centerX = slot.x;
        Camera cam = Camera.main;

        // El cuerpo tumbado es ancho: si la casilla queda en el borde de la pantalla, se desplaza para que no se corte.
        if (cam != null && cam.orthographic)
        {
            float left = cam.transform.position.x - cam.orthographicSize * cam.aspect;
            centerX = Mathf.Max(centerX, left + body.bounds.extents.x + 0.25f);
        }

        Vector3 target = new Vector3(centerX - center.x, slot.y + groundDelta - lowest, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
    }
}
