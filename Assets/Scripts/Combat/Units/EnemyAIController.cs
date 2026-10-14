using System.Collections.Generic;
using UnityEngine;

// IA de un enemigo. Se anade a las unidades enemigas que deben actuar
// solas; las que no la tengan siguen usando la tecla A.
//
// TurnManager llama a PerformTurn() cuando es el turno del enemigo, ya
// procesados sus estados, y termina el turno una sola vez despues.
// Esta clase solo decide y delega la accion en AbilitySystem (danio,
// evasion, Guardia, estados, cooldowns y posiciones salen de los
// sistemas actuales). Las posiciones se leen siempre relativas al
// equipo: el enemigo es del equipo Enemy y sus rivales del equipo Player.
//
// Estilos (AIStyle):
//  - Meadow:     ataque basico; especial ocasional al azar.
//  - Forest:     cura a un aliado herido si puede; si no, estados utiles
//                con cierta probabilidad; si no, ataque basico.
//  - Corruption: estados utiles y ataques fuertes; algo de variacion.
// Para otras estrategias se puede heredar y sobrescribir ChooseOption()
// y/o ChooseTarget().
public partial class EnemyAIController : MonoBehaviour
{
    // Accion realizada por un enemigo (solo lectura: diagnostico).
    // Parametros: usuario, habilidad, objetivo, es especial.
    public static event System.Action<BattleUnit, AbilityData, BattleUnit, bool>
        ActionPerformed;

    private void NotifyAction(BattleUnit self, AbilityData ability, BattleUnit target)
    {
        ActionPerformed?.Invoke(self, ability, target, ability != basicAttack);
    }

    // Lecturas de diagnostico tomadas justo antes de ejecutar la accion
    // (no influyen en la decision): fraccion de vida del objetivo y si la
    // habilidad solo repetia estados que ya tenia.
    public static float LastTargetHpBefore { get; private set; }
    public static bool LastActionRedundant { get; private set; }

    private static void CaptureBefore(BattleUnit target, AbilityData ability)
    {
        LastTargetHpBefore = target.MaxHealth <= 0
            ? 1f : (float)target.CurrentHealth / target.MaxHealth;

        bool redundant = false;

        if (ability.statusEffects != null && ability.statusEffects.Count > 0)
        {
            redundant = true;
            StatusEffectController effects = target.GetComponent<StatusEffectController>();

            foreach (AbilityStatusEffect status in ability.statusEffects)
            {
                if (status != null && (effects == null || !effects.HasEffect(status.type)))
                    redundant = false;
            }
        }

        LastActionRedundant = redundant;
    }

    public enum TurnResult
    {
        Acted,
        Passed
    }

    public enum AIStyle
    {
        Meadow,
        Forest,
        Corruption
    }

    // Una habilidad utilizable este turno y sus objetivos validos.
    protected class Option
    {
        public AbilityData ability;
        public List<BattleUnit> targets = new List<BattleUnit>();
    }

    [Header("Comportamiento")]
    [Tooltip("Ataque basico que usa el enemigo (AbilityData).")]
    [SerializeField] private AbilityData basicAttack;

    [Tooltip("Habilidades especiales opcionales.")]
    [SerializeField] private List<AbilityData> specialAbilities =
        new List<AbilityData>();

    [SerializeField] private AIStyle style = AIStyle.Meadow;

    [Tooltip("Meadow: probabilidad de usar un especial disponible.")]
    [SerializeField, Range(0f, 1f)] private float specialChance = 0.3f;

    [Tooltip("Forest: probabilidad de usar un estado util disponible.")]
    [SerializeField, Range(0f, 1f)] private float statusPreference = 0.6f;

    [Tooltip("Corruption: probabilidad de elegir una opcion al azar.")]
    [SerializeField, Range(0f, 1f)] private float variation = 0.2f;

    [Tooltip("Pausa antes de actuar, en segundos, para que el jugador vea el turno. 0 = inmediato.")]
    [SerializeField, Min(0f)] private float actionDelay = 0.6f;

    [Header("Referencias (se buscan en la escena si faltan)")]
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private PositionManager positionManager;

    [Header("Origen")]
    [SerializeField] private EnemyArchetypeData archetype;

    private BattleUnit unit;

    public float ActionDelay => actionDelay;
    public AIStyle Style => style;

    // Arquetipo con el que se configuro (null si se configuro a mano).
    public EnemyArchetypeData Archetype => archetype;

    private BattleUnit Unit
    {
        get
        {
            if (unit == null)
                unit = GetComponent<BattleUnit>();

            return unit;
        }
    }

    // Copia las habilidades, el estilo y el retardo de un arquetipo.
    public void ApplyArchetype(EnemyArchetypeData archetype)
    {
        if (archetype == null)
            return;

        this.archetype = archetype;
        basicAttack = archetype.basicAttack;
        specialAbilities = new List<AbilityData>(archetype.specialAbilities);
        style = archetype.aiStyle;
        actionDelay = archetype.actionDelay;
    }

    // ------------------------------------------------------------
    // Construccion de opciones
    // ------------------------------------------------------------

    private void ResolveReferences()
    {
        if (abilitySystem == null)
        {
            foreach (AbilitySystem found in FindObjectsByType<AbilitySystem>())
            {
                if (found.gameObject.scene == gameObject.scene)
                    abilitySystem = found;
            }
        }

        if (positionManager == null)
        {
            foreach (PositionManager found in FindObjectsByType<PositionManager>())
            {
                if (found.gameObject.scene == gameObject.scene)
                    positionManager = found;
            }
        }
    }

    // Lectura de las condiciones de la propia habilidad; AbilitySystem
    // vuelve a validar todo al ejecutarla.
    private bool IsUsable(BattleUnit self, AbilityData ability)
    {
        if (ability == null || ability.requiresPreparation)
            return false;

        if (!ability.CanUseFromPosition(self.Position))
            return false;

        AbilityCooldownController cooldowns =
            self.GetComponent<AbilityCooldownController>();

        return cooldowns == null || cooldowns.IsReady(ability);
    }

    private static int MissingHealth(BattleUnit unit)
    {
        return unit.MaxHealth - unit.CurrentHealth;
    }

    // Vida que debe faltar para que merezca la pena curar.
    private static int HealThreshold(AbilityData ability)
    {
        return Mathf.Max(1, ability.basePower);
    }

    private List<BattleUnit> CollectTargets(BattleUnit self, AbilityData ability)
    {
        List<BattleUnit> targets = new List<BattleUnit>();

        if (ability.targetType == AbilityTargetType.Self)
        {
            targets.Add(self);
            return targets;
        }

        if (ability.targetType != AbilityTargetType.Single)
            return targets; // area y grupales: no soportadas por esta IA

        TeamSide side = ability.abilityType == AbilityType.Attack
            ? (self.Side == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player)
            : self.Side;

        for (int position = 1; position <= 4; position++)
        {
            if (!ability.CanTargetPosition(position))
                continue;

            BattleUnit candidate =
                positionManager.GetUnitAtPosition(side, position);

            if (candidate == null || !candidate.IsAlive)
                continue;

            // Evita curar a quien tiene la vida completa (o casi).
            if (ability.abilityType == AbilityType.Heal &&
                MissingHealth(candidate) < HealThreshold(ability))
                continue;

            targets.Add(candidate);
        }

        return targets;
    }

    private List<Option> BuildOptions(BattleUnit self)
    {
        List<Option> options = new List<Option>();
        List<AbilityData> all = new List<AbilityData>();

        if (basicAttack != null)
            all.Add(basicAttack);

        foreach (AbilityData special in specialAbilities)
        {
            if (special != null && special != basicAttack)
                all.Add(special);
        }

        foreach (AbilityData ability in all)
        {
            if (!IsUsable(self, ability))
                continue;

            List<BattleUnit> targets = CollectTargets(self, ability);

            if (targets.Count == 0)
                continue;

            options.Add(new Option { ability = ability, targets = targets });
        }

        return options;
    }

    // ------------------------------------------------------------
    // Decision
    // ------------------------------------------------------------

    private bool IsSpecial(Option option)
    {
        return option.ability != basicAttack;
    }

    // true si la habilidad aplica estados y algun objetivo aun no los tiene.
    private static bool AppliesUsefulStatus(Option option)
    {
        AbilityData ability = option.ability;

        if (ability.abilityType == AbilityType.Heal ||
            ability.statusEffects == null ||
            ability.statusEffects.Count == 0)
            return false;

        foreach (BattleUnit target in option.targets)
        {
            if (LacksStatus(target, ability))
                return true;
        }

        return false;
    }

    private static bool LacksStatus(BattleUnit target, AbilityData ability)
    {
        StatusEffectController effects =
            target.GetComponent<StatusEffectController>();

        if (effects == null)
            return true;

        foreach (AbilityStatusEffect status in ability.statusEffects)
        {
            if (status != null && !effects.HasEffect(status.type))
                return true;
        }

        return false;
    }

    private static Option RandomOf(List<Option> options)
    {
        return options[Random.Range(0, options.Count)];
    }

    // Eleccion segun el estilo. Sobrescribir para otras estrategias.
    protected virtual Option ChooseOption(List<Option> options, BattleUnit self)
    {
        Option basic = options.Find(o => !IsSpecial(o));
        List<Option> specials = options.FindAll(IsSpecial);

        switch (style)
        {
            case AIStyle.Forest:
            {
                // Prioridad: curar a un aliado herido.
                Option heal = options.Find(o =>
                    o.ability.abilityType == AbilityType.Heal);

                if (heal != null)
                    return heal;

                List<Option> useful = specials.FindAll(AppliesUsefulStatus);

                if (useful.Count > 0 && Random.value < statusPreference)
                    return RandomOf(useful);

                return basic ?? RandomOf(options);
            }

            case AIStyle.Corruption:
            {
                if (Random.value < variation)
                    return RandomOf(options);

                // Estados utiles primero; luego el ataque mas fuerte.
                List<Option> useful = specials.FindAll(AppliesUsefulStatus);

                if (useful.Count > 0)
                    return RandomOf(useful);

                Option strongest = null;

                foreach (Option option in specials)
                {
                    if (option.ability.abilityType != AbilityType.Attack ||
                        !option.ability.dealsDamage)
                        continue;

                    if (strongest == null ||
                        option.ability.basePower > strongest.ability.basePower)
                        strongest = option;
                }

                return strongest ?? basic ?? RandomOf(options);
            }

            default: // Meadow
                if (specials.Count > 0 && Random.value < specialChance)
                    return RandomOf(specials);

                return basic ?? RandomOf(options);
        }
    }

    // Objetivo entre los validos. Para curar, el mas herido; para los
    // estados, uno que aun no los tenga; en el resto, al azar (sin
    // priorizar al de menos vida).
    protected virtual BattleUnit ChooseTarget(Option option, BattleUnit self)
    {
        List<BattleUnit> pool = option.targets;
        AbilityData ability = option.ability;

        if (ability.abilityType == AbilityType.Heal)
        {
            BattleUnit worst = pool[0];

            foreach (BattleUnit target in pool)
            {
                if (MissingHealth(target) > MissingHealth(worst))
                    worst = target;
            }

            return worst;
        }

        if (ability.statusEffects != null && ability.statusEffects.Count > 0)
        {
            List<BattleUnit> lacking = pool.FindAll(t => LacksStatus(t, ability));

            if (lacking.Count > 0)
                pool = lacking;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    // ------------------------------------------------------------
    // Turno
    // ------------------------------------------------------------

    // Realiza como maximo una accion. No termina el turno: lo hace
    // TurnManager, una sola vez, con el resultado.
    public TurnResult PerformTurn()
    {
        BattleUnit self = Unit;

        if (self == null || !self.IsAlive)
            return TurnResult.Passed;

        ResolveReferences();

        string name = self.Data.unitName;

        if (abilitySystem == null || positionManager == null)
        {
            Debug.LogWarning(name + " pasa su turno: faltan referencias de la IA.");
            return TurnResult.Passed;
        }

        List<Option> options = BuildOptions(self);

        // IA v0.2 (experimental): puntuacion tactica. Si no consigue
        // actuar, sigue el flujo normal de reposicionamiento / pasar.
        if (options.Count > 0 && EnemyAISettings.Active == EnemyAIVersion.V02)
        {
            if (TryTacticalAction(self, options))
                return TurnResult.Acted;
        }
        else if (options.Count > 0)
        {
            // IA v0.1. Primero la elegida; si falla, alternativas validas al azar.
            Option chosen = ChooseOption(options, self);
            List<Option> attempts = new List<Option> { chosen };

            List<Option> rest = options.FindAll(o => o != chosen);

            while (rest.Count > 0)
            {
                int index = Random.Range(0, rest.Count);
                attempts.Add(rest[index]);
                rest.RemoveAt(index);
            }

            foreach (Option option in attempts)
            {
                BattleUnit target = ChooseTarget(option, self);

                if (target != null)
                    CaptureBefore(target, option.ability);

                if (target != null &&
                    abilitySystem.ExecuteAbility(self, target, option.ability))
                {
                    NotifyAction(self, option.ability, target);
                    return TurnResult.Acted;
                }
            }
        }

        // Sin habilidad ejecutable: si su ataque no alcanza desde su
        // posicion, intenta acercarse (o alejarse) una posicion. Es su
        // accion del turno.
        if (TryReposition(self))
            return TurnResult.Acted;

        Debug.Log(name + (options.Count == 0
            ? " pasa su turno: no hay objetivos validos."
            : " pasa su turno: no pudo usar ninguna habilidad."));

        return TurnResult.Passed;
    }

    // Movimiento legal de una posicion hacia donde su ataque basico
    // pueda usarse. Solo si desde la posicion actual no puede atacar.
    private bool TryReposition(BattleUnit self)
    {
        if (basicAttack == null || basicAttack.CanUseFromPosition(self.Position))
            return false;

        int direction = 0;

        // Avanzar (-1) si hay posiciones utiles por delante; si no, retroceder.
        for (int position = 1; position < self.Position; position++)
        {
            if (basicAttack.CanUseFromPosition(position))
                direction = -1;
        }

        if (direction == 0)
        {
            for (int position = self.Position + 1; position <= 4; position++)
            {
                if (basicAttack.CanUseFromPosition(position))
                    direction = 1;
            }
        }

        if (direction == 0 || !positionManager.CanMoveUnit(self, direction))
            return false;

        Debug.Log(
            self.Data.unitName + " se mueve una posicion para poder atacar."
        );

        return positionManager.MoveUnit(self, direction);
    }
}
