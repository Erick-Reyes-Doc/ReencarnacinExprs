
using UnityEngine;

public static class DamageCalculator
{
    public static int CalculateDamage(
        BattleUnit attacker,
        BattleUnit target,
        out bool isCritical
    )
    {
        // Calcular el daño base.
        int damage = Mathf.Max(
            1,
            attacker.Attack - target.Defense
        );

        // Calcular si el ataque es critico.
        isCritical = Random.Range(0f, 100f)
            < attacker.CriticalChance;

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
