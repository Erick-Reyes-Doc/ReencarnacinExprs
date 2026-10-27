using System.Collections.Generic;
using UnityEngine;

// Extension tactica exclusiva de los jefes (UnitData.isBoss). La IA v0.2
// la suma a la puntuacion de cada pareja (habilidad, objetivo); para los
// enemigos comunes devuelve siempre 0, asi su comportamiento no cambia.
public partial class EnemyAIController
{
    // Ajuste de puntuacion solo para jefes. Se suma al valor ya calculado
    // por ScorePair; nunca obliga a elegir una opcion concreta.
    private float BossScoreAdjust(
        BattleUnit self, AbilityData ability, BattleUnit target, int maxAttack)
    {
        if (self.Data == null || !self.Data.isBoss)
            return 0f;

        float bonus = 0f;

        // Marcar al protagonista mas valioso: amenaza ofensiva y capacidad
        // de curacion/apoyo (Threat) y vida restante (no merece la pena
        // marcar a quien esta a punto de caer). Si ya esta marcado, el
        // estado figura como no nuevo y ScorePair descarta la opcion.
        if (HasStatus(ability, StatusEffectType.PriorityTarget))
        {
            // Valor esperado real de la marca: el dano extra (+20 %) que el
            // Capitan cobrara de este objetivo durante la ventana de la marca
            // (sus dos siguientes turnos). Ver MarkBenefit.
            bonus += MarkBenefit(self, ability, target) + 2f * Threat(target, maxAttack);
        }

        // Un objetivo ya marcado por este jefe recibe +20 % de dano directo:
        // se refleja en el valor de los ataques con dano y se le da un pequeno
        // empuje para aprovechar la marca, sin obligar a atacar siempre al
        // marcado (si otra opcion puntua mas, la elige).
        if (ability.dealsDamage && IsMarkedBy(target, self))
        {
            StatusEffectInstance mark = target.GetComponent<StatusEffectController>()
                .GetEffect(StatusEffectType.PriorityTarget);

            bonus += ExpectedDamage(self, target, ability) * mark.Potency / 100f + 3f;
        }

        // Perfil de la fase actual (BossPhaseData): mas valor a los ataques con
        // dano y a los objetivos marcados. Solo suma puntos; no fuerza nada.
        BossPhaseData.AiProfile profile = PhaseProfile(self);

        if (profile != null)
        {
            if (ability.dealsDamage)
                bonus += profile.offensiveBonus;

            if (ability.dealsDamage && IsMarkedBy(target, self))
                bonus += profile.markedTargetBonus;
        }

        return bonus;
    }

    // ------------------------------------------------------------
    // Evaluacion tactica de Objetivo Prioritario y Disciplina de Hierro
    // ------------------------------------------------------------

    // Dano esperado del Capitan contra un objetivo, incluyendo la bonificacion
    // de su propia marca (la marca solo cuenta para el que la aplico).
    private static float BossExpectedDamage(BattleUnit self, BattleUnit target, AbilityData ability)
    {
        float damage = ExpectedDamage(self, target, ability);
        StatusEffectController fx = target.GetComponent<StatusEffectController>();
        StatusEffectInstance mark = fx != null ? fx.GetEffect(StatusEffectType.PriorityTarget) : null;

        if (mark != null && mark.Source == self)
            damage *= 1f + mark.Potency / 100f;

        return damage;
    }

    private static int RemainingCooldown(BattleUnit self, AbilityData ability)
    {
        AbilityCooldownController cd = self.GetComponent<AbilityCooldownController>();
        return cd != null ? cd.GetRemainingTurns(ability) : 0;
    }

    // Dano adicional que se espera cobrar con la marca durante su ventana: los
    // dos siguientes turnos del Capitan. Cuenta solo lo que realmente puede
    // golpear al objetivo: el ataque de area si estara listo (o ya preparado),
    // el impacto de Fuego de Supresion si tiene a este objetivo en una de sus
    // areas, y un ataque individual con la probabilidad de que lo elija. El
    // dano esperado ya descuenta defensa, Guardia y reducciones del objetivo.
    private float MarkBenefit(BattleUnit self, AbilityData markAbility, BattleUnit target)
    {
        float potency = 0.2f;

        foreach (AbilityStatusEffect s in markAbility.statusEffects)
            if (s != null && s.type == StatusEffectType.PriorityTarget)
                potency = s.potency / 100f;

        float benefit = 0f;
        float hp = target.CurrentHealth;

        // Ataque de area inmediato (Ultima Orden): golpea a todos.
        AbilityData ultimate = specialAbilities.Find(a =>
            a != null && !a.requiresPreparation && a.abilityType == AbilityType.Attack &&
            a.targetType == AbilityTargetType.AllEnemies && PhaseAllows(self, a));

        if (ultimate != null && RemainingCooldown(self, ultimate) <= 1)
            benefit += potency * ExpectedDamage(self, target, ultimate) * 0.9f;

        // Fuego de Supresion: si estara listo, el impacto llega dentro de la ventana
        // y la IA elegira un area con este objetivo (la marca sube su valor).
        AbilityData fire = specialAbilities.Find(a =>
            a != null && a.requiresPreparation && a.selectableAreas != null &&
            a.selectableAreas.Count > 0 && PhaseAllows(self, a));

        PreparedAbilityController prepared = self.GetComponent<PreparedAbilityController>();
        bool firePending = prepared != null && prepared.HasPreparation;

        if (fire != null && (firePending || RemainingCooldown(self, fire) <= 1))
        {
            float damage = ExpectedDamage(self, target, fire);
            benefit += potency * damage * 0.65f * 0.6f;

            if (damage < hp && damage * (1f + potency) >= hp)
                benefit += 4f;   // la marca convierte el impacto en baja
        }

        // Un ataque individual del Capitan en la ventana (su otra accion suele
        // ser el aviso o la resolucion de Fuego) sobre este objetivo.
        float single = ExpectedDamage(self, target, basicAttack);
        benefit += potency * single * 0.45f;

        if (single < hp && single * (1f + potency) >= hp)
            benefit += 4f;

        return benefit;
    }

    // Valor de Disciplina de Hierro (u otra mejora de DEF propia) en puntos de
    // dano. Compara el dano por ronda que el grupo hace al Capitan con y sin la
    // mejora: lo que ahorra, durante la duracion del beneficio, son rondas extra
    // de vida, cada una con una accion del Capitan (su mejor ataque individual).
    // Si cae antes de poder aprovecharla no tiene valor (se penaliza). Para
    // enemigos comunes o habilidades distintas devuelve 'fallback' sin cambios.
    private float BossSupportValue(BattleUnit self, AbilityData ability, float fallback)
    {
        if (self.Data == null || !self.Data.isBoss || ability.statusEffects == null)
            return fallback;

        AbilityStatusEffect boost = ability.statusEffects.Find(s => s != null && s.type == StatusEffectType.DefenseBoost);

        if (boost == null)
            return fallback;

        List<BattleUnit> foes = Foes(self);

        if (foes.Count == 0)
            return fallback;

        float defense = self.Defense;
        float boosted = Mathf.Round(defense * (100 + boost.potency) / 100f);
        float perRound = 0f;
        float perRoundBoosted = 0f;
        float bestAction = 0f;

        foreach (BattleUnit foe in foes)
        {
            // Poder medio de las habilidades de los protagonistas: +3.
            float attack = foe.Attack + 3f;
            perRound += Mathf.Max(1f, attack - defense);
            perRoundBoosted += Mathf.Max(1f, attack - boosted);

            if (basicAttack != null)
                bestAction = Mathf.Max(bestAction, BossExpectedDamage(self, foe, basicAttack));
        }

        float roundsLeft = self.CurrentHealth / Mathf.Max(1f, perRound);

        // A punto de caer: no llega a aprovecharla.
        if (roundsLeft <= 1.25f)
            return -6f;

        float saved = (perRound - perRoundBoosted) * boost.duration;
        float extraRounds = saved / Mathf.Max(1f, perRound);
        float value = extraRounds * bestAction;

        // Cerca del final cada ronda extra pesa mas (habilidades pendientes).
        if (roundsLeft <= 2.5f)
            value += 3f;

        return value;
    }

    // Ajuste de la fase a las habilidades de apoyo sobre si mismo
    // (p. ej. Disciplina de Hierro). 0 fuera de jefes o sin perfil.
    private float BossSelfSupportAdjust(BattleUnit self)
    {
        BossPhaseData.AiProfile profile = PhaseProfile(self);
        return profile != null ? profile.selfSupportAdjust : 0f;
    }

    private static BossPhaseData.AiProfile PhaseProfile(BattleUnit self)
    {
        if (self.Data == null || !self.Data.isBoss)
            return null;

        BossPhaseController phases = self.GetComponent<BossPhaseController>();
        return phases != null ? phases.AiProfile : null;
    }

    // Habilidades exclusivas de fase: fuera de su fase no se eligen.
    // Sin BossPhaseController (enemigos comunes) siempre esta permitida.
    private static bool PhaseAllows(BattleUnit self, AbilityData ability)
    {
        BossPhaseController phases = self.GetComponent<BossPhaseController>();
        return phases == null || phases.IsAbilityAvailable(ability);
    }

    // ------------------------------------------------------------
    // Ataques preparados por areas (Fuego de Supresion)
    // ------------------------------------------------------------

    // Ultima area anunciada (indice), para no repetir siempre la misma.
    private int lastAnnouncedArea = -1;

    // Devuelve true si el turno queda resuelto aqui (anuncio de un ataque
    // preparado, ataque de area inmediato, o pasar con un ataque ya pendiente).
    // Si devuelve false, la IA sigue como siempre con las demas habilidades.
    // La resolucion del ataque preparado la hace TurnManager al inicio del
    // siguiente turno propio, antes de llamar a la IA.
    private bool TryBossPreparation(BattleUnit self, out TurnResult result)
    {
        result = TurnResult.Passed;

        PreparedAbilityController prepared =
            self.GetComponent<PreparedAbilityController>();

        // Con un ataque pendiente no se elige ninguna otra habilidad.
        if (prepared != null && prepared.HasPreparation)
            return true;

        if (abilitySystem == null || positionManager == null)
            return false;

        // Ataque preparado por areas (Fuego de Supresion) y ataque de area
        // inmediato (Ultima Orden). Ambos respetan las fases y el cooldown.
        AbilityData fire = specialAbilities.Find(a =>
            a != null && a.requiresPreparation &&
            a.selectableAreas != null && a.selectableAreas.Count > 0 &&
            PhaseAllows(self, a));

        AbilityData ultimate = specialAbilities.Find(a =>
            a != null && !a.requiresPreparation &&
            a.abilityType == AbilityType.Attack &&
            a.targetType == AbilityTargetType.AllEnemies &&
            PhaseAllows(self, a));

        AbilityCooldownController cooldowns =
            self.GetComponent<AbilityCooldownController>();

        bool fireReady = fire != null &&
            (cooldowns == null || cooldowns.IsReady(fire)) &&
            fire.CanUseFromPosition(self.Position);

        bool ultimateReady = ultimate != null &&
            (cooldowns == null || cooldowns.IsReady(ultimate)) &&
            ultimate.CanUseFromPosition(self.Position);

        if (!fireReady && !ultimateReady)
            return false;

        List<BattleUnit> foes = Foes(self);

        if (foes.Count == 0)
            return false;

        int maxAttack = 0;

        foreach (BattleUnit foe in foes)
            maxAttack = Mathf.Max(maxAttack, foe.Attack);

        BossPhaseData.AiProfile phaseProfile = PhaseProfile(self);

        // Referencia: el mejor ataque individual disponible.
        float bestSingle = 0f;

        if (basicAttack != null)
        {
            foreach (BattleUnit foe in foes)
                bestSingle = Mathf.Max(bestSingle, BossExpectedDamage(self, foe, basicAttack));
        }

        // Fuego de Supresion: valor esperado de cada area (dano a quienes
        // ocupan hoy sus posiciones, descontando que pueden moverse, esquivar
        // o protegerse durante el aviso). Sin ocupantes el area no es valida.
        float[] areaValues = fireReady ? new float[fire.selectableAreas.Count] : new float[0];
        float fireBest = 0f;

        for (int i = 0; i < areaValues.Length; i++)
        {
            AbilityPositionArea area = fire.selectableAreas[i];

            foreach (BattleUnit foe in foes)
            {
                if (!area.Contains(foe.Position))
                    continue;

                float damage = BossExpectedDamage(self, foe, fire);
                areaValues[i] += damage * 0.65f + (damage >= foe.CurrentHealth ? 4f : 0f);
            }

            fireBest = Mathf.Max(fireBest, areaValues[i]);
        }

        if (fireBest <= 0f)
            fireReady = false;   // ninguna area tiene objetivos validos

        float fireValue = fireReady
            ? fireBest + (phaseProfile != null ? phaseProfile.preparedAttackBias : 0f)
            : 0f;

        // Ultima Orden: golpea a todos de inmediato. Cuenta el dano total
        // esperado (con un descuento por esquiva/critico no garantizado), las
        // posibles bajas y el valor de debilitar a quien aun no lo esta. Un
        // objetivo ya debilitado no suma por el estado.
        float ultimateValue = 0f;

        if (ultimateReady)
        {
            foreach (BattleUnit foe in foes)
            {
                float damage = BossExpectedDamage(self, foe, ultimate);
                ultimateValue += damage * UltimateDamageWeight;

                if (damage >= foe.CurrentHealth)
                    ultimateValue += 4f;

                foreach (AbilityStatusEffect status in ultimate.statusEffects)
                {
                    if (status != null && Lacks(foe, status))
                        ultimateValue += StatusValue(status, foe, maxAttack) * 0.5f;
                }
            }
        }

        // Eleccion ponderada entre seguir con las demas habilidades (valor =
        // mejor ataque individual), preparar Fuego y lanzar Ultima Orden.
        float reference = Mathf.Max(bestSingle, fireReady ? fireValue : 0f, ultimateReady ? ultimateValue : 0f);
        float wNormal = Mathf.Exp((bestSingle - reference) / 6f);
        float wFire = fireReady ? Mathf.Exp((fireValue - reference) / 6f) : 0f;
        float wUltimate = ultimateReady ? Mathf.Exp((ultimateValue - reference) / 6f) : 0f;

        float pick = Random.value * (wNormal + wFire + wUltimate);

        if (pick < wNormal)
            return false;

        if (pick < wNormal + wUltimate)
        {
            if (!abilitySystem.ExecuteAreaAbility(self, ultimate))
                return false;

            NotifyAction(self, ultimate, null);
            result = TurnResult.Acted;
            return true;
        }

        // Area: ponderada por valor, con penalizacion a repetir la anterior.
        float total = 0f;
        float[] weights = new float[areaValues.Length];

        for (int i = 0; i < areaValues.Length; i++)
        {
            weights[i] = areaValues[i] <= 0f
                ? 0f
                : Mathf.Exp((areaValues[i] - fireBest) / 5f) * (i == lastAnnouncedArea ? 0.5f : 1f);
            total += weights[i];
        }

        float roll = Random.value * total;
        int chosen = -1;

        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0f)
                continue;

            chosen = i;
            roll -= weights[i];

            if (roll <= 0f)
                break;
        }

        if (chosen < 0 ||
            !abilitySystem.ExecuteAreaAbility(self, fire, fire.selectableAreas[chosen]))
            return false;

        lastAnnouncedArea = chosen;
        NotifyAction(self, fire, null);
        result = TurnResult.Acted;
        return true;
    }

    // Peso del dano total esperado de Ultima Orden en su valor (el dano de area
    // por objetivo es bajo y no todos los golpes cuentan).
    private const float UltimateDamageWeight = 0.5f;

    private static bool HasStatus(AbilityData ability, StatusEffectType type)
    {
        return ability.statusEffects != null &&
               ability.statusEffects.Exists(s => s != null && s.type == type);
    }

    private static bool IsMarkedBy(BattleUnit target, BattleUnit source)
    {
        StatusEffectController effects = target.GetComponent<StatusEffectController>();
        StatusEffectInstance mark = effects != null
            ? effects.GetEffect(StatusEffectType.PriorityTarget)
            : null;

        return mark != null && mark.Source == source;
    }
}
