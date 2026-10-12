
using UnityEngine;
using UnityEngine.InputSystem;

public class AbilityTest : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PositionManager positionManager;
    [SerializeField] private BattleUnit target;
    [SerializeField] private AbilityData ability;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // T = utilizar habilidad de prueba.
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            TestAbility();
        }
    }

    private void TestAbility()
    {
        if (abilitySystem == null || turnManager == null)
        {
            Debug.LogWarning("Faltan referencias.");
            return;
        }

        BattleUnit user = turnManager.CurrentUnit;

        if (user == null)
            return;

        bool success = abilitySystem.ExecuteAbility(
            user,
            target,
            ability
        );

        if (!success)
        {
            Debug.Log(
                "Habilidad fallida. El turno continua."
            );
            return;
        }

        Debug.Log(
            user.Data.unitName +
            " utilizo su turno para una habilidad."
        );


        if (target != null &&
            !target.IsAlive &&
            positionManager != null)
        {
            positionManager.HandleDefeatedUnit(target);
        }


        // TurnManager comprueba si el combate termino
        // antes de comenzar el siguiente turno.
        turnManager.EndTurn();
    }
}
