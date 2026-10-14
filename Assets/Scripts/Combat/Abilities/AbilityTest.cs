
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

    [Header("Depuracion")]
    [Tooltip("Y = transformar a la unidad activa. Desactivada: en el flujo normal la transformacion de Noelle ocurre solo con el evento del mapa.")]
    [SerializeField] private bool allowDebugTransform = false;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // T = utilizar habilidad de prueba.
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            TestAbility();
        }

        // H = elegir la siguiente habilidad del conjunto de la unidad
        // activa (solo en ejecucion; no modifica la escena).
        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            CycleLoadoutAbility();
        }

        // Y = transformar el conjunto de habilidades de la unidad activa
        // (prueba manual de la transformacion de Noelle).
        if (allowDebugTransform && Debug.isDebugBuild && Keyboard.current.yKey.wasPressedThisFrame)
        {
            UnlockActiveUnitForm();
        }
    }

    private int loadoutIndex = -1;

    private void CycleLoadoutAbility()
    {
        BattleUnit user = turnManager != null
            ? turnManager.CurrentUnit
            : null;

        CharacterAbilityLoadout loadout = user != null
            ? user.GetComponent<CharacterAbilityLoadout>()
            : null;

        if (loadout == null || loadout.Abilities.Count == 0)
        {
            Debug.Log("La unidad activa no tiene conjunto de habilidades.");
            return;
        }

        loadoutIndex = (loadoutIndex + 1) % loadout.Abilities.Count;
        ability = loadout.Abilities[loadoutIndex];

        AbilityCooldownController cooldowns =
            user.GetComponent<AbilityCooldownController>();

        int remaining = cooldowns != null
            ? cooldowns.GetRemainingTurns(ability)
            : 0;

        Debug.Log(
            "[" + user.Data.unitName + "] Habilidad " +
            (loadoutIndex + 1) + "/" + loadout.Abilities.Count + ": " +
            ability.abilityName +
            " | cooldown restante: " + remaining +
            (remaining > 0 ? " (bloqueada)" : " (lista)") +
            " | desde su posicion actual: " +
            (ability.CanUseFromPosition(user.Position) ? "si" : "no") +
            " | objetivos validos: " + DescribeTargets(user, ability)
        );
    }

    // Texto con los objetivos validos de una habilidad.
    private string DescribeTargets(BattleUnit user, AbilityData data)
    {
        if (data.targetType == AbilityTargetType.Self)
            return "el propio usuario";

        if (positionManager == null)
            return "(sin PositionManager)";

        bool targetsEnemies =
            data.abilityType == AbilityType.Attack;

        TeamSide side = targetsEnemies
            ? (user.Side == TeamSide.Player
                ? TeamSide.Enemy
                : TeamSide.Player)
            : user.Side;

        string text = "";

        for (int position = 1; position <= 4; position++)
        {
            if (!data.CanTargetPosition(position))
                continue;

            BattleUnit unit =
                positionManager.GetUnitAtPosition(side, position);

            if (unit != null && unit.IsAlive)
                text += unit.Data.unitName + "(P" + position + ") ";
        }

        if (text == "")
            text = "ninguno";

        string mode =
            data.targetType == AbilityTargetType.AllEnemies ||
            data.targetType == AbilityTargetType.AllAllies
                ? "todos: "
                : "elegir uno con 1-4" +
                  (targetsEnemies ? "" : " tras pulsar F") + ": ";

        return mode + text;
    }

    private void UnlockActiveUnitForm()
    {
        BattleUnit user = turnManager != null
            ? turnManager.CurrentUnit
            : null;

        CharacterAbilityLoadout loadout = user != null
            ? user.GetComponent<CharacterAbilityLoadout>()
            : null;

        // Solo una unidad enmascarada con conjunto alternativo
        // (Noelle) puede transformarse con esta tecla.
        if (loadout == null || !loadout.CanTransform)
        {
            Debug.Log(
                "Y solo transforma a Noelle enmascarada cuando es la " +
                "unidad activa."
            );
            return;
        }

        if (loadout.UnlockTransformedForm())
        {
            loadoutIndex = -1;
            ability = null;
            Debug.Log("Pulsa H para ver las nuevas habilidades.");
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

        // La derrota se gestiona automaticamente con BattleUnit.OnDefeated.
    }
    else if (ability.targetType == AbilityTargetType.Self)
    {
        // Habilidad sobre uno mismo: no necesita seleccionar objetivo.
        success = abilitySystem.ExecuteAbility(
            user,
            user,
            ability
        );
    }
    else if (ability.targetType == AbilityTargetType.AllEnemies)
    {
        // Ataque de area: no necesita seleccionar objetivo.
        success = abilitySystem.ExecuteAreaAbility(
            user,
            ability
        );
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
