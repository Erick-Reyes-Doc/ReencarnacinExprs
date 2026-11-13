
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
    private int damageMin;
    private int damageMax;
    private int protection;      // % base (datos + reliquias)
    private float evasion;       // % base (datos + reliquias)
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
    // Daño base efectivo: el rango se reduce con Debilitamiento (en %). Cada ataque tira un valor entre
    // minimo y maximo; el Poder de la habilidad se suma despues (ver DamageCalculator).
    public int DamageMin => Mathf.Max(1, ApplyReduction(damageMin, StatusEffectType.Weaken));
    public int DamageMax => Mathf.Max(DamageMin, ApplyReduction(damageMax, StatusEffectType.Weaken));

    // Daño medio, para comparaciones y estimaciones de la IA ("ataque" relativo).
    public float AverageDamage => (DamageMin + DamageMax) * 0.5f;
    public int Attack => Mathf.RoundToInt(AverageDamage);

    // Limites de la protección porcentual.
    public const int MaxProtectionPercent = 50;
    public const int MinProtectionPercent = -50;
    public const int MaxEvasionPercent = 40;

    // Protección efectiva en %: base + mejora (DefenseBoost, en puntos) - Vulnerabilidad (en puntos).
    // Con valor negativo el objetivo recibe daño extra. La penetracion se aplica despues (DamageCalculator).
    public int ProtectionPercent
    {
        get
        {
            int value = protection
                + GetStatusPercent(StatusEffectType.DefenseBoost)
                - GetStatusPercent(StatusEffectType.Vulnerability);

            return Mathf.Clamp(value, MinProtectionPercent, MaxProtectionPercent);
        }
    }

    // Probabilidad de esquivar ataques directos (%).
    public float EvasionPercent => Mathf.Clamp(
        evasion + GetStatusPercent(StatusEffectType.EvasionBoost),
        0f,
        MaxEvasionPercent
    );

    // Maximo de reduccion de daño directo acumulable.
    public const int MaxDamageReductionPercent = 50;

    // Reduccion actual del daño directo recibido (Guard), con tope.
    public int DamageReductionPercent => Mathf.Min(
        MaxDamageReductionPercent,
        GetStatusPercent(StatusEffectType.Guard)
    );
    // Velocidad efectiva: base + reliquias + bonos temporales de reliquia (Botas del Mensajero).
    public int Speed => speed + RelicEffects.TempSpeed(this);

    public float CriticalChance => criticalChance;
    public float CriticalDamage => criticalDamage;

    // Un enemigo que HUYE del campo (p. ej. los secuaces cuando cae el jefe) deja de participar: no cuenta como vivo, no
    // actua y no se contabiliza como derrota ni da recompensas.
    private bool hasFled;

    public bool HasFled => hasFled;

    public bool IsAlive => currentHealth > 0 && !hasFled;

    // Heroe CAIDO: vida 0. Los protagonistas no mueren: quedan en el suelo, sin turno ni objetivo para los enemigos,
    // hasta que reciben una curacion (pocion o habilidad de curacion de Noelle).
    public bool IsDowned => currentHealth <= 0 && teamSide == TeamSide.Player && !hasFled;

    // Se dispara una sola vez cuando la unidad huye (PositionManager la retira de la formacion).
    public event Action<BattleUnit> OnFled;

    public void Flee()
    {
        if (hasFled || currentHealth <= 0)
            return;

        hasFled = true;
        OnFled?.Invoke(this);
    }

    // Se dispara una vez cuando la vida pasa de positiva a 0.
    public event Action<BattleUnit> OnDefeated;

    // Se dispara cuando una unidad derrotada vuelve a la vida
    // (recuperacion poscombate). PositionManager la reincorpora.
    public event Action<BattleUnit> OnRevived;

    // Recuperacion poscombate (solo tras una victoria). Los supervivientes
    // conservan EXACTAMENTE la vida con la que terminaron: no reciben
    // curacion. Una unidad derrotada revive con 'percent'% de su vida maxima
    // efectiva (nivel + reliquias), redondeado hacia arriba y minimo 1 HP.
    // Devuelve true si la unidad estaba derrotada y revivio.
    public bool ReviveAfterBattle(int percent)
    {
        if (unitData == null)
            return false;

        // Estadisticas efectivas actualizadas (nivel actual y reliquias).
        // No toca la vida actual.
        UpdateStats();

        if (currentHealth > 0)
        {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            return false;
        }

        // Redondeo hacia arriba.
        int amount = (maxHealth * Mathf.Clamp(percent, 0, 100) + 99) / 100;
        currentHealth = Mathf.Clamp(amount, 1, maxHealth);

        Debug.Log(
            unitData.unitName + " revive con " + currentHealth +
            "/" + maxHealth + " HP."
        );

        OnRevived?.Invoke(this);
        return true;
    }

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

    // Restaura la vida guardada (sesion). Un heroe puede quedar en 0 (caido, espera curacion); un enemigo nunca baja de 1.
    public void RestoreHealth(int health)
    {
        currentHealth = Mathf.Clamp(health, teamSide == TeamSide.Player ? 0 : 1, maxHealth);
    }

    // Calcula las estadisticas segun el nivel actual.
    // No modifica la vida actual.
    private void UpdateStats()
    {
        int healthBonus = 0;
        int damageBonus = 0;
        int speedBonus = 0;

        if (progression != null)
        {
            healthBonus = progression.HealthBonus;
            damageBonus = progression.DamageBonus;
            speedBonus = progression.SpeedBonus;
        }

        maxHealth = Mathf.Max(
            1,
            unitData.maxHealth + healthBonus
        );

        damageMin = Mathf.Max(1, unitData.damageMin + damageBonus);
        damageMax = Mathf.Max(damageMin, unitData.damageMax + damageBonus);
        protection = Mathf.Clamp(unitData.protectionPercent, MinProtectionPercent, MaxProtectionPercent);
        evasion = Mathf.Clamp(unitData.evasionPercent, 0f, MaxEvasionPercent);

        speed = Mathf.Max(
            0,
            unitData.speed + speedBonus
        );

        // Los criticos parten de sus valores base.
        criticalChance = unitData.criticalChance;
        criticalDamage = unitData.criticalDamage;

        // Reliquias equipadas (solo protagonistas): se calculan siempre desde el equipamiento actual, sin acumular.
        if (teamSide == TeamSide.Player && GameSession.IsActive)
        {
            StatMods relics = RelicInventory.GetMods(gameObject.name);
            maxHealth = Mathf.Max(1, maxHealth + relics.health);
            damageMin = Mathf.Max(1, damageMin + relics.damage);
            damageMax = Mathf.Max(damageMin, damageMax + relics.damage);
            protection = Mathf.Clamp(protection + relics.protection, MinProtectionPercent, MaxProtectionPercent);
            evasion = Mathf.Clamp(evasion + relics.evasion, 0f, MaxEvasionPercent);
            speed = Mathf.Max(0, speed + relics.speed);
            criticalChance = Mathf.Clamp(criticalChance + relics.crit, 0f, 100f);
        }
        else if (teamSide == TeamSide.Enemy)
        {
            // Reliquia sorteada del enemigo (EnemyRelics): mismos modificadores que en un heroe.
            StatMods relics = EnemyRelics.GetMods(this);
            maxHealth = Mathf.Max(1, maxHealth + relics.health);
            damageMin = Mathf.Max(1, damageMin + relics.damage);
            damageMax = Mathf.Max(damageMin, damageMax + relics.damage);
            protection = Mathf.Clamp(protection + relics.protection, MinProtectionPercent, MaxProtectionPercent);
            evasion = Mathf.Clamp(evasion + relics.evasion, 0f, MaxEvasionPercent);
            speed = Mathf.Max(0, speed + relics.speed);
            criticalChance = Mathf.Clamp(criticalChance + relics.crit, 0f, 100f);
        }
    }

    // Se ejecuta al subir de nivel.
    private void HandleLevelUp(int newLevel)
    {
        UpdateStats();

        // Al subir de nivel el heroe recupera el 100 % de su vida (con el nuevo maximo). Un heroe caido (vida 0) no se
        // levanta por subir de nivel: sigue esperando una curacion.
        if (IsAlive)
            currentHealth = maxHealth;
        else
            currentHealth = Mathf.Min(currentHealth, maxHealth);

        Debug.Log(
            unitData.unitName +
            " ahora es nivel " + newLevel +
            ". Estadisticas actualizadas: " +
            "HP " + currentHealth + "/" + maxHealth +
            ", daño " + damageMin + "-" + damageMax +
            ", prot " + protection + "%" +
            ", evasion " + evasion + "%" +
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
    // 'attacker' (opcional) permite bonos ligados al origen, como
    // PriorityTarget: solo suma si el atacante es quien aplico la marca.
    public void TakeDirectDamage(int damage, BattleUnit attacker = null)
    {
        if (attacker != null && damage > 0)
        {
            StatusEffectController statuses =
                GetComponent<StatusEffectController>();

            StatusEffectInstance priority = statuses != null
                ? statuses.GetEffect(StatusEffectType.PriorityTarget)
                : null;

            if (priority != null && priority.Source == attacker)
            {
                int boosted = Mathf.RoundToInt(
                    damage * (100 + Mathf.Clamp(priority.Potency, 0, 100)) / 100f
                );

                Debug.Log(
                    unitData.unitName + " es objetivo prioritario de " +
                    attacker.Data.unitName + ": daño " + damage + " -> " + boosted
                );

                damage = boosted;

                // La marca del Capitan se consume con ese ataque directo.
                statuses.RemoveEffect(StatusEffectType.PriorityTarget);
            }
        }

        // Marca: el siguiente ataque directo causa x2 sobre el daño ya
        // calculado (defensa y critico) y la marca se consume al resolverlo.
        StatusEffectController markStatuses =
            GetComponent<StatusEffectController>();

        if (markStatuses != null && markStatuses.HasEffect(StatusEffectType.Mark))
        {
            if (damage > 0)
            {
                int marked = damage * 2;

                Debug.Log(
                    unitData.unitName + " esta marcado: daño " +
                    damage + " -> " + marked
                );

                damage = marked;
            }

            markStatuses.RemoveEffect(StatusEffectType.Mark);
        }

        // La intercepcion de Guardia de Trinchera (reparto entre protector y objetivo, cada uno con su PROT) se resuelve
        // ANTES, en AbilitySystem.DeliverDirectDamage: aqui el daño ya es la parte que recibe esta unidad.
        TakeReducedDamage(damage);

        // Un golpe directo de un atacante consume la Defensa elevada de esta unidad (ya protegio este golpe).
        if (attacker != null && damage > 0)
            ConsumeDefenseBoostOnHit();
    }

    // Defensa elevada (bono de proteccion de habilidades): ademas de gastarse con los turnos, se acaba al recibir un golpe
    // directo (el golpe que la consume ya cuenta con ella). Vale para heroes y enemigos; el daño periodico no la consume.
    public void ConsumeDefenseBoostOnHit()
    {
        StatusEffectController statuses = GetComponent<StatusEffectController>();

        if (statuses != null && statuses.RemoveEffect(StatusEffectType.DefenseBoost))
            Debug.Log(unitData.unitName + " pierde Defensa elevada al recibir un golpe.");
    }

    // Aplica la reduccion de daño directo propia (Guard, con tope)
    // y despues el daño. No redirige ni usa Marca.
    public void TakeReducedDamage(int damage)
    {
        int reduction = DamageReductionPercent;

        if (reduction > 0 && damage > 0)
        {
            // Un impacto valido nunca baja de 1 por la reduccion.
            damage = Mathf.Max(
                1,
                Mathf.RoundToInt(damage * (100 - reduction) / 100f)
            );
        }

        TakeDamage(damage);
    }

    // Vida realmente perdida por una unidad (ya con evasion, defensa,
    // Marca, Guard y proteccion aplicados; sin el exceso sobre la vida
    // restante). 'periodic' = daño periodico (sangrado, veneno,
    // quemadura). Solo lectura: lo usan las metricas de diagnostico.
    public static event Action<BattleUnit, int, bool> DamageApplied;

    // Permite que la unidad reciba daño.
    public void TakeDamage(int damage, bool periodic = false)
    {
        if (!IsAlive) return;

        damage = Mathf.Max(0, damage);

        // Reliquias: el daño directo puede modificarse antes de aplicarse (Idolo del Ultimo Aliento).
        if (!periodic && damage > 0)
            damage = RelicEffects.ModifyIncomingDirect(this, damage);

        int healthBefore = currentHealth;

        currentHealth = Mathf.Max(
            0,
            currentHealth - damage
        );

        DamageApplied?.Invoke(this, healthBefore - currentHealth, periodic);

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

            // Reliquias: una unidad con Marca que cae por daño periodico la transfiere (Ojo del Acechador).
            RelicEffects.OnUnitDefeated(this);

            // Solo se llega aqui al pasar de vida positiva a 0,
            // porque TakeDamage ignora a las unidades derrotadas.
            OnDefeated?.Invoke(this);
        }
        else if (!periodic)
        {
            // Reliquias que reaccionan a un golpe directo recibido (Amuleto de Corteza, Espejo del Dolor).
            RelicEffects.OnDirectDamageTaken(this, healthBefore - currentHealth);
        }
    }

    // Porcentaje de vida maxima con el que revive un aliado caido al recibir una curacion.
    public const int ReviveHealPercent = 40;

    // Curacion realmente aplicada a una unidad (solo presentacion: marcadores flotantes).
    public static event Action<BattleUnit, int> HealApplied;

    // Recupera puntos de vida.
    public void Heal(int amount)
    {
        // Un heroe caido SI puede recibir curacion: es lo que lo levanta. Un enemigo derrotado no.
        bool wasDowned = IsDowned;

        if (!IsAlive && !wasDowned) return;

        amount = Mathf.Max(0, amount);

        int before = currentHealth;

        if (wasDowned)
        {
            // Aliado caido (0 de vida): cualquier curacion lo levanta con el 40 % de su vida maxima (redondeo hacia
            // arriba, minimo 1), sin importar cuanto curaba. Solo aplica a heroes en 0 de vida; a los demas se les
            // suma la curacion normal.
            if (amount > 0)
                currentHealth = Mathf.Clamp((maxHealth * ReviveHealPercent + 99) / 100, 1, maxHealth);
        }
        else
        {
            currentHealth = Mathf.Min(
                maxHealth,
                currentHealth + amount
            );
        }

        if (currentHealth > before)
            HealApplied?.Invoke(this, currentHealth - before);

        if (wasDowned && currentHealth > 0)
        {
            Debug.Log(unitData.unitName + " se levanta con " + currentHealth + "/" + maxHealth + " HP.");
            OnRevived?.Invoke(this);
            return;
        }

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
