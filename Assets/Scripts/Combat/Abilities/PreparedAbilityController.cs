using UnityEngine;

// Estado de una habilidad en preparacion (por ejemplo, Golpe de
// Meteorito). Solo guarda que se prepara; AbilitySystem valida el
// lanzamiento e inicia el cooldown, y TurnManager llama a Resolve() al
// comienzo del siguiente turno propio de la unidad.
public class PreparedAbilityController : MonoBehaviour
{
    private BattleUnit unit;
    private AbilitySystem system;

    public AbilityData PreparedAbility { get; private set; }

    // Area de posiciones fijada al preparar (null = comportamiento
    // original, p. ej. Golpe de Meteorito).
    public AbilityPositionArea PreparedArea { get; private set; }

    public bool HasPreparation => PreparedAbility != null;

    private BattleUnit Unit
    {
        get
        {
            if (unit == null)
                unit = GetComponent<BattleUnit>();

            return unit;
        }
    }

    private void OnDestroy()
    {
        if (unit != null)
            unit.OnDefeated -= HandleDefeated;
    }

    // Registra la preparacion. Si la unidad cae, se cancela.
    public void Prepare(
        AbilityData ability,
        AbilitySystem abilitySystem,
        AbilityPositionArea area = null
    )
    {
        PreparedAbility = ability;
        PreparedArea = area;
        system = abilitySystem;

        Unit.OnDefeated -= HandleDefeated;
        Unit.OnDefeated += HandleDefeated;

        Debug.Log(
            Unit.Data.unitName + " prepara " + ability.abilityName +
            ": impactara en su siguiente turno."
        );
    }

    private void HandleDefeated(BattleUnit defeated)
    {
        Cancel("la unidad fue derrotada");
    }

    // Cancela la preparacion sin causar daño.
    public void Cancel(string reason)
    {
        if (!HasPreparation)
            return;

        Debug.Log(
            PreparedAbility.abilityName + " de " + Unit.Data.unitName +
            " se cancela: " + reason + "."
        );

        Clear();
    }

    public void Clear()
    {
        PreparedAbility = null;
        PreparedArea = null;
    }

    // Ejecuta el impacto. El estado se limpia antes de resolver, de
    // modo que nunca puede ejecutarse dos veces.
    public bool Resolve()
    {
        if (!HasPreparation || system == null)
        {
            Clear();
            return false;
        }

        AbilityData ability = PreparedAbility;
        AbilityPositionArea area = PreparedArea;
        AbilitySystem abilitySystem = system;

        Clear();

        return abilitySystem.ResolvePreparedAbility(Unit, ability, area);
    }
}
