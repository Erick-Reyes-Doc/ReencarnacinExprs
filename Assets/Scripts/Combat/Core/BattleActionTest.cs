
using UnityEngine;
using UnityEngine.InputSystem;

public class BattleActionTest : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private BattleUnit player;
    [SerializeField] private BattleUnit enemy;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // Presionar A para atacar.
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            PerformTurnAttack();
        }
    }

    private void PerformTurnAttack()
    {
        if (turnManager == null ||
            player == null ||
            enemy == null)
        {
            Debug.LogWarning("Faltan referencias.");
            return;
        }

        BattleUnit attacker = turnManager.CurrentUnit;

        if (attacker == null || !attacker.IsAlive)
            return;

        // Elegir el objetivo contrario.
        BattleUnit target =
            attacker.Side == TeamSide.Player
            ? enemy
            : player;

        if (!target.IsAlive)
        {
            Debug.Log("El objetivo ya esta derrotado.");
            return;
        }

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

        // Detener la prueba si alguien fue derrotado.
        if (!target.IsAlive)
        {
            Debug.Log(
                attacker.Side == TeamSide.Player
                ? "¡VICTORIA!"
                : "DERROTA"
            );

            enabled = false;
            return;
        }

        // Terminar turno después de atacar.
        turnManager.EndTurn();
    }
}
