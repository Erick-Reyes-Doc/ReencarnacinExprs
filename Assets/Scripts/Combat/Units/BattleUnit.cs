
using UnityEngine;

// Identifica a qué equipo pertenece una unidad.
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

        InitializeUnit();
    }

    // Inicializa las estadisticas del personaje.
    public void InitializeUnit()
    {
        maxHealth = unitData.maxHealth;
        attack = unitData.attack;
        defense = unitData.defense;
        speed = unitData.speed;

        criticalChance = unitData.criticalChance;
        criticalDamage = unitData.criticalDamage;

        currentHealth = maxHealth;
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
