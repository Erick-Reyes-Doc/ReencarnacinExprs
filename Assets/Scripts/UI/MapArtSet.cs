using UnityEngine;

// Recursos visuales provisionales del mapa y sus paneles (formas, marcos,
// degradados e iconos simples generados en Assets/Art/UI/Provisional).
// Todo son sprites blancos que se tiñen con Image.color, asi que
// reemplazarlos por arte final es cambiar la referencia aqui, sin tocar la
// logica ni las escenas.
[CreateAssetMenu(
    fileName = "MapArtSet",
    menuName = "Reencarnacion Express/UI/Map Art Set"
)]
public class MapArtSet : ScriptableObject
{
    [Header("Formas")]
    public Sprite circle;
    public Sprite ring;
    public Sprite softCircle;
    public Sprite diamond;
    public Sprite hexagon;
    public Sprite triangle;

    [Header("Paneles (9-slice)")]
    public Sprite roundedRect;
    public Sprite frame;

    [Header("Lineas")]
    [Tooltip("Degradado vertical: opaco arriba, transparente abajo.")]
    public Sprite gradient;
    [Tooltip("Trazo discontinuo para caminos pendientes (Image tipo Tiled).")]
    public Sprite dash;

    [Header("Mundo ilustrado (pendiente de arte final)")]
    [Tooltip("Ilustracion base del territorio, 16:9 (recomendado 3840x2160). Si se asigna, sustituye al terreno provisional generado por codigo.")]
    public Sprite worldBase;

    [Tooltip("Capas con transparencia, de atras hacia delante (montanas, masas de bosque, caminos, puntos de interes). Mismo encuadre 16:9 que la base.")]
    public Sprite[] worldLayers;

    [Tooltip("Atmosfera por encima de todo el mundo: niebla, luz, resplandor corrupto, vineta.")]
    public Sprite[] atmosphereLayers;

    [Tooltip("Niebla que cubre el Bosque mientras esta bloqueado (cuadro completo 16:9, con alfa).")]
    public Sprite fogLockForest;

    [Tooltip("Niebla que cubre la Corrupcion mientras esta bloqueada (cuadro completo 16:9, con alfa).")]
    public Sprite fogLockCorruption;

    [Header("Iconos")]
    public Sprite lockIcon;
    public Sprite checkIcon;
    public Sprite starIcon;
    public Sprite maskIcon;
    public Sprite skullIcon;
    public Sprite potionIcon;
    public Sprite antidoteIcon;
}
