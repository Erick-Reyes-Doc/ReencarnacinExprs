
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

    public BattleUnit CurrentUnit { get; private set; }

    private void Start()
    {
        StartBattle();
    }

    public void StartBattle()
    {
        currentRound = 0;
        currentTurnIndex = 0;
        battleFinished = false;
        CurrentUnit = null;

        StartNewRound();
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

                // Procesar efectos de regeneracion
                // al comenzar el turno del personaje.
                RegenerationEffect regeneration =
                    CurrentUnit.GetComponent<RegenerationEffect>();

                if (regeneration != null && regeneration.IsActive)
                {
                    regeneration.ProcessTurnStart();
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

        if (!enemiesAlive && playersAlive)
        {
            Debug.Log("¡VICTORIA!");
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
