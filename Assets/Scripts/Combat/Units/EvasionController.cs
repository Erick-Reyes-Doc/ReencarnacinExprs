using UnityEngine;

// Clasificacion de un ataque para la evasion.
public enum AttackWeight
{
    Light,
    Heavy,
    Unavoidable
}

// Evasion opcional de una unidad: solo la tienen los personajes que
// lleven este componente. No modifica velocidad ni estadisticas.
//
// Ligero:      min(maxChance, baseChance + speedFactor * velocidad)
// Pesado:      la mitad del ligero (heavyMultiplier)
// Inevitable:  0%
public class EvasionController : MonoBehaviour
{
    [Header("Evasion contra ataques ligeros (%)")]
    [SerializeField, Min(0f)] private float baseChance = 8f;
    [SerializeField, Min(0f)] private float speedFactor = 0.5f;
    [SerializeField, Min(0f)] private float maxChance = 25f;

    [Header("Ataques pesados")]
    [SerializeField, Range(0f, 1f)] private float heavyMultiplier = 0.5f;

    private BattleUnit unit;

    public float GetChance(AttackWeight weight)
    {
        if (weight == AttackWeight.Unavoidable)
            return 0f;

        if (unit == null)
            unit = GetComponent<BattleUnit>();

        int speed = unit != null ? unit.Speed : 0;

        float chance = Mathf.Min(
            maxChance,
            baseChance + speedFactor * speed
        );

        if (weight == AttackWeight.Heavy)
            chance *= heavyMultiplier;

        return Mathf.Clamp(chance, 0f, 100f);
    }

    // Tira la evasion. 'roll' (0-100) permite fijar el resultado en
    // pruebas; si es negativo se usa un valor aleatorio.
    public bool TryEvade(AttackWeight weight, float roll = -1f)
    {
        float chance = GetChance(weight);

        if (chance <= 0f)
            return false;

        if (roll < 0f)
            roll = Random.Range(0f, 100f);

        return roll < chance;
    }
}
