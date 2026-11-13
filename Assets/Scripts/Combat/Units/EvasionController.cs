using UnityEngine;

// Clasificacion de un ataque para la evasion.
public enum AttackWeight
{
    Light,
    Heavy,
    Unavoidable
}

// Evasion porcentual. TODAS las unidades esquivan con BattleUnit.EvasionPercent (datos + reliquias, tope 40 %):
//   Ligero:      EvasionPercent
//   Pesado:      la mitad (heavyMultiplier)
//   Inevitable:  0 %
// AbilitySystem tira la evasion una sola vez por objetivo (ver ResolveOffensiveHit). Este componente es opcional y
// solo expone la misma consulta para pruebas; ya no guarda valores propios.
public class EvasionController : MonoBehaviour
{
    public const float HeavyMultiplier = 0.5f;

    private BattleUnit unit;

    // 'penetration' = % de la evasion del objetivo que el ataque ignora (50 = la mitad).
    public static float ChanceFor(BattleUnit target, AttackWeight weight, float penetration = 0f)
    {
        if (target == null || weight == AttackWeight.Unavoidable)
            return 0f;

        float chance = target.EvasionPercent;

        if (weight == AttackWeight.Heavy)
            chance *= HeavyMultiplier;

        if (penetration > 0f)
            chance *= 1f - Mathf.Clamp(penetration, 0f, 100f) / 100f;

        return Mathf.Clamp(chance, 0f, 100f);
    }

    // Tira la evasion. 'roll' (0-100) permite fijar el resultado en pruebas; si es negativo se usa un valor al azar.
    public static bool TryEvade(BattleUnit target, AttackWeight weight, float roll = -1f, float penetration = 0f)
    {
        float chance = ChanceFor(target, weight, penetration);

        if (chance <= 0f)
            return false;

        if (roll < 0f)
            roll = Random.Range(0f, 100f);

        return roll < chance;
    }

    public float GetChance(AttackWeight weight)
    {
        if (unit == null)
            unit = GetComponent<BattleUnit>();

        return ChanceFor(unit, weight);
    }

    public bool TryEvade(AttackWeight weight, float roll = -1f)
    {
        if (unit == null)
            unit = GetComponent<BattleUnit>();

        return TryEvade(unit, weight, roll);
    }
}
