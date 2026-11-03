using System.Collections.Generic;
using UnityEngine;

// Definicion reutilizable de un tipo de enemigo: sus estadisticas, sus
// habilidades y su estilo de IA. Sirve para configurar a los enemigos de
// un encuentro (por ejemplo, con EnemyAIController.ApplyArchetype) sin
// repetir esos datos en cada instancia.
[CreateAssetMenu(
    fileName = "NewEnemyArchetype",
    menuName = "Reencarnacion Express/Combat/Enemy Archetype"
)]
public class EnemyArchetypeData : ScriptableObject
{
    [System.Flags]
    public enum AIBehavior
    {
        None = 0,
        HealWoundedAllies = 1,   // Chaman: prioriza curar aliados con poca vida
        PunishWeakened = 2,      // Bandido: prioriza atacar objetivos con Debilitamiento
        PoisonFresh = 4,         // Lobo: prefiere envenenar a quien aun no tiene Veneno
        SelfBuffWhenHurt = 8     // Guardian Forestal: considera su mejora propia bajo el 60 % de vida
    }

    public enum Zone
    {
        Pradera,
        Bosque,
        Corrupcion,
        Jefe
    }

    [Header("Identidad")]
    public string archetypeName;
    public Zone zone;

    [Header("Estadisticas")]
    public UnitData unitData;

    [Header("Habilidades")]
    [Tooltip("Ataque basico (cooldown 0).")]
    public AbilityData basicAttack;

    [Tooltip("Habilidades especiales, en el orden de la tabla de balance.")]
    public List<AbilityData> specialAbilities = new List<AbilityData>();

    [Header("IA")]
    public EnemyAIController.AIStyle aiStyle =
        EnemyAIController.AIStyle.Meadow;

    [Tooltip("Comportamientos extra de la IA tactica (se suman a la puntuacion; nunca fuerzan una accion).")]
    public AIBehavior behaviors = AIBehavior.None;

    [Min(0f)] public float actionDelay = 0.6f;
}
