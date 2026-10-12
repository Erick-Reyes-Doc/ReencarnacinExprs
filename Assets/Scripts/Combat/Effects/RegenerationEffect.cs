
using UnityEngine;

public class RegenerationEffect : MonoBehaviour
{
    private BattleUnit unit;

    private int healPerTurn;
    private int remainingTurns;

    public bool IsActive => remainingTurns > 0;

    private void Awake()
    {
        unit = GetComponent<BattleUnit>();
    }

    // Aplica o renueva la regeneracion.
    public void ApplyRegeneration(
        int amount,
        int duration
    )
    {
        if (unit == null || !unit.IsAlive)
            return;

        if (amount <= 0 || duration <= 0)
            return;

        healPerTurn = amount;
        remainingTurns = duration;

        Debug.Log(
            unit.Data.unitName +
            " recibe regeneracion de " +
            healPerTurn +
            " HP durante " +
            remainingTurns +
            " turnos."
        );
    }

    // Se ejecutara al comenzar el turno
    // de este personaje.
    public void ProcessTurnStart()
    {
        if (!IsActive || unit == null)
            return;

        if (!unit.IsAlive)
        {
            ClearEffect();
            return;
        }

        unit.Heal(healPerTurn);

        remainingTurns--;

        Debug.Log(
            unit.Data.unitName +
            " recupera vida por regeneracion. " +
            "Turnos restantes: " +
            remainingTurns
        );

        if (remainingTurns <= 0)
        {
            ClearEffect();
        }
    }

    public void ClearEffect()
    {
        healPerTurn = 0;
        remainingTurns = 0;
    }
}
