
using UnityEngine;
using UnityEngine.InputSystem;

public class AbilityTest : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PositionManager positionManager;
    [SerializeField] private TargetSelector targetSelector;

    [Header("Habilidad de prueba")]
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
    if (abilitySystem == null ||
        turnManager == null ||
        ability == null)
    {
        Debug.LogWarning("Faltan referencias.");
        return;
    }

    BattleUnit user = turnManager.CurrentUnit;

    if (user == null || !user.IsAlive)
        return;

    // Las pruebas manuales son para el jugador.
    if (user.Side != TeamSide.Player)
    {
        Debug.Log(
            "Las habilidades enemigas se implementaran despues."
        );
        return;
    }

    bool success = false;

    if (ability.targetType == AbilityTargetType.AllAllies)
    {
        // Una habilidad grupal no necesita
        // seleccionar un objetivo individual.
        success = abilitySystem.ExecuteGroupRegeneration(
            user,
            ability
        );
    }
    else if (ability.targetType == AbilityTargetType.Single)
    {
        if (targetSelector == null)
        {
            Debug.LogWarning("Falta TargetSelector.");
            return;
        }

        BattleUnit target = targetSelector.SelectedTarget;

        if (target == null || !target.IsAlive)
        {
            Debug.Log(
                "Selecciona un objetivo antes de usar la habilidad."
            );
            return;
        }

        success = abilitySystem.ExecuteAbility(
            user,
            target,
            ability
        );

        // Procesar derrotas de habilidades individuales.
        if (success && !target.IsAlive &&
            positionManager != null)
        {
            positionManager.HandleDefeatedUnit(target);
        }
    }
    else
    {
        Debug.LogWarning(
            "Este tipo de objetivo aun no esta implementado."
        );
        return;
    }

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

    // Una habilidad grupal consume un solo turno.
    turnManager.EndTurn();
}

}
