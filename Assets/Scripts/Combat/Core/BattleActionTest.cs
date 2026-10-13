
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BattleActionTest : MonoBehaviour
{
    [Header("Administradores")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PositionManager positionManager;

    [SerializeField] private TargetSelector targetSelector;

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

        BattleUnit target;

        // Los jugadores eligen manualmente su objetivo.
        if (attacker.Side == TeamSide.Player)
        {
            if (targetSelector == null)
            {
                Debug.LogWarning("Falta asignar TargetSelector.");
                return;
            }

            target = targetSelector.SelectedTarget;

            if (target == null ||
                !target.IsAlive ||
                target.Side != TeamSide.Enemy)
            {
                Debug.Log(
                    "Selecciona un enemigo valido antes de atacar."
                );
                return;
            }
        }
        else
        {
            // Los enemigos siguen seleccionando objetivos
            // automaticamente de forma provisional.
            target = GetFirstAliveUnit(playerUnits);
        }
        if (target == null)
        {
            // TurnManager comprobara el resultado.
            turnManager.EndTurn();
            return;
        }

        // Evasion opcional del objetivo (solo si tiene el componente).
        EvasionController evasion =
            target.GetComponent<EvasionController>();

        if (evasion != null && evasion.TryEvade(AttackWeight.Light))
        {
            Debug.Log(
                target.Data.unitName + " esquiva el ataque de " +
                attacker.Data.unitName + "."
            );

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

        target.TakeDirectDamage(damage);

        // La derrota se gestiona automaticamente con BattleUnit.OnDefeated.

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
