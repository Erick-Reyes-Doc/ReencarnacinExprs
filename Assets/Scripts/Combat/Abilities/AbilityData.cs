
using System;
using System.Collections.Generic;
using UnityEngine;

// Estado alterado que una habilidad intenta aplicar al impactar.
// La Regeneracion se sigue configurando con los campos de
// regeneracion de AbilityData (RegenerationEffect).
[Serializable]
public class AbilityStatusEffect
{
    public StatusEffectType type = StatusEffectType.Bleeding;

    [Tooltip("Valor base. Para Marca, Debilitamiento y Vulnerabilidad es un porcentaje (20 = 20%).")]
    [Min(0)] public int potency = 1;

    [Min(1)] public int duration = 1;

    [Range(0f, 100f)] public float chance = 100f;

    [Header("Escalado por nivel del usuario (nivel 1 = sin bono)")]
    public float potencyPerLevel = 0f;
    public float durationPerLevel = 0f;

    public int GetPotency(int userLevel)
    {
        int bonus = Mathf.RoundToInt(
            potencyPerLevel * Mathf.Max(0, userLevel - 1)
        );

        return Mathf.Max(0, potency + bonus);
    }

    public int GetDuration(int userLevel)
    {
        int bonus = Mathf.RoundToInt(
            durationPerLevel * Mathf.Max(0, userLevel - 1)
        );

        return Mathf.Max(1, duration + bonus);
    }
}

// Movimiento del usuario durante una habilidad.
public enum UserMovement
{
    None,
    Forward,   // hacia el frente (posicion -1)
    Backward   // hacia la retaguardia (posicion +1)
}

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
    AllEnemies,
    Self        // siempre el propio usuario
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
    [Tooltip("Se suma al ataque antes de restar la defensa. Puede ser negativo.")]
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

    [Header("Curacion")]
    [Tooltip("Si es mayor que 0, la curacion es este % de la vida maxima del objetivo (redondeado) en lugar del Poder fijo.")]
    [Range(0f, 100f)] public float healPercentMaxHealth = 0f;

    [Header("Preparacion diferida")]
    [Tooltip("Ataques de area que se anuncian al lanzarlos y impactan en el siguiente turno propio del usuario (PreparedAbilityController). El cooldown empieza al preparar.")]
    public bool requiresPreparation = false;

    [Header("Daño directo")]
    [Tooltip("Desactivar para habilidades que solo aplican estados.")]
    public bool dealsDamage = true;

    [Tooltip("Porcentaje de la defensa efectiva del objetivo que se ignora.")]
    [Range(0f, 100f)] public float defensePenetration = 0f;

    [Tooltip("Clasificacion para la evasion del objetivo.")]
    public AttackWeight attackWeight = AttackWeight.Light;

    [Tooltip("Si es true, los estados no se aplican cuando el golpe es esquivado.")]
    public bool statusesRequireHit = true;

    [Header("Movimiento del usuario")]
    [Tooltip("Mueve al usuario una posicion tras la accion (una sola accion).")]
    public UserMovement userMovement = UserMovement.None;

    [Header("Proteccion (Guard)")]
    [Tooltip("Si la habilidad aplica Guard a un aliado distinto del usuario, % de su daño directo que intercepta el usuario.")]
    [Range(0, 100)] public int allyProtectionPercent = 0;

    [Header("Estados alterados")]
    public List<AbilityStatusEffect> statusEffects =
        new List<AbilityStatusEffect>();


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
