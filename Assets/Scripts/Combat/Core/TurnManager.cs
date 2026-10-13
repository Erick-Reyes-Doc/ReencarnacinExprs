
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    [Header("Unidades del combate")]
    [SerializeField]
    private List<BattleUnit> units =
        new List<BattleUnit>();

    private List<BattleUnit> turnOrder =
        new List<BattleUnit>();

    private int currentTurnIndex;
    private int currentRound;

    private bool battleFinished;
    private bool experienceAwarded;

    public BattleUnit CurrentUnit { get; private set; }

    public bool BattleFinished => battleFinished;

    private void Start()
    {
        StartBattle();
    }

    public void StartBattle()
    {
        currentRound = 0;
        currentTurnIndex = 0;
        battleFinished = false;
        experienceAwarded = false;
        CurrentUnit = null;

        SubscribeToUnits();

        StartNewRound();
    }

    private void OnDestroy()
    {
        foreach (BattleUnit unit in units)
        {
            if (unit != null)
                unit.OnDefeated -= HandleUnitDefeated;
        }
    }

    // Quitar antes de agregar evita suscripciones duplicadas
    // si el encuentro se reinicia.
    private void SubscribeToUnits()
    {
        foreach (BattleUnit unit in units)
        {
            if (unit == null)
                continue;

            unit.OnDefeated -= HandleUnitDefeated;
            unit.OnDefeated += HandleUnitDefeated;

            // Un encuentro nuevo empieza sin cooldowns.
            AbilityCooldownController cooldowns =
                unit.GetComponent<AbilityCooldownController>();

            if (cooldowns != null)
                cooldowns.ResetAll();

            PreparedAbilityController prepared =
                unit.GetComponent<PreparedAbilityController>();

            if (prepared != null)
                prepared.Clear();
        }
    }

    // Tras cualquier derrota se comprueba el fin del combate.
    // No avanza el turno: eso sigue haciendolo EndTurn().
    private void HandleUnitDefeated(BattleUnit unit)
    {
        if (battleFinished)
            return;

        CheckBattleEnd();
    }

    private void StartNewRound()
    {
        if (battleFinished)
            return;

        // Comprobar si algun equipo fue derrotado.
        if (CheckBattleEnd())
            return;

        currentRound++;

        // Ordenar solamente las unidades vivas.
        turnOrder = units
            .Where(unit => unit != null && unit.IsAlive)
            .OrderByDescending(unit => unit.Speed)
            .ToList();

        currentTurnIndex = 0;

        Debug.Log("=== RONDA " + currentRound + " ===");

        foreach (BattleUnit unit in turnOrder)
        {
            Debug.Log(
                unit.Data.unitName +
                " - Velocidad: " + unit.Speed
            );
        }

        StartNextTurn();
    }

    private void StartNextTurn()
    {
        if (battleFinished)
            return;

        if (CheckBattleEnd())
            return;

        // Buscar la siguiente unidad viva.
        while (currentTurnIndex < turnOrder.Count)
        {
            BattleUnit unit = turnOrder[currentTurnIndex];


            if (unit != null && unit.IsAlive)
            {
                CurrentUnit = unit;

                Debug.Log(
                    "Turno de: " +
                    CurrentUnit.Data.unitName
                );

                // Los cooldowns avanzan en cada turno propio, incluso
                // si la unidad pierde la accion por Stun.
                AbilityCooldownController cooldowns =
                    CurrentUnit.GetComponent<AbilityCooldownController>();

                if (cooldowns != null)
                    cooldowns.TickTurnStart();

                // Daño periodico al comenzar el turno propio.
                StatusEffectController statusEffects =
                    CurrentUnit.GetComponent<StatusEffectController>();

                if (statusEffects != null)
                {
                    statusEffects.ProcessTurnStart();

                    // La muerte ya notifico a OnDefeated; si el
                    // combate termino no hay nada mas que hacer.
                    if (battleFinished)
                        return;

                    // Murio por daño periodico: pasar al siguiente.
                    if (!unit.IsAlive)
                    {
                        currentTurnIndex++;
                        StartNextTurn();
                        return;
                    }
                }

                // Procesar efectos de regeneracion
                // al comenzar el turno del personaje.
                RegenerationEffect regeneration =
                    CurrentUnit.GetComponent<RegenerationEffect>();

                if (regeneration != null && regeneration.IsActive)
                {
                    regeneration.ProcessTurnStart();
                }

                bool stunned = statusEffects != null &&
                    statusEffects.SkipsCurrentAction;

                // Habilidad preparada (ej. Meteorito): si esta aturdida
                // se cancela; si no, el impacto es la accion de este
                // turno y se pasa al siguiente turno valido.
                PreparedAbilityController prepared =
                    CurrentUnit.GetComponent<PreparedAbilityController>();

                if (prepared != null && prepared.HasPreparation)
                {
                    if (stunned)
                    {
                        prepared.Cancel("la unidad esta aturdida");
                    }
                    else
                    {
                        prepared.Resolve();

                        // La victoria pudo ocurrir durante el impacto.
                        if (battleFinished)
                            return;

                        currentTurnIndex++;
                        StartNextTurn();
                        return;
                    }
                }

                // Aturdida: pierde la accion y pasa al siguiente
                // turno valido. Se comprueba al final para que el
                // daño periodico y la regeneracion ya se hayan aplicado.
                if (stunned)
                {
                    Debug.Log(
                        CurrentUnit.Data.unitName +
                        " esta aturdido y pierde su accion."
                    );

                    currentTurnIndex++;
                    StartNextTurn();
                }

                return;
            }


            // Saltar unidad derrotada.
            currentTurnIndex++;
        }

        // Si terminaron los turnos, iniciar otra ronda.
        StartNewRound();
    }

    public void EndTurn()
    {
        if (battleFinished)
            return;

        currentTurnIndex++;
        StartNextTurn();
    }

    // Suma la XP de los enemigos originales del encuentro
    // y la entrega completa a cada protagonista con progresion,
    // aunque haya caido. Solo se entrega una vez por combate.
    private void AwardVictoryExperience()
    {
        if (experienceAwarded)
            return;

        experienceAwarded = true;

        int totalExperience = 0;

        foreach (BattleUnit unit in units)
        {
            if (unit == null || unit.Side != TeamSide.Enemy)
                continue;

            EnemyExperienceReward reward =
                unit.GetComponent<EnemyExperienceReward>();

            if (reward != null)
                totalExperience += reward.ExperienceReward;
        }

        if (totalExperience <= 0)
            return;

        Debug.Log("Experiencia de victoria: " + totalExperience);

        foreach (BattleUnit unit in units)
        {
            if (unit == null || unit.Side != TeamSide.Player)
                continue;

            CharacterProgression progression =
                unit.GetComponent<CharacterProgression>();

            if (progression != null)
                progression.GainExperience(totalExperience);
        }
    }

    private bool CheckBattleEnd()
    {
        bool playersAlive = units.Any(unit =>
            unit != null &&
            unit.Side == TeamSide.Player &&
            unit.IsAlive
        );

        bool enemiesAlive = units.Any(unit =>
            unit != null &&
            unit.Side == TeamSide.Enemy &&
            unit.IsAlive
        );

        if (playersAlive && enemiesAlive)
            return false;

        battleFinished = true;
        CurrentUnit = null;

        // Las habilidades en preparacion no sobreviven al combate.
        foreach (BattleUnit unit in units)
        {
            if (unit == null)
                continue;

            PreparedAbilityController prepared =
                unit.GetComponent<PreparedAbilityController>();

            if (prepared != null)
                prepared.Cancel("el combate termino");
        }

        if (!enemiesAlive && playersAlive)
        {
            Debug.Log("¡VICTORIA!");
            AwardVictoryExperience();
        }
        else if (!playersAlive && enemiesAlive)
        {
            Debug.Log("DERROTA");
        }
        else
        {
            Debug.Log("Combate finalizado sin ganador.");
        }

        return true;
    }
}
