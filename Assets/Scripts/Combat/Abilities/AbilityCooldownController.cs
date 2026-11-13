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

    // Los enemigos nunca esperan mas de 2 turnos para repetir una habilidad (las de los heroes no cambian).
    public const int MaxEnemyCooldown = 2;

    public static int EffectiveCooldown(BattleUnit unit, AbilityData ability)
    {
        if (ability == null)
            return 0;

        return unit != null && unit.Side == TeamSide.Enemy
            ? Mathf.Min(ability.cooldown, MaxEnemyCooldown)
            : ability.cooldown;
    }

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

        int turns = remaining.TryGetValue(ability, out int t) ? t : 0;

        // Disponibilidad inicial: no se puede usar antes de la RONDA N del combate (la ronda que lleva el TurnManager,
        // no los turnos propios de la unidad). Fuera de un combate en curso se cuentan los turnos propios.
        // Solo las habilidades con availableFromTurn > 1 tienen bloqueo inicial; el resto nunca.
        int reached = CombatRound > 0 ? CombatRound : ownTurnsStarted;
        int initial = ability.availableFromTurn > 1
            ? Mathf.Max(0, ability.availableFromTurn - reached)
            : 0;

        return Mathf.Max(turns, initial);
    }

    // Turnos propios iniciados en este combate (solo respaldo si no hay ronda de combate en curso).
    private int ownTurnsStarted = 0;

    // Ronda actual del combate (la fija TurnManager al empezar el combate y cada ronda; 0 = sin combate en curso).
    public static int CombatRound { get; set; }

    // Usos de cada habilidad en el combate actual (para AbilityData.maxUsesPerCombat).
    private readonly Dictionary<AbilityData, int> uses = new Dictionary<AbilityData, int>();

    public int GetUses(AbilityData ability)
    {
        return ability != null && uses.TryGetValue(ability, out int n) ? n : 0;
    }

    // true si la habilidad ya llego a su limite de usos por combate.
    public bool UsesExhausted(AbilityData ability)
    {
        return ability != null && ability.maxUsesPerCombat > 0 && GetUses(ability) >= ability.maxUsesPerCombat;
    }

    // Inicia el cooldown tras un uso exitoso.
    public void StartCooldown(AbilityData ability)
    {
        if (ability != null)
            uses[ability] = GetUses(ability) + 1;

        if (ability == null || ability.cooldown <= 0)
            return;

        BattleUnit owner = GetComponent<BattleUnit>();
        remaining[ability] = EffectiveCooldown(owner, ability) + 1;

        // Reloj de Arena Roto: la primera habilidad del combate con enfriamiento >= 2 lo reduce en 1.
        int reduction = RelicEffects.CooldownReduction(owner, ability);

        if (reduction > 0)
            remaining[ability] = Mathf.Max(1, remaining[ability] - reduction);
    }

    // Se llama una vez al inicio de cada turno propio.
    public void TickTurnStart()
    {
        ownTurnsStarted++;

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
        uses.Clear();
        ownTurnsStarted = 0;
    }
}
