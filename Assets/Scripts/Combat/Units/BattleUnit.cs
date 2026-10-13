
using System;
using UnityEngine;

// Identifica a que equipo pertenece una unidad.
public enum TeamSide
{
    Player,
    Enemy
}

public class BattleUnit : MonoBehaviour
{
    [Header("Datos del personaje")]
    [SerializeField] private UnitData unitData;

    [Header("Equipo")]
    [SerializeField] private TeamSide teamSide;

    [Header("Posicion en combate")]
    [Range(1, 4)]
    [SerializeField] private int position = 1;

    // Componente opcional para personajes con niveles.
    private CharacterProgression progression;

    // Estadisticas efectivas durante el combate.
    private int maxHealth;
    private int attack;
    private int defense;
    private int speed;
    private float criticalChance;
    private float criticalDamage;

    // Vida actual de la unidad.
    private int currentHealth;

    // Propiedades de lectura.
    public UnitData Data => unitData;
    public TeamSide Side => teamSide;
    public int Position => position;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    // Ataque y defensa efectivos: base + nivel, ajustados por
    // Debilitamiento y Vulnerabilidad. No modifican UnitData ni
    // los valores calculados por nivel.
    public int Attack => ApplyReduction(
        attack, StatusEffectType.Weaken
    );

    public int Defense => ApplyReduction(
        defense, StatusEffectType.Vulnerability
    );
    public int Speed => speed;

    public float CriticalChance => criticalChance;
    public float CriticalDamage => criticalDamage;

    public bool IsAlive => currentHealth > 0;

    // Se dispara una vez cuando la vida pasa de positiva a 0.
    public event Action<BattleUnit> OnDefeated;

    private void Awake()
    {
        if (unitData == null)
        {
            Debug.LogError(
                "No se asignaron datos a " + gameObject.name
            );

            enabled = false;
            return;
        }

        // Busca la progresion si existe.
        progression = GetComponent<CharacterProgression>();

        // Configura las estadisticas iniciales.
        InitializeUnit();

        // Escucha las subidas de nivel.
        if (progression != null)
        {
            progression.OnLevelUp += HandleLevelUp;
        }
    }

    private void OnDestroy()
    {
        // Evita dejar eventos suscritos.
        if (progression != null)
        {
            progression.OnLevelUp -= HandleLevelUp;
        }
    }

    // Inicializa las estadisticas del personaje.
    // Este metodo tambien restaura la vida al maximo.
    public void InitializeUnit()
    {
        UpdateStats();

        currentHealth = maxHealth;
    }

    // Calcula las estadisticas segun el nivel actual.
    // No modifica la vida actual.
    private void UpdateStats()
    {
        int healthBonus = 0;
        int attackBonus = 0;
        int defenseBonus = 0;
        int speedBonus = 0;

        if (progression != null)
        {
            healthBonus = progression.HealthBonus;
            attackBonus = progression.AttackBonus;
            defenseBonus = progression.DefenseBonus;
            speedBonus = progression.SpeedBonus;
        }

        maxHealth = Mathf.Max(
            1,
            unitData.maxHealth + healthBonus
        );

        attack = Mathf.Max(
            0,
            unitData.attack + attackBonus
        );

        defense = Mathf.Max(
            0,
            unitData.defense + defenseBonus
        );

        speed = Mathf.Max(
            0,
            unitData.speed + speedBonus
        );

        // Por ahora los criticos mantienen
        // sus valores base.
        criticalChance = unitData.criticalChance;
        criticalDamage = unitData.criticalDamage;
    }

    // Se ejecuta al subir de nivel.
    private void HandleLevelUp(int newLevel)
    {
        UpdateStats();

        // La vida actual se conserva.
        // Solo nos aseguramos de no superar
        // el nuevo maximo.
        currentHealth = Mathf.Min(
            currentHealth,
            maxHealth
        );

        Debug.Log(
            unitData.unitName +
            " ahora es nivel " + newLevel +
            ". Estadisticas actualizadas: " +
            "HP " + currentHealth + "/" + maxHealth +
            ", ATQ " + attack +
            ", DEF " + defense +
            ", VEL " + speed
        );
    }

    // Controlador de estados (opcional), buscado una sola vez.
    private StatusEffectController statusEffects;
    private bool statusEffectsSearched;

    private StatusEffectController StatusEffects
    {
        get
        {
            if (!statusEffectsSearched)
            {
                statusEffects = GetComponent<StatusEffectController>();
                statusEffectsSearched = true;
            }

            return statusEffects;
        }
    }

    // Potencia porcentual del estado (20 = 20%), 0 si no esta activo.
    private int GetStatusPercent(StatusEffectType type)
    {
        StatusEffectController controller = StatusEffects;

        if (controller == null)
            return 0;

        StatusEffectInstance effect = controller.GetEffect(type);

        return effect == null
            ? 0
            : Mathf.Clamp(effect.Potency, 0, 100);
    }

    // Reduce un valor segun el porcentaje del estado. Nunca baja de 0.
    private int ApplyReduction(int value, StatusEffectType type)
    {
        int percent = GetStatusPercent(type);

        if (percent <= 0)
            return value;

        return Mathf.Max(
            0,
            Mathf.RoundToInt(value * (100 - percent) / 100f)
        );
    }

    // Daño directo (ataques y habilidades). Aplica el bono de Marca
    // sobre el daño ya calculado con defensa y criticos.
    // El daño periodico y las curaciones no pasan por aqui.
    public void TakeDirectDamage(int damage)
    {
        int markPercent = GetStatusPercent(StatusEffectType.Mark);

        if (markPercent > 0 && damage > 0)
        {
            int marked = Mathf.RoundToInt(
                damage * (100 + markPercent) / 100f
            );

            Debug.Log(
                unitData.unitName + " esta marcado: daño " +
                damage + " -> " + marked
            );

            damage = marked;
        }

        TakeDamage(damage);
    }

    // Permite que la unidad reciba daño.
    public void TakeDamage(int damage)
    {
        if (!IsAlive) return;

        damage = Mathf.Max(0, damage);

        currentHealth = Mathf.Max(
            0,
            currentHealth - damage
        );

        Debug.Log(
            unitData.unitName +
            " recibio " + damage +
            " de daño. Vida restante: " +
            currentHealth + "/" + maxHealth
        );

        if (!IsAlive)
        {
            Debug.Log(
                unitData.unitName + " ha sido derrotado."
            );

            // Solo se llega aqui al pasar de vida positiva a 0,
            // porque TakeDamage ignora a las unidades derrotadas.
            OnDefeated?.Invoke(this);
        }
    }

    // Recupera puntos de vida.
    public void Heal(int amount)
    {
        if (!IsAlive) return;

        amount = Mathf.Max(0, amount);

        currentHealth = Mathf.Min(
            maxHealth,
            currentHealth + amount
        );

        Debug.Log(
            unitData.unitName +
            " ahora tiene " +
            currentHealth + "/" + maxHealth +
            " de vida."
        );
    }

    // Cambia la posicion del personaje.
    public void SetPosition(int newPosition)
    {
        if (newPosition < 1 || newPosition > 4)
        {
            Debug.LogWarning(
                "La posicion debe estar entre 1 y 4."
            );
            return;
        }

        position = newPosition;
    }
}
