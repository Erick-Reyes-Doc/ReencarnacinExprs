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

    [Min(0f)] public float actionDelay = 0.6f;
}
