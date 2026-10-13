using System.Collections.Generic;
using UnityEngine;

// Cooldowns de habilidades de una unidad. Cada unidad tiene los suyos,
// aunque comparta el mismo AbilityData (que nunca se modifica).
//
// Regla: una habilidad con cooldown N queda disponible de nuevo tras N
// turnos propios completos. Se guarda N + 1 al usarla y se descuenta 1
// al inicio de cada turno propio (incluidos los perdidos por Stun).
// Asi el turno en que se usa no cuenta, y vuelve a estar lista cuando
// el contador llega a 0 al inicio del turno N + 1 posterior al uso.
// Cooldown 0 nunca bloquea la habilidad.
public class AbilityCooldownController : MonoBehaviour
{
    private readonly Dictionary<AbilityData, int> remaining =
        new Dictionary<AbilityData, int>();

    private readonly List<AbilityData> buffer =
        new List<AbilityData>();

    public bool IsReady(AbilityData ability)
    {
        return GetRemainingTurns(ability) <= 0;
    }

    // Turnos propios que faltan para poder usarla (0 = lista). Justo
    // despues de usarla vale cooldown + 1 (el turno de uso no cuenta).
    public int GetRemainingTurns(AbilityData ability)
    {
        if (ability == null)
            return 0;

        return remaining.TryGetValue(ability, out int turns)
            ? turns
            : 0;
    }

    // Inicia el cooldown tras un uso exitoso.
    public void StartCooldown(AbilityData ability)
    {
        if (ability == null || ability.cooldown <= 0)
            return;

        remaining[ability] = ability.cooldown + 1;
    }

    // Se llama una vez al inicio de cada turno propio.
    public void TickTurnStart()
    {
        if (remaining.Count == 0)
            return;

        buffer.Clear();
        buffer.AddRange(remaining.Keys);

        foreach (AbilityData ability in buffer)
        {
            int turns = remaining[ability] - 1;

            if (turns <= 0)
                remaining.Remove(ability);
            else
                remaining[ability] = turns;
        }
    }

    // La habilidad 'to' hereda el cooldown pendiente de 'from' si es
    // mayor que el suyo (usado al cambiar el conjunto de habilidades).
    public void InheritCooldown(AbilityData from, AbilityData to)
    {
        if (from == null || to == null || from == to)
            return;

        if (!remaining.TryGetValue(from, out int turns) || turns <= 0)
            return;

        if (!remaining.TryGetValue(to, out int current) ||
            turns > current)
        {
            remaining[to] = turns;
        }
    }

    public void ResetAll()
    {
        remaining.Clear();
    }
}
