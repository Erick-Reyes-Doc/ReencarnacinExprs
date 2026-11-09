
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

    [Tooltip("Valor base. Para Debilitamiento y Vulnerabilidad es un porcentaje (20 = 20%). La Marca no usa la potencia (siempre x2 al siguiente ataque directo).")]
    [Min(0)] public int potency = 1;

    [Min(1)] public int duration = 1;

    [Range(0f, 100f)] public float chance = 100f;

    [Tooltip("Si es true, el estado se aplica al USUARIO en lugar del objetivo (p. ej. la evasion extra de Tiro Evasivo). No depende de que el golpe impacte.")]
    public bool applyToUser = false;

    [Header("Daño por turno como % del daño (solo Sangrado, Quemadura y Veneno)")]
    [Tooltip("Mayor que 0 = el daño por turno es este % del daño de la habilidad (el golpe, con critico y bonos, antes de la " +
             "proteccion del objetivo), en lugar de la Potencia fija. Si la habilidad no hace daño directo (Daño directo " +
             "desactivado) se usa una tirada del usuario con el % de daño y el Poder de la habilidad. 0 = Potencia fija.")]
    [Range(0f, 300f)] public float damageScalePercent = 0f;

    [Header("Escalado por nivel del usuario (nivel 1 = sin bono)")]
    public float potencyPerLevel = 0f;
    public float durationPerLevel = 0f;

    // true si este estado hace daño por turno proporcional al daño de la habilidad.
    public bool ScalesWithDamage =>
        damageScalePercent > 0f &&
        (type == StatusEffectType.Bleeding || type == StatusEffectType.Burn || type == StatusEffectType.Poison);

    // Daño por turno a partir del daño de referencia (minimo 1).
    public int PeriodicDamageFrom(int referenceDamage)
    {
        return Mathf.Max(1, Mathf.RoundToInt(referenceDamage * damageScalePercent / 100f));
    }

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

// Area de posiciones que una habilidad preparada puede anunciar
// (p. ej. "primera linea" = P1 y P2). Se guarda por posiciones, no por
// personajes: al resolver se consulta quien ocupa esas posiciones.
[Serializable]
public class AbilityPositionArea
{
    [Tooltip("Nombre corto para el anuncio: 'primera linea', 'retaguardia'.")]
    public string label = "";

    [Tooltip("Posiciones P1-P4 amenazadas.")]
    public bool[] positions = new bool[4];

    public bool Contains(int position)
    {
        return position >= 1 && position <= 4 &&
               positions != null && positions.Length >= position &&
               positions[position - 1];
    }

    // "P1 y P2", "P3, P4 y ..." segun las posiciones marcadas.
    public string PositionsText()
    {
        List<string> names = new List<string>();

        for (int p = 1; p <= 4; p++)
        {
            if (Contains(p))
                names.Add("P" + p);
        }

        if (names.Count <= 1)
            return string.Join("", names);

        return string.Join(", ", names.GetRange(0, names.Count - 1)) +
               " y " + names[names.Count - 1];
    }
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

    [Tooltip("Opcional. Frase que muestra el HUD cuando un enemigo usa esta habilidad (p. ej. la de un jefe). Vacio = ninguna.")]
    public string useAnnouncement = "";

    [Header("Tipo de objetivo")]
    public AbilityTargetType targetType =
    AbilityTargetType.Single;

    [Header("Valores de combate")]
    [Tooltip("Se suma al ataque antes de restar la defensa. Puede ser negativo.")]
    public int basePower = 0;

    [Tooltip("Porcentaje del daño base (la tirada min-max del atacante) que usa la habilidad: 100 = completo, 70 = el 70 %, 150 = el 150 %. Se aplica antes del Poder, del critico y de la proteccion.")]
    [Range(0, 300)] public int damagePercent = 100;

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

    [Tooltip("Primer turno propio del combate en que la habilidad ya puede usarse (1 = desde el inicio, 3 = desde el tercer turno propio / ronda 3).")]
    [Min(1)] public int availableFromTurn = 1;

    [Header("Regeneracion")]
    [Min(0)]
    public int regenerationPerTurn = 0;

    [Min(0)]
    public int regenerationDuration = 0;

    [Tooltip("Si es mayor que 0, la regeneracion cura este % de la vida maxima de CADA aliado por turno (redondeado, minimo 1) " +
             "en lugar de los PV fijos de 'Regeneration Per Turn'.")]
    [Range(0f, 100f)]
    public float regenerationPercentMaxHealth = 0f;

    // Hay regeneracion configurada (PV fijos o % de vida maxima) y dura al menos un turno.
    public bool HasRegeneration =>
        regenerationDuration > 0 &&
        (regenerationPerTurn > 0 || regenerationPercentMaxHealth > 0f);

    // PV que regenera por turno el objetivo indicado.
    public int RegenerationFor(BattleUnit target)
    {
        if (regenerationPercentMaxHealth > 0f && target != null)
            return Mathf.Max(1, Mathf.RoundToInt(target.MaxHealth * regenerationPercentMaxHealth / 100f));

        return regenerationPerTurn;
    }

    [Header("Curacion")]
    [Tooltip("Si es mayor que 0, la curacion es este % de la vida maxima del objetivo (redondeado) en lugar del Poder fijo. " +
             "Con 'Puede ser critica' puede salir critica: el % se multiplica por el daño critico de quien cura.")]
    [Range(0f, 100f)] public float healPercentMaxHealth = 0f;

    [Tooltip("Curacion fija en rango (PV). Si Maximo es mayor que 0 se usa un valor al azar entre Minimo y Maximo (tiene prioridad sobre el % y el Poder).")]
    [Min(0)] public int healMin = 0;
    [Min(0)] public int healMax = 0;

    [Tooltip("Solo curacion por %: multiplicador del % cuando la curacion es critica. 0 = usar el daño critico de quien cura (x1,5 por defecto).")]
    [Min(0f)] public float healCriticalMultiplier = 0f;

    // Curacion media sobre un objetivo (lectura para IA y pruebas).
    public int AverageHeal(BattleUnit target)
    {
        if (healMax > 0)
            return Mathf.RoundToInt((healMin + healMax) * 0.5f);

        if (healPercentMaxHealth > 0f && target != null)
            return Mathf.Max(1, Mathf.RoundToInt(target.MaxHealth * healPercentMaxHealth / 100f));

        return Mathf.Max(0, basePower);
    }

    [Header("Preparacion diferida")]
    [Tooltip("Ataques de area que se anuncian al lanzarlos y impactan en el siguiente turno propio del usuario (PreparedAbilityController). El cooldown empieza al preparar.")]
    public bool requiresPreparation = false;

    [Tooltip("Opcional. Si no esta vacia, la preparacion debe elegir una de estas areas por posiciones y solo estas se amenazan al resolver. Vacia = comportamiento original (Golpe de Meteorito).")]
    public List<AbilityPositionArea> selectableAreas = new List<AbilityPositionArea>();

    [Header("Daño directo")]
    [Tooltip("Desactivar para habilidades que solo aplican estados.")]
    public bool dealsDamage = true;

    [Tooltip("Porcentaje de la defensa efectiva del objetivo que se ignora.")]
    [Range(0f, 100f)] public float defensePenetration = 0f;

    [Tooltip("Porcentaje de la evasion del objetivo que se ignora durante este ataque (50 = la mitad).")]
    [Range(0f, 100f)] public float evasionPenetration = 0f;

    [Tooltip("Clasificacion para la evasion del objetivo.")]
    public AttackWeight attackWeight = AttackWeight.Light;

    [Tooltip("Si es true, los estados no se aplican cuando el golpe es esquivado.")]
    public bool statusesRequireHit = true;

    [Header("Bonos condicionales de dano directo")]
    [Tooltip("Puntos porcentuales extra de probabilidad critica, solo para esta habilidad.")]
    [Range(0f, 100f)] public float criticalChanceBonus = 0f;

    [Tooltip("Si el objetivo tiene este estado, el dano directo de esta habilidad se multiplica por (1 + bonus). Una sola vez por golpe; no afecta al dano periodico. None = sin bono.")]
    public StatusEffectType bonusIfTargetHas = StatusEffectType.None;

    [Tooltip("Porcentaje de dano directo extra cuando se cumple el estado anterior (20 = +20 %).")]
    [Range(0f, 200f)] public float bonusDamagePercent = 0f;

    [Tooltip("Si es true y el bono anterior se cumple en un golpe, el estado se consume (se retira del objetivo).")]
    public bool consumeBonusStatus = false;

    [Tooltip("Si es true, el movimiento del usuario es opcional: si no es posible, la habilidad se usa igualmente sin moverse.")]
    public bool movementOptional = false;

    // Multiplicador de dano directo de esta habilidad contra un objetivo (1 si no aplica).
    public float DamageMultiplierAgainst(BattleUnit target)
    {
        if (bonusIfTargetHas == StatusEffectType.None || bonusDamagePercent <= 0f || target == null)
            return 1f;

        StatusEffectController effects = target.GetComponent<StatusEffectController>();
        return effects != null && effects.HasEffect(bonusIfTargetHas)
            ? 1f + bonusDamagePercent / 100f
            : 1f;
    }

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
