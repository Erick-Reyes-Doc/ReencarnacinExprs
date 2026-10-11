
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    [Header("Unidades del combate")]
    [SerializeField] private List<BattleUnit> units =
        new List<BattleUnit>();

    private List<BattleUnit> turnOrder =
        new List<BattleUnit>();

    private int currentTurnIndex;
    private int currentRound;

    public BattleUnit CurrentUnit { get; private set; }

    private void Start()
    {
        StartBattle();
    }

    public void StartBattle()
    {
        currentRound = 0;
        StartNewRound();
    }

    private void StartNewRound()
    {
        currentRound++;

        // Ordenar unidades vivas por velocidad.
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
        if (currentTurnIndex >= turnOrder.Count)
        {
            StartNewRound();
            return;
        }

        CurrentUnit = turnOrder[currentTurnIndex];

        Debug.Log(
            "Turno de: " + CurrentUnit.Data.unitName
        );
    }

    public void EndTurn()
    {
        currentTurnIndex++;
        StartNextTurn();
    }
}
