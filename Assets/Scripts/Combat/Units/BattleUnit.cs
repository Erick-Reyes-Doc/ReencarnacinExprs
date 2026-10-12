
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
    public int Attack => attack;
    public int Defense => defense;
    public int Speed => speed;

    public float CriticalChance => criticalChance;
    public float CriticalDamage => criticalDamage;

    public bool IsAlive => currentHealth > 0;

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
