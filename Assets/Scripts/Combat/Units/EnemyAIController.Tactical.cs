using System.Collections.Generic;
using UnityEngine;

// IA v0.2 (experimental, ver EnemyAISettings): puntua cada pareja
// (habilidad, objetivo) entre las opciones que BuildOptions() ya filtro
// por posicion, rango y cooldown, y elige con una muestra ponderada (no
// siempre la mejor), asi no es una IA perfecta. No inventa habilidades ni
// se salta restricciones: AbilitySystem vuelve a validar al ejecutar.
public partial class EnemyAIController
{
    // Parametros por zona. Solo afinan el criterio; no tocan datos base.
    public struct TacticalProfile
    {
        public float temperature;     // mayor = mas imprevisible
        public float randomChance;    // eleccion uniforme entre las legales
        public float woundWeight;     // bonus por objetivo herido
        public float killWeight;      // bonus por poder rematar
        public float threatWeight;    // bonus por amenaza del objetivo
        public float focusPenalty;    // penaliza concentrar siempre al mismo
        public float healBelow;       // curar solo si HP% <= este valor
        public float specialBonus;    // empuje a usar habilidades especiales
    }

    // Indices: 0 Pradera, 1 Bosque, 2 Corrupcion (y jefe). Publico para
    // poder afinar el balance de la IA en pruebas sin recompilar.
    public static readonly TacticalProfile[] Profiles =
    {
        new TacticalProfile
        {
            temperature = 7f, randomChance = 0.20f,
            woundWeight = 1f, killWeight = 2f, threatWeight = 0f,
            focusPenalty = 0f, healBelow = 0.7f, specialBonus = 0f
        },
        // Bosque: foco en heridos/amenazas y mas uso de especiales.
        new TacticalProfile
        {
            temperature = 2.0f, randomChance = 0.06f,
            woundWeight = 5f, killWeight = 4f, threatWeight = 3f,
            focusPenalty = 1.0f, healBelow = 0.7f, specialBonus = 2f
        },
        // Corrupcion: exigente pero ganable (ver simulaciones del Paso 59).
        new TacticalProfile
        {
            temperature = 5.0f, randomChance = 0.12f,
            woundWeight = 2f, killWeight = 1f, threatWeight = 1f,
            focusPenalty = 2.0f, healBelow = 0.6f, specialBonus = 0f
        }
    };

    private static TacticalProfile ProfileFor(EnemyArchetypeData.Zone zone)
    {
        switch (zone)
        {
            case EnemyArchetypeData.Zone.Pradera: return Profiles[0];
            case EnemyArchetypeData.Zone.Bosque: return Profiles[1];
            default: return Profiles[2];   // Corrupcion y jefe
        }
    }

    private EnemyArchetypeData.Zone TacticalZone()
    {
        if (archetype != null)
            return archetype.zone;

        switch (style)
        {
            case AIStyle.Forest: return EnemyArchetypeData.Zone.Bosque;
            case AIStyle.Corruption: return EnemyArchetypeData.Zone.Corrupcion;
            default: return EnemyArchetypeData.Zone.Pradera;
        }
    }

    // Memoria corta de los ultimos objetivos atacados por los enemigos,
    // para no concentrar siempre al mismo protagonista.
    private const int FocusMemory = 4;
    private static readonly Queue<BattleUnit> recentTargets = new Queue<BattleUnit>();

    public static void ResetTactics()
    {
        recentTargets.Clear();
    }

    private static int FocusCount(BattleUnit target)
    {
        int n = 0;

        foreach (BattleUnit t in recentTargets)
            if (t == target) n++;

        return n;
    }

    private static void RememberTarget(BattleUnit target)
    {
        recentTargets.Enqueue(target);

        while (recentTargets.Count > FocusMemory)
            recentTargets.Dequeue();
    }

    private struct Candidate
    {
        public Option option;
        public BattleUnit target;
        public float score;
    }

    // ------------------------------------------------------------
    // Puntuacion
    // ------------------------------------------------------------

    private List<BattleUnit> Foes(BattleUnit self)
    {
        List<BattleUnit> list = new List<BattleUnit>();
        TeamSide side = self.Side == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;

        for (int position = 1; position <= 4; position++)
        {
            BattleUnit u = positionManager.GetUnitAtPosition(side, position);

            if (u != null && u.IsAlive)
                list.Add(u);
        }

        return list;
    }

    // Dano esperado sin critico ni evasion (lectura, no ejecuta nada).
    public static int ExpectedDamage(BattleUnit attacker, BattleUnit target, AbilityData ability)
    {
        int damage = Mathf.Max(
            1,
            attacker.Attack + ability.basePower -
            DamageCalculator.GetEffectiveDefense(target, ability.defensePenetration)
        );

        StatusEffectController effects = target.GetComponent<StatusEffectController>();

        if (effects != null)
        {
            int mark = effects.GetPotency(StatusEffectType.Mark);

            if (mark > 0)
                damage = Mathf.RoundToInt(damage * (100 + mark) / 100f);
        }

        int reduction = target.DamageReductionPercent;

        if (reduction > 0)
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * (100 - reduction) / 100f));

        return damage;
    }

    private static float HealthFraction(BattleUnit u)
    {
        return u.MaxHealth <= 0 ? 1f : (float)u.CurrentHealth / u.MaxHealth;
    }

    // 0..1: ataque relativo del objetivo y si cura/apoya al equipo.
    private static float Threat(BattleUnit target, int maxAttack)
    {
        float offense = maxAttack > 0 ? (float)target.Attack / maxAttack : 0f;
        float value = offense * 0.7f;

        CharacterAbilityLoadout loadout = target.GetComponent<CharacterAbilityLoadout>();

        if (loadout != null)
        {
            foreach (AbilityData a in loadout.Abilities)
            {
                if (a != null && a.abilityType == AbilityType.Heal)
                {
                    value += 0.4f;
                    break;
                }
            }
        }

        return Mathf.Clamp01(value);
    }

    // Valor de aplicar un estado a un objetivo que aun no lo tiene.
    private static float StatusValue(AbilityStatusEffect status, BattleUnit target, int maxAttack)
    {
        float chance = Mathf.Clamp01(status.chance / 100f);
        float hp = HealthFraction(target);
        float offense = maxAttack > 0 ? (float)target.Attack / maxAttack : 0f;
        float value;

        switch (status.type)
        {
            case StatusEffectType.Weaken:
                value = 1.5f + 5f * offense;          // mejor contra ofensivos
                break;

            case StatusEffectType.Vulnerability:
                value = 2f + 3f * hp;                 // mejor contra quien aguanta
                break;

            case StatusEffectType.Bleeding:
            case StatusEffectType.Poison:
            case StatusEffectType.Burn:
                value = Mathf.Min(status.potency * status.duration, target.CurrentHealth) * 0.6f;
                break;

            case StatusEffectType.Stun:
                value = 6f;
                break;

            case StatusEffectType.Mark:
                value = 3f;
                break;

            default:
                value = 2f;
                break;
        }

        return value * chance;
    }

    private static bool Lacks(BattleUnit target, AbilityStatusEffect status)
    {
        StatusEffectController effects = target.GetComponent<StatusEffectController>();
        return effects == null || !effects.HasEffect(status.type);
    }

    private const float Invalid = -1000f;

    private float ScorePair(
        BattleUnit self, Option option, BattleUnit target,
        TacticalProfile profile, int maxAttack, bool allowRedundant)
    {
        AbilityData ability = option.ability;
        float hp = HealthFraction(target);
        bool hasStatus = ability.statusEffects != null && ability.statusEffects.Count > 0;

        // --- Curacion: solo con necesidad real ---
        if (ability.abilityType == AbilityType.Heal)
        {
            if (hp > profile.healBelow)
                return Invalid;

            int missing = target.MaxHealth - target.CurrentHealth;
            int amount = ability.healPercentMaxHealth > 0f
                ? Mathf.RoundToInt(target.MaxHealth * ability.healPercentMaxHealth / 100f)
                : ability.basePower;

            return Mathf.Min(amount, missing) + (1f - hp) * 8f + 6f;
        }

        // --- Apoyo sobre si mismo (p. ej. Corteza / Coraza) ---
        if (ability.abilityType == AbilityType.Support ||
            ability.targetType == AbilityTargetType.Self)
        {
            bool anyNew = false;

            if (hasStatus)
            {
                foreach (AbilityStatusEffect s in ability.statusEffects)
                    if (s != null && Lacks(self, s)) anyNew = true;
            }

            if (!anyNew)
                return Invalid;   // ya activo: no renovar sin beneficio

            float value = 1.5f + (1f - HealthFraction(self)) * 10f;

            if (self.Position <= 2)
                value += 1.5f;    // en primera linea aprovecha mas la defensa

            // Solo jefes con fases (0 para el resto).
            value = BossSupportValue(self, ability, value);
            value += BossSelfSupportAdjust(self);

            return value;
        }

        // --- Ataque (con o sin dano) ---
        float score = ability != basicAttack ? profile.specialBonus : 0f;

        if (ability.dealsDamage)
        {
            int damage = ExpectedDamage(self, target, ability);
            score += damage;

            if (damage >= target.CurrentHealth)
                score += profile.killWeight;
        }

        score += (1f - hp) * profile.woundWeight;
        score += Threat(target, maxAttack) * profile.threatWeight;
        score -= FocusCount(target) * profile.focusPenalty;

        if (hasStatus)
        {
            bool anyNew = false;

            foreach (AbilityStatusEffect s in ability.statusEffects)
            {
                if (s == null)
                    continue;

                if (Lacks(target, s))
                {
                    anyNew = true;
                    score += StatusValue(s, target, maxAttack);
                }
                else
                {
                    score -= 3f;  // redundante: el estado ya esta activo
                }
            }

            // Repetir estados ya activos solo si no queda otra opcion.
            if (!anyNew && !allowRedundant)
                return Invalid;

            // Una habilidad que solo aplica estados no sirve si ya estan.
            if (!ability.dealsDamage && !anyNew)
                return Invalid;
        }

        // Solo jefes (0 para el resto de enemigos).
        score += BossScoreAdjust(self, ability, target, maxAttack);

        return score;
    }

    // ------------------------------------------------------------
    // Decision
    // ------------------------------------------------------------

    // Devuelve true si realizo una accion. Una sola accion por turno.
    private bool TryTacticalAction(BattleUnit self, List<Option> options)
    {
        TacticalProfile profile = ProfileFor(TacticalZone());

        List<BattleUnit> foes = Foes(self);
        int maxAttack = 0;

        foreach (BattleUnit foe in foes)
            maxAttack = Mathf.Max(maxAttack, foe.Attack);

        List<Candidate> candidates = new List<Candidate>();

        // Primero sin repetir estados activos; si no queda ninguna opcion,
        // una segunda pasada los permite (mejor atacar que perder el turno).
        for (int pass = 0; pass < 2 && candidates.Count == 0; pass++)
        {
            foreach (Option option in options)
            {
                foreach (BattleUnit target in option.targets)
                {
                    float score = ScorePair(self, option, target, profile, maxAttack, pass == 1);

                    if (score > Invalid / 2f)
                        candidates.Add(new Candidate { option = option, target = target, score = score });
                }
            }
        }

        if (candidates.Count == 0)
            return false;

        // Eleccion: con poca probabilidad uniforme (error ocasional); si no,
        // muestra ponderada por exp(puntuacion / temperatura).
        int chosen;

        if (Random.value < profile.randomChance)
        {
            chosen = Random.Range(0, candidates.Count);
        }
        else
        {
            float best = float.MinValue;

            foreach (Candidate c in candidates)
                best = Mathf.Max(best, c.score);

            float total = 0f;
            float[] weights = new float[candidates.Count];

            for (int i = 0; i < candidates.Count; i++)
            {
                weights[i] = Mathf.Exp((candidates[i].score - best) / profile.temperature);
                total += weights[i];
            }

            float roll = Random.value * total;
            chosen = candidates.Count - 1;

            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];

                if (roll <= 0f)
                {
                    chosen = i;
                    break;
                }
            }
        }

        // Intentos: la elegida y, si AbilitySystem la rechaza, el resto
        // por puntuacion descendente.
        List<Candidate> attempts = new List<Candidate> { candidates[chosen] };
        candidates.RemoveAt(chosen);
        candidates.Sort((a, b) => b.score.CompareTo(a.score));
        attempts.AddRange(candidates);

        foreach (Candidate attempt in attempts)
        {
            CaptureBefore(attempt.target, attempt.option.ability);

            if (!abilitySystem.ExecuteAbility(self, attempt.target, attempt.option.ability))
                continue;

            if (attempt.option.ability.abilityType != AbilityType.Heal &&
                attempt.target.Side != self.Side)
                RememberTarget(attempt.target);

            NotifyAction(self, attempt.option.ability, attempt.target);
            return true;
        }

        return false;
    }
}
