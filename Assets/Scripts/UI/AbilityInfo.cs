using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Texto informativo de una habilidad con los numeros reales de un protagonista: daño (min-max y critico) calculado con
// sus estadisticas actuales (nivel y reliquias), curacion, efectos con su potencia y duracion segun el nivel, desde que
// posiciones se usa y a cuales alcanza. Usa las mismas formulas que el combate (DamageCalculator / AbilitySystem), asi
// que las cifras coinciden con las que se ven al jugar. Solo presenta: no modifica nada.
public static class AbilityInfo
{
    private const string Gold = "#D4AF6A";
    private const string Dim = "#9AA3B2";

    public static string Body(AbilityData ability, HeroProfile hero, HeroStats stats, IList<int> allyMaxHealths)
    {
        StringBuilder sb = new StringBuilder();

        void Line(string text)
        {
            if (sb.Length > 0)
                sb.Append('\n');

            sb.Append(text);
        }

        if (!string.IsNullOrEmpty(ability.description))
            Line(ability.description);

        float critMultiplier = hero != null && hero.unit != null ? hero.unit.criticalDamage : 1.5f;

        // ---- Daño ----
        if (ability.abilityType == AbilityType.Attack && ability.dealsDamage)
        {
            int min = Roll(stats.damageMin, ability.damagePercent, ability.basePower);
            int max = Roll(stats.damageMax, ability.damagePercent, ability.basePower);
            Line(Label("Daño", Range(min, max)) + " <color=" + Dim + ">(antes de la protección)</color>");

            if (ability.canCritical)
            {
                float chance = Mathf.Clamp(stats.crit + ability.criticalChanceBonus, 0f, 100f);
                Line(Label("Crítico (" + Percent(chance) + ")", Range(Crit(min, critMultiplier), Crit(max, critMultiplier))));
            }

            if (ability.bonusIfTargetHas != StatusEffectType.None && ability.bonusDamagePercent > 0f)
                Line("+" + Percent(ability.bonusDamagePercent) + " de daño si el objetivo tiene " + StatusName(ability.bonusIfTargetHas));

            if (ability.defensePenetration > 0f)
                Line("Ignora el " + Percent(ability.defensePenetration) + " de la protección del objetivo");
        }

        // ---- Curación ----
        if (ability.abilityType == AbilityType.Heal)
        {
            if (ability.healMax > 0)
            {
                Line(Label("Cura", Range(ability.healMin, ability.healMax) + " PV"));
            }
            else if (ability.healPercentMaxHealth > 0f)
            {
                bool self = ability.targetType == AbilityTargetType.Self;
                int low = stats.maxHealth, high = stats.maxHealth;

                if (!self && allyMaxHealths != null && allyMaxHealths.Count > 0)
                {
                    low = int.MaxValue;
                    high = 0;

                    foreach (int health in allyMaxHealths)
                    {
                        low = Mathf.Min(low, health);
                        high = Mathf.Max(high, health);
                    }
                }

                float percent = ability.healPercentMaxHealth;
                Line(Label("Cura", Percent(percent) + (self ? " de tu vida máxima" : " de la vida máxima del aliado") +
                    " (" + Range(HealOf(low, percent), HealOf(high, percent)) + " PV)"));

                if (ability.canCritical)
                {
                    float chance = Mathf.Clamp(stats.crit + ability.criticalChanceBonus, 0f, 100f);
                    float critPercent = percent * (ability.healCriticalMultiplier > 0f ? ability.healCriticalMultiplier : critMultiplier);
                    Line(Label("Crítico (" + Percent(chance) + ")", Percent(critPercent) +
                        " (" + Range(HealOf(low, critPercent), HealOf(high, critPercent)) + " PV)"));
                }
            }
            else if (ability.basePower > 0)
            {
                Line(Label("Cura", ability.basePower + " PV"));
            }
        }

        // ---- Regeneración ----
        if (ability.HasRegeneration)
            Line(Label("Regeneración", ability.regenerationPercentMaxHealth > 0f
                ? "+" + ability.regenerationPercentMaxHealth.ToString("0.#") + " % de la vida máxima por turno durante " + Turns(ability.regenerationDuration)
                : "+" + ability.regenerationPerTurn + " PV por turno durante " + Turns(ability.regenerationDuration)));

        // ---- Efectos de estado (potencia y duración con el nivel del personaje) ----
        if (ability.statusEffects != null)
        {
            foreach (AbilityStatusEffect effect in ability.statusEffects)
            {
                if (effect == null || effect.type == StatusEffectType.Regeneration)
                    continue;

                // Rango del daño de referencia (tirada del personaje con el % de daño y el Poder de la habilidad, sin
                // critico) para los estados con "% del daño por turno".
                int refMin = Roll(stats.damageMin, ability.damagePercent, ability.basePower);
                int refMax = Roll(stats.damageMax, ability.damagePercent, ability.basePower);
                string text = EffectText(effect, stats.level, ability, refMin, refMax);

                if (effect.chance < 100f)
                    text += " (" + Percent(effect.chance) + ")";

                if (effect.applyToUser)
                    text += " sobre ti";

                Line("• " + text);
            }
        }

        // ---- Posiciones ----
        Line(Label("Se usa desde", RangeText(ability.usablePositions)));

        switch (ability.targetType)
        {
            case AbilityTargetType.Self:
                Line(Label("Alcanza", "solo a ti"));
                break;
            case AbilityTargetType.AllAllies:
                Line(Label("Alcanza", "a todo el equipo"));
                break;
            case AbilityTargetType.AllEnemies:
                Line(Label("Alcanza", "a todos los enemigos"));
                break;
            default:
                Line(Label("Alcanza", RangeText(ability.targetPositions)));
                break;
        }

        Line("<color=" + Dim + ">P1 = primera línea, P4 = retaguardia</color>");

        // ---- Movimiento ----
        if (ability.userMovement != UserMovement.None)
            Line(Label("Movimiento", (ability.userMovement == UserMovement.Forward ? "avanzas 1 posición" : "retrocedes 1 posición") +
                (ability.movementOptional ? " si es posible" : "")));

        if (ability.movementEffect != MovementEffect.None && ability.movementDistance > 0)
            Line(Label("Desplaza", (ability.movementEffect == MovementEffect.Push ? "empuja" : "atrae") + " al objetivo " +
                ability.movementDistance + (ability.movementDistance == 1 ? " posición" : " posiciones") +
                (ability.movementChance < 100f ? " (" + Percent(ability.movementChance) + ")" : "")));

        if (ability.requiresPreparation)
            Line("Requiere preparación: impacta en tu siguiente turno.");

        if (ability.availableFromTurn > 1)
            Line(Label("Disponible", "desde la ronda " + ability.availableFromTurn));

        if (ability.cooldown > 0)
            Line(Label("Enfriamiento", Turns(ability.cooldown)));

        return sb.ToString();
    }

    // ---------------- Versión compacta para el panel de información del combate ----------------
    // Usa los valores reales de la unidad que va a actuar (daño ya con Debilitamiento, crítico, etc.).

    // "Daño: 4–5 · Crít. 4 %: 6–8", "Cura: 9–14 PV · Crít. 5 %: 13–21" o null si la habilidad no daña ni cura.
    public static string CombatNumbers(AbilityData ability, BattleUnit user, IList<int> allyMaxHealths)
    {
        if (ability == null || user == null)
            return null;

        if (ability.abilityType == AbilityType.Attack && ability.dealsDamage)
        {
            int min = Roll(user.DamageMin, ability.damagePercent, ability.basePower);
            int max = Roll(user.DamageMax, ability.damagePercent, ability.basePower);
            string text = "Daño: " + Range(min, max);

            if (ability.canCritical)
            {
                float chance = Mathf.Clamp(user.CriticalChance + ability.criticalChanceBonus, 0f, 100f);
                text += " · Crít. " + Percent(chance) + ": " + Range(Crit(min, user.CriticalDamage), Crit(max, user.CriticalDamage));
            }

            return text;
        }

        if (ability.abilityType == AbilityType.Heal)
        {
            if (ability.healMax > 0)
                return "Cura: " + Range(ability.healMin, ability.healMax) + " PV";

            if (ability.healPercentMaxHealth > 0f)
            {
                int low = user.MaxHealth, high = user.MaxHealth;

                if (ability.targetType != AbilityTargetType.Self && allyMaxHealths != null && allyMaxHealths.Count > 0)
                {
                    low = int.MaxValue;
                    high = 0;

                    foreach (int health in allyMaxHealths)
                    {
                        low = Mathf.Min(low, health);
                        high = Mathf.Max(high, health);
                    }
                }

                float percent = ability.healPercentMaxHealth;
                string text = "Cura: " + Range(HealOf(low, percent), HealOf(high, percent)) + " PV";

                if (ability.canCritical)
                {
                    float chance = Mathf.Clamp(user.CriticalChance + ability.criticalChanceBonus, 0f, 100f);
                    float critPercent = percent * (ability.healCriticalMultiplier > 0f ? ability.healCriticalMultiplier : user.CriticalDamage);
                    text += " · Crít. " + Percent(chance) + ": " + Range(HealOf(low, critPercent), HealOf(high, critPercent));
                }

                return text;
            }

            if (ability.basePower > 0)
                return "Cura: " + ability.basePower + " PV";
        }

        if (ability.HasRegeneration)
            return ability.regenerationPercentMaxHealth > 0f
                ? "Regenera: +" + ability.regenerationPercentMaxHealth.ToString("0.#") + " % de la vida máxima por turno, " + Turns(ability.regenerationDuration)
                : "Regenera: +" + ability.regenerationPerTurn + " PV por turno, " + Turns(ability.regenerationDuration);

        return null;
    }

    // "Desde P1 – P2 · Alcanza P1 – P2" (o "Todos los enemigos", "Todo el equipo", "Solo tú").
    public static string CombatPositions(AbilityData ability)
    {
        if (ability == null)
            return null;

        string from = "Desde " + RangeText(ability.usablePositions);

        switch (ability.targetType)
        {
            case AbilityTargetType.Self: return from + " · Solo tú";
            case AbilityTargetType.AllAllies: return from + " · Todo el equipo";
            case AbilityTargetType.AllEnemies: return from + " · Todos los enemigos";
            default: return from + " · Alcanza " + RangeText(ability.targetPositions);
        }
    }

    // ---------------- Cálculos (mismas formulas que el combate) ----------------

    // DamageCalculator.CalculatePreProtection: tirada escalada por el % de la habilidad, mas el Poder; minimo 1.
    private static int Roll(int roll, int damagePercent, int power)
    {
        if (damagePercent != 100)
            roll = Mathf.RoundToInt(roll * damagePercent / 100f);

        return Mathf.Max(1, roll + power);
    }

    private static int Crit(int damage, float multiplier)
    {
        return Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
    }

    // AbilitySystem.ResolveHeal: porcentaje de la vida maxima del objetivo; minimo 1.
    private static int HealOf(int maxHealth, float percent)
    {
        return Mathf.Max(1, Mathf.RoundToInt(maxHealth * percent / 100f));
    }

    // ---------------- Texto ----------------

    private static string Label(string name, string value)
    {
        return "<color=" + Gold + ">" + name + ":</color> " + value;
    }

    private static string Range(int min, int max)
    {
        return min == max ? min.ToString() : min + "–" + max;
    }

    private static string Percent(float value)
    {
        return value.ToString("0.#") + " %";
    }

    private static string Turns(int turns)
    {
        return turns + (turns == 1 ? " turno" : " turnos");
    }

    // "P1 – P3" (contiguas), "P1, P3" (sueltas) o "P2" segun las posiciones marcadas.
    private static string RangeText(bool[] flags)
    {
        List<int> positions = new List<int>();

        for (int i = 0; flags != null && i < flags.Length; i++)
            if (flags[i])
                positions.Add(i + 1);

        if (positions.Count == 0)
            return "-";

        if (positions.Count == 1)
            return "P" + positions[0];

        if (positions[positions.Count - 1] - positions[0] == positions.Count - 1)
            return "P" + positions[0] + " – P" + positions[positions.Count - 1];

        List<string> parts = new List<string>();

        foreach (int position in positions)
            parts.Add("P" + position);

        return string.Join(", ", parts);
    }

    private static string StatusName(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Bleeding: return "Sangrado";
            case StatusEffectType.Burn: return "Quemadura";
            case StatusEffectType.Poison: return "Veneno";
            case StatusEffectType.Mark: return "Marca";
            case StatusEffectType.Stun: return "Aturdimiento";
            case StatusEffectType.Weaken: return "Debilitamiento";
            case StatusEffectType.Vulnerability: return "Vulnerabilidad";
            case StatusEffectType.DefenseBoost: return "Protección";
            case StatusEffectType.Guard: return "Guardia";
            case StatusEffectType.PriorityTarget: return "Objetivo prioritario";
            case StatusEffectType.EvasionBoost: return "Evasión";
            default: return type.ToString();
        }
    }

    // Lo que hace cada estado, con su potencia y duracion para el nivel del usuario.
    private static string EffectText(AbilityStatusEffect effect, int level, AbilityData ability, int refMin, int refMax)
    {
        int power = effect.GetPotency(level);
        string duration = Turns(effect.GetDuration(level));
        string name = StatusName(effect.type);

        switch (effect.type)
        {
            case StatusEffectType.Bleeding:
            case StatusEffectType.Burn:
            case StatusEffectType.Poison:
                // "% del daño": el daño por turno sale del golpe; se muestra el % y el rango aproximado (sin critico).
                if (effect.ScalesWithDamage)
                    return name + ": " + Percent(effect.damageScalePercent) + " del daño" +
                        (ability.dealsDamage ? " del golpe" : " de referencia") + " por turno (" +
                        Range(effect.PeriodicDamageFrom(refMin), effect.PeriodicDamageFrom(refMax)) + " de daño), " + duration;

                return name + ": " + power + " de daño por turno, " + duration;
            case StatusEffectType.Weaken:
                return name + ": −" + power + " % de daño, " + duration;
            case StatusEffectType.Vulnerability:
                return name + ": −" + power + " % de protección, " + duration;
            case StatusEffectType.DefenseBoost:
                return name + ": +" + power + " %, " + duration;
            case StatusEffectType.EvasionBoost:
                return name + ": +" + power + " %, " + duration;
            case StatusEffectType.Guard:
                // La Guardia de un aliado se define con "allyProtectionPercent": intercepta ese % del daño directo
                // dirigido al aliado. Con potencia propia, reduce el daño directo que recibe quien la tiene.
                if (power <= 0 && ability.allyProtectionPercent > 0)
                    return name + ": intercepta el " + ability.allyProtectionPercent + " % del daño directo dirigido al aliado, " + duration;

                return name + ": −" + power + " % de daño directo recibido, " + duration;
            case StatusEffectType.Stun:
                return name + ": pierde su turno, " + duration;
            case StatusEffectType.Mark:
                return name + ": el siguiente ataque directo hace el doble, " + duration;
            default:
                return name + ": " + power + ", " + duration;
        }
    }
}
