
using UnityEngine;
using UnityEngine.InputSystem;

public class MovementTest : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PositionManager positionManager;
    [SerializeField] private TurnManager turnManager;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (positionManager == null || turnManager == null)
            return;

        // Obtener el personaje que tiene el turno.
        BattleUnit activeUnit = turnManager.CurrentUnit;

        if (activeUnit == null || !activeUnit.IsAlive)
            return;

        // Solo permitir movimiento voluntario a jugadores.
        if (activeUnit.Side != TeamSide.Player)
            return;

        // Q = avanzar hacia el enemigo.
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            TryMove(activeUnit, -1);
        }
        // E = retroceder hacia la retaguardia.
        else if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryMove(activeUnit, 1);
        }
    }

    private void TryMove(BattleUnit unit, int direction)
    {
        bool moved = positionManager.MoveUnit(
            unit,
            direction
        );

        if (!moved)
        {
            Debug.Log(
                "Movimiento invalido. El turno continua."
            );
            return;
        }

        Debug.Log(
            unit.Data.unitName +
            " utilizo su turno para moverse."
        );

        // Moverse consume el turno completo.
        turnManager.EndTurn();
    }
}
