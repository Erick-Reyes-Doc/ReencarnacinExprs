
using UnityEngine;

// Tipos generales de habilidades.
public enum AbilityType
{
    Attack,
    Heal,
    Support
}

// Efectos de desplazamiento.
public enum MovementEffect
{
    None,
    Push,
    Pull
}

public enum AbilityTargetType
{
    Single,
    AllAllies,
    AllEnemies
}

[CreateAssetMenu(
    fileName = "NewAbility",
    menuName = "Reencarnacion Express/Combat/Ability Data"
)]
public class AbilityData : ScriptableObject
{
    [Header("Informacion")]
    public string abilityName;

    [TextArea(2, 4)]
    public string description;

    public Sprite abilityIcon;

    [Header("Tipo de habilidad")]
    public AbilityType abilityType;

    [Header("Tipo de objetivo")]
    public AbilityTargetType targetType =
    AbilityTargetType.Single;

    [Header("Valores de combate")]
    [Min(0)]
    public int basePower = 0;

    public bool canCritical = true;

    [Header("Posiciones desde donde puede usarse")]
    public bool[] usablePositions = new bool[4]
    {
        true, true, true, true
    };

    [Header("Posiciones que puede seleccionar")]
    public bool[] targetPositions = new bool[4]
    {
        true, true, true, true
    };

    [Header("Desplazamiento")]
    public MovementEffect movementEffect =
        MovementEffect.None;

    [Range(0, 3)]
    public int movementDistance = 0;

    [Range(0f, 100f)]
    public float movementChance = 100f;

    [Header("Tiempo de reutilizacion")]
    [Min(0)]
    public int cooldown = 0;

    [Header("Regeneracion")]
    [Min(0)]
    public int regenerationPerTurn = 0;

    [Min(0)]
    public int regenerationDuration = 0;


    // Comprueba desde que posicion se puede usar.
    public bool CanUseFromPosition(int position)
    {
        if (position < 1 || position > 4)
            return false;

        return usablePositions != null &&
               usablePositions.Length >= position &&
               usablePositions[position - 1];
    }

    // Comprueba a que posicion puede apuntar.
    public bool CanTargetPosition(int position)
    {
        if (position < 1 || position > 4)
            return false;

        return targetPositions != null &&
               targetPositions.Length >= position &&
               targetPositions[position - 1];
    }
}
