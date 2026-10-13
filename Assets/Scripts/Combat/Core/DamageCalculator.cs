
using UnityEngine;

public static class DamageCalculator
{
    // Defensa del objetivo tras ignorar un porcentaje de ella.
    // Parte de la defensa efectiva (con Vulnerabilidad y mejoras)
    // y no modifica al objetivo.
    public static int GetEffectiveDefense(
        BattleUnit target,
        float defensePenetration
    )
    {
        int defense = target.Defense;

        if (defensePenetration <= 0f)
            return defense;

        float remaining =
            1f - Mathf.Clamp(defensePenetration, 0f, 100f) / 100f;

        return Mathf.Max(0, Mathf.RoundToInt(defense * remaining));
    }

    public static int CalculateDamage(
        BattleUnit attacker,
        BattleUnit target,
        out bool isCritical
    )
    {
        return CalculateDamage(attacker, target, 0f, out isCritical);
    }

    public static int CalculateDamage(
        BattleUnit attacker,
        BattleUnit target,
        float defensePenetration,
        out bool isCritical
    )
    {
        return CalculateDamage(
            attacker, target, defensePenetration, 0, true, out isCritical
        );
    }

    // Daño base = max(1, ATQ efectivo + Poder - DEF efectiva tras
    // penetracion). Despues se aplica el critico. La Marca y las
    // reducciones se aplican al recibir el daño (BattleUnit).
    // El Poder puede ser negativo.
    public static int CalculateDamage(
        BattleUnit attacker,
        BattleUnit target,
        float defensePenetration,
        int power,
        bool allowCritical,
        out bool isCritical
    )
    {
        // Calcular el daño base.
        int damage = Mathf.Max(
            1,
            attacker.Attack + power -
            GetEffectiveDefense(target, defensePenetration)
        );

        // Calcular si el ataque es critico.
        isCritical = allowCritical &&
            Random.Range(0f, 100f) < attacker.CriticalChance;

        // Aplicar multiplicador critico.
        if (isCritical)
        {
            damage = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    damage * attacker.CriticalDamage
                )
            );
        }

        return damage;
    }
}
