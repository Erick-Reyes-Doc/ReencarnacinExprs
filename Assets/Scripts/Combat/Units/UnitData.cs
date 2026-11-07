
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
}
