using System;
using System.Collections.Generic;
using UnityEngine;

// Fases de un jefe (datos configurables). Cada fase se activa cuando el HP
// del jefe baja a su umbral (porcentaje del HP maximo) y no se revierte.
// La primera fase (umbral 100) es la inicial. Ver BossPhaseController.
[CreateAssetMenu(
    fileName = "NewBossPhases",
    menuName = "Reencarnacion Express/Combat/Boss Phase Data"
)]
public class BossPhaseData : ScriptableObject
{
    // Ajustes de IA de una fase. Solo los lee la extension de IA de jefes
    // (EnemyAIController.Boss); 0 = sin cambio. Son puntos que se suman a
    // la puntuacion de la IA v0.2, nunca fuerzan una habilidad concreta.
    [Serializable]
    public class AiProfile
    {
        [Tooltip("Puntos extra a las habilidades que infligen dano.")]
        public float offensiveBonus = 0f;

        [Tooltip("Puntos extra a los ataques contra un objetivo marcado por el jefe.")]
        public float markedTargetBonus = 0f;

        [Tooltip("Sesgo a favor de preparar ataques por areas cuando son utiles (suma a la ventaja del area sobre el mejor ataque individual).")]
        public float preparedAttackBias = 0f;

        [Tooltip("Puntos (normalmente negativos) a las habilidades solo defensivas sobre si mismo.")]
        public float selfSupportAdjust = 0f;
    }

    [Serializable]
    public class Phase
    {
        public string phaseName = "Fase";

        [Tooltip("Se activa cuando HP <= este porcentaje del HP maximo. La fase inicial usa 100.")]
        [Range(1, 100)] public int hpThresholdPercent = 100;

        [Tooltip("Mensaje narrativo corto al entrar en la fase (vacio en la fase inicial).")]
        [TextArea(1, 3)] public string announcement = "";

        [Tooltip("Aviso temporal grande en el HUD (vacio = no se muestra).")]
        public string bannerText = "";

        [Tooltip("Habilidades que SOLO estan disponibles durante esta fase. Deben estar tambien en las habilidades del arquetipo; fuera de esta fase la IA las ignora.")]
        public List<AbilityData> exclusiveAbilities = new List<AbilityData>();

        public AiProfile ai = new AiProfile();
    }

    public List<Phase> phases = new List<Phase>();
}
