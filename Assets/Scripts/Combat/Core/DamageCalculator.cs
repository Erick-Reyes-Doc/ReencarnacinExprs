
using UnityEngine;

// Daño directo. Formula (escala pequeña, protección porcentual):
//   1. Tirada      = valor entero al azar entre DañoMin y DañoMax del atacante (ya con Debilitamiento y reliquias).
//   2. Bruto       = max(1, Tirada + Poder de la habilidad).
//   3. Critico     = si procede (solo cuando el ataque impacta): Bruto x multiplicador critico.
//   4. Proteccion  = Bruto x (1 - Proteccion% efectiva / 100), redondeado, minimo 1.
// La Marca, Guard (reduccion), PriorityTarget y la intercepcion de aliados se aplican despues, al recibir el daño
// (BattleUnit). La evasion se tira antes, en AbilitySystem, una sola vez por objetivo.
public static class DamageCalculator
{
    // Protección efectiva (%) del objetivo tras ignorar un porcentaje de ella.
    // La penetracion solo reduce una proteccion positiva; una proteccion negativa (Vulnerabilidad) no cambia.
    public static float GetEffectiveProtection(
        BattleUnit target,
        float defensePenetration
    )
    {
        float protection = target.ProtectionPercent;

        if (protection <= 0f || defensePenetration <= 0f)
            return protection;

        return protection * (1f - Mathf.Clamp(defensePenetration, 0f, 100f) / 100f);
    }

    // Aplica la proteccion porcentual a un daño. Minimo 1.
    public static int ApplyProtection(int damage, float protectionPercent)
    {
        return Mathf.Max(
            1,
            Mathf.FloorToInt(damage * (1f - protectionPercent / 100f) + 0.5f)
        );
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

    public static int CalculateDamage(
        BattleUnit attacker,
        BattleUnit target,
        float defensePenetration,
        int power,
        bool allowCritical,
        out bool isCritical
    )
    {
        return CalculateDamage(
            attacker, target, defensePenetration, power, allowCritical, 0f,
            out isCritical
        );
    }

    // Igual que la anterior con puntos porcentuales extra de critico
    // (solo para la habilidad que lo pide).
    public static int CalculateDamage(
        BattleUnit attacker,
        BattleUnit target,
        float defensePenetration,
        int power,
        bool allowCritical,
        float criticalChanceBonus,
        out bool isCritical
    )
    {
        return CalculateDamage(
            attacker, target, defensePenetration, power, 100,
            allowCritical, criticalChanceBonus, out isCritical
        );
    }

    // 'damagePercent' = porcentaje de la tirada de daño base que usa la habilidad (100 = completa).
    public static int CalculateDamage(
        BattleUnit attacker,
        BattleUnit target,
        float defensePenetration,
        int power,
        int damagePercent,
        bool allowCritical,
        float criticalChanceBonus,
        out bool isCritical
    )
    {
        int damage = CalculatePreProtection(
            attacker, power, damagePercent, allowCritical, criticalChanceBonus, out isCritical
        );

        // 4. Proteccion porcentual del objetivo.
        return ApplyProtection(
            damage, GetEffectiveProtection(target, defensePenetration)
        );
    }

    // Pasos 1-3 (tirada min-max escalada, Poder y critico) SIN la proteccion del objetivo. Sirve para repartir el
    // daño entre varios receptores (Guardia de Trinchera), cada uno con su propia PROT.
    public static int CalculatePreProtection(
        BattleUnit attacker,
        int power,
        int damagePercent,
        bool allowCritical,
        float criticalChanceBonus,
        out bool isCritical
    )
    {
        // 1-2. Tirada entre minimo y maximo, escalada por el % de la habilidad, mas el Poder.
        int roll = Random.Range(attacker.DamageMin, attacker.DamageMax + 1);

        if (damagePercent != 100)
            roll = Mathf.RoundToInt(roll * damagePercent / 100f);

        int damage = Mathf.Max(1, roll + power);

        // 3. Critico (solo se llega aqui si el ataque impacto).
        isCritical = allowCritical &&
            Random.Range(0f, 100f) < attacker.CriticalChance + criticalChanceBonus;

        if (isCritical)
        {
            damage = Mathf.Max(
                1,
                Mathf.RoundToInt(damage * attacker.CriticalDamage)
            );
        }

        return damage;
    }

    // Daño medio esperado de una habilidad (sin critico ni evasion). Lectura para la IA y las pruebas.
    public static int ExpectedDamage(
        BattleUnit attacker,
        BattleUnit target,
        int power,
        float defensePenetration
    )
    {
        return ExpectedDamage(attacker, target, power, 100, defensePenetration);
    }

    public static int ExpectedDamage(
        BattleUnit attacker,
        BattleUnit target,
        int power,
        int damagePercent,
        float defensePenetration
    )
    {
        int damage = Mathf.Max(
            1,
            Mathf.RoundToInt(attacker.AverageDamage * damagePercent / 100f + power)
        );

        return ApplyProtection(
            damage, GetEffectiveProtection(target, defensePenetration)
        );
    }
}
