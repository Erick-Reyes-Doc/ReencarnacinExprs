using System;
using System.Collections.Generic;
using UnityEngine;

// Definicion de un encuentro: que enemigos aparecen, en que posicion y
// cuanta experiencia se entrega al ganar.
[CreateAssetMenu(
    fileName = "NewEncounter",
    menuName = "Reencarnacion Express/Combat/Encounter"
)]
public class EncounterData : ScriptableObject
{
    [Serializable]
    public class EnemySlot
    {
        [Tooltip("Prefab del enemigo (BattleUnit + EnemyAIController).")]
        public GameObject prefab;

        [Tooltip("Posicion enemiga E1-E4.")]
        [Range(1, 4)] public int position = 1;

        // Arquetipo configurado en el prefab (para validacion y consulta).
        public EnemyArchetypeData Archetype
        {
            get
            {
                if (prefab == null)
                    return null;

                EnemyAIController ai = prefab.GetComponent<EnemyAIController>();
                return ai != null ? ai.Archetype : null;
            }
        }
    }

    [Header("Identificacion")]
    public string encounterId;
    public string displayName;
    public EnemyArchetypeData.Zone zone;

    [Tooltip("Encuentro obligatorio de la historia.")]
    public bool mandatory = true;

    [Header("Enemigos")]
    public List<EnemySlot> enemies = new List<EnemySlot>();

    [Header("Recompensa")]
    [Tooltip("XP total del encuentro, entregada una sola vez al ganar.")]
    [Min(0)] public int experienceReward = 50;

    // Comprueba que el encuentro puede cargarse: 1-4 enemigos con
    // prefab, posiciones E1-E4 sin repetir. Devuelve el motivo si falla.
    public bool Validate(out string error)
    {
        error = "";

        if (enemies == null || enemies.Count == 0 || enemies.Count > 4)
        {
            error = "El encuentro debe tener entre 1 y 4 enemigos.";
            return false;
        }

        HashSet<int> used = new HashSet<int>();

        foreach (EnemySlot slot in enemies)
        {
            if (slot == null || slot.prefab == null ||
                slot.prefab.GetComponent<BattleUnit>() == null)
            {
                error = "Hay un enemigo sin prefab valido.";
                return false;
            }

            if (slot.position < 1 || slot.position > 4)
            {
                error = "Posicion fuera de E1-E4.";
                return false;
            }

            if (!used.Add(slot.position))
            {
                error = "Dos enemigos en la posicion E" + slot.position + ".";
                return false;
            }
        }

        return true;
    }
}
