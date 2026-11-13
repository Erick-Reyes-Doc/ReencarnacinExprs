
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewUnit",
    menuName = "Reencarnacion Express/Combat/Unit Data"
)]
public class UnitData : ScriptableObject
{
    [Header("Informacion del personaje")]
    public string unitName;

    [Header("Estadisticas")]
    [Min(1)] public int maxHealth = 100;

    [Header("Daño base (cada ataque tira un valor entre minimo y maximo)")]
    [Min(0)] public int damageMin = 3;
    [Min(0)] public int damageMax = 6;

    [Header("Defensa")]
    [Tooltip("Reduccion porcentual del daño directo recibido (0-50).")]
    [Range(0, 50)] public int protectionPercent = 0;

    [Tooltip("Probabilidad de esquivar un ataque directo (%).")]
    [Range(0, 40)] public float evasionPercent = 0f;

    [Min(1)] public int speed = 5;

    [Header("Tipo de unidad")]
    [Tooltip("Los jefes son inmunes al aturdimiento.")]
    public bool isBoss = false;

    [Header("Criticos")]
    [Range(0, 100)]
    public float criticalChance = 10f;

    [Min(1f)]
    public float criticalDamage = 1.5f;

    [Header("Apariencia")]
    public Sprite characterSprite;

    [Tooltip("Recorte (en pixeles de la textura de 'characterSprite') que se usa como retrato del enemigo en el orden de turno. Vacio = recorte automatico de la parte superior.")]
    public Rect portraitRect;

    [Tooltip("Desplazamiento horizontal (unidades de mundo) cuando este enemigo queda justo detras de la casilla extra de una unidad grande; sirve para repartir el hueco entre ambos. 0 = sin cambio.")]
    public float offsetAfterWideUnit = 0f;

    [Tooltip("Enemigo grande: ocupa dos casillas de la formacion (la suya y la siguiente), como en Darkest Dungeon. No se mueve ni deja que otras unidades se coloquen en la casilla extra.")]
    public bool occupiesTwoSlots = false;

    [Tooltip("Enemigo grande: desplazamiento horizontal (unidades de mundo) respecto al punto medio de sus dos casillas. Negativo = hacia los heroes. Sirve para alinearlo con el resto de la formacion (la barra de vida lo sigue). 0 = centrado.")]
    public float wideCenterOffsetX = 0f;

    [Tooltip("Ultima frase del jefe al caer (rotulo grande). Vacio = sin frase.")]
    public string defeatLine = "";

    [Tooltip("Sonido de la derrota animada del jefe (empieza junto con su animacion de derrota). Vacio = sin sonido.")]
    public AudioClip defeatSound;

    [Tooltip("Tamano de la barra de vida sobre el campo (1 = normal). Los enemigos grandes la llevan algo mas ancha.")]
    [Range(1f, 2f)]
    public float healthBarScale = 1f;
}
