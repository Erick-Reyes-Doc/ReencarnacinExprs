
using UnityEngine;
using UnityEngine.InputSystem;

public class TargetSelector : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PositionManager positionManager;

    public BattleUnit SelectedTarget { get; private set; }

    // false = enemigos, true = aliados.
    public bool SelectingAllies { get; private set; }

    private BattleUnit previousUnit;

    private void Update()
    {
        if (turnManager == null || positionManager == null)
            return;

        BattleUnit currentUnit = turnManager.CurrentUnit;

        // Reiniciar la seleccion al cambiar de turno.
        if (currentUnit != previousUnit)
        {
            ClearSelection();
            SelectingAllies = false;
            previousUnit = currentUnit;
        }

        if (Keyboard.current == null)
            return;

        // La seleccion manual pertenece al jugador.
        if (currentUnit == null ||
            currentUnit.Side != TeamSide.Player ||
            !currentUnit.IsAlive)
        {
            return;
        }

        // F = cambiar entre aliados y enemigos.
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            SelectingAllies = !SelectingAllies;
            ClearSelection();

            Debug.Log(
                SelectingAllies
                    ? "Modo de seleccion: ALIADOS"
                    : "Modo de seleccion: ENEMIGOS"
            );
        }

        int selectedPosition = 0;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            selectedPosition = 1;
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            selectedPosition = 2;
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            selectedPosition = 3;
        else if (Keyboard.current.digit4Key.wasPressedThisFrame)
            selectedPosition = 4;

        if (selectedPosition != 0)
        {
            SelectTarget(selectedPosition);
        }
    }

    public void SelectTarget(int position)
    {
        if (turnManager == null || positionManager == null)
            return;

        BattleUnit currentUnit = turnManager.CurrentUnit;

        if (currentUnit == null || !currentUnit.IsAlive)
            return;

        if (currentUnit.Side != TeamSide.Player)
            return;

        TeamSide targetSide = SelectingAllies
            ? TeamSide.Player
            : TeamSide.Enemy;

        BattleUnit target =
            positionManager.GetUnitAtPosition(
                targetSide,
                position
            );

        if (target == null)
        {
            ClearSelection();

            Debug.Log(
                "No hay un objetivo disponible en la posicion " +
                position
            );

            return;
        }

        SelectedTarget = target;

        Debug.Log(
            "Objetivo seleccionado: " +
            target.Data.unitName +
            " - Posicion " +
            target.Position
        );
    }

    public void ClearSelection()
    {
        SelectedTarget = null;
    }
}
