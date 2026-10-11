
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
    [Min(0)] public int attack = 15;
    [Min(0)] public int defense = 5;
    [Min(1)] public int speed = 10;

    [Header("Criticos")]
    [Range(0, 100)]
    public float criticalChance = 10f;

    [Min(1f)]
    public float criticalDamage = 1.5f;

    [Header("Apariencia")]
    public Sprite characterSprite;
}
