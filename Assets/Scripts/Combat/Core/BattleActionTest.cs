
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BattleActionTest : MonoBehaviour
{
    [Header("Administradores")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PositionManager positionManager;

    [Header("Equipo del jugador")]
    [SerializeField]
    private List<BattleUnit> playerUnits =
        new List<BattleUnit>();

    [Header("Equipo enemigo")]
    [SerializeField]
    private List<BattleUnit> enemyUnits =
        new List<BattleUnit>();

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (turnManager == null)
            return;

        // A = ataque normal de la unidad activa.
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            PerformTurnAttack();
        }
    }

    private void PerformTurnAttack()
    {
        BattleUnit attacker = turnManager.CurrentUnit;

        // Sin unidad activa no se puede atacar.
        if (attacker == null || !attacker.IsAlive)
            return;

        // Seleccionar el equipo contrario.
        List<BattleUnit> targets =
            attacker.Side == TeamSide.Player
            ? enemyUnits
            : playerUnits;

        BattleUnit target = GetFirstAliveUnit(targets);

        if (target == null)
        {
            // TurnManager comprobara el resultado.
            turnManager.EndTurn();
            return;
        }

        // Calcular daño.
        int damage = DamageCalculator.CalculateDamage(
            attacker,
            target,
            out bool isCritical
        );

        Debug.Log(
            attacker.Data.unitName +
            " ataca a " +
            target.Data.unitName +
            (isCritical ? " ¡CRITICO!" : "")
        );

        target.TakeDamage(damage);

        // Gestionar al objetivo derrotado.

        if (!target.IsAlive && positionManager != null)
        {
            positionManager.HandleDefeatedUnit(target);
        }


        // TurnManager decide si comienza otro turno
        // o si el combate ha terminado.
        turnManager.EndTurn();
    }

    private BattleUnit GetFirstAliveUnit(
        List<BattleUnit> units
    )
    {
        foreach (BattleUnit unit in units)
        {
            if (unit != null && unit.IsAlive)
            {
                return unit;
            }
        }

        return null;
    }
}
