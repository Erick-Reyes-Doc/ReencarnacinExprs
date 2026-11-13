
using System.Collections.Generic;
using UnityEngine;

public class AbilitySystem : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PositionManager positionManager;

    // Direccion de PositionManager.MoveUnit: -1 avanzar, +1 retroceder.
    // Aviso de presentacion: se lanza cuando una habilidad se ejecuta con exito (usuario, objetivo principal, habilidad).
    // Solo lo escuchan componentes visuales (UnitAnimationDriver); no afecta a ninguna regla de combate.
    public static event System.Action<BattleUnit, BattleUnit, AbilityData> AbilityExecuted;

    private static void NotifyAbilityExecuted(BattleUnit user, BattleUnit target, AbilityData ability)
    {
        AbilityExecuted?.Invoke(user, target, ability);
    }

    // Presentacion: un ataque ya validado va a resolverse (Starting) o termino sin efecto (Failed). Permiten al enfoque
    // cinematografico retener los numeros de daño hasta el impacto sin tocar ninguna regla.
    public static event System.Action<BattleUnit, BattleUnit, AbilityData> AbilityStarting;
    public static event System.Action AbilityFailed;

    private static int GetMoveDirection(UserMovement movement)
    {
        switch (movement)
        {
            case UserMovement.Forward: return -1;
            case UserMovement.Backward: return 1;
            default: return 0;
        }
    }

    // Comprueba que el movimiento del usuario (si lo hay) sea posible.
    // Se valida antes de ejecutar para evitar efectos parciales.
    private bool CanUserMove(BattleUnit user, AbilityData ability)
    {
        if (ability.userMovement == UserMovement.None)
            return true;

        // Movimiento opcional: si no es posible, la habilidad se usa igualmente sin moverse.
        if (ability.movementOptional)
            return true;

        if (positionManager == null ||
            !positionManager.CanMoveUnit(
                user, GetMoveDirection(ability.userMovement)))
        {
            Debug.Log(
                user.Data.unitName + " no puede moverse para usar " +
                ability.abilityName + "."
            );
            return false;
        }

        return true;
    }

    private void ApplyUserMovement(BattleUnit user, AbilityData ability)
    {
        if (ability.userMovement == UserMovement.None ||
            positionManager == null || !user.IsAlive)
            return;

        if (ability.movementOptional &&
            !positionManager.CanMoveUnit(user, GetMoveDirection(ability.userMovement)))
            return;

        positionManager.MoveUnit(
            user, GetMoveDirection(ability.userMovement)
        );
    }

    // Comprobaciones comunes al usuario: turno, vida, cooldown,
    // posicion y movimiento.
    private bool CanUserAct(BattleUnit user, AbilityData ability)
    {
        if (!user.IsAlive)
        {
            Debug.LogWarning("El usuario esta derrotado.");
            return false;
        }

        // La preparacion diferida solo esta soportada por los ataques
        // de area (ExecuteAreaAbility); el resto no se ejecuta como
        // ataque instantaneo.
        if (ability.requiresPreparation &&
            ability.targetType != AbilityTargetType.AllEnemies)
        {
            Debug.LogWarning(
                ability.abilityName + " requiere preparacion diferida, " +
                "que solo existe para ataques de area."
            );
            return false;
        }

        // Si la unidad tiene un conjunto de habilidades, solo puede
        // usar las del conjunto activo.
        CharacterAbilityLoadout loadout =
            user.GetComponent<CharacterAbilityLoadout>();

        if (loadout != null && !loadout.HasAbility(ability))
        {
            Debug.Log(
                user.Data.unitName + " no tiene disponible " +
                ability.abilityName + " en su conjunto actual."
            );
            return false;
        }

        // Habilidades exclusivas de una fase de jefe (solo jefes con fases).
        BossPhaseController bossPhases = user.GetComponent<BossPhaseController>();

        if (bossPhases != null && !bossPhases.IsAbilityAvailable(ability))
        {
            Debug.Log(
                ability.abilityName + " no esta disponible en la fase actual de " +
                user.Data.unitName + "."
            );
            return false;
        }

        // Comprobar que sea el turno del usuario.
        if (turnManager == null ||
            turnManager.CurrentUnit != user)
        {
            Debug.LogWarning(
                "No es el turno de este personaje."
            );
            return false;
        }

        // Comprobar que la habilidad no este en cooldown.
        AbilityCooldownController cooldowns = GetCooldowns(user);

        if (!cooldowns.IsReady(ability))
        {
            Debug.Log(
                ability.abilityName + " esta en cooldown (" +
                cooldowns.GetRemainingTurns(ability) +
                " turnos propios)."
            );
            return false;
        }

        // Limite de usos por combate (invocaciones del jefe).
        if (cooldowns.UsesExhausted(ability))
        {
            Debug.Log(ability.abilityName + " ya alcanzo su limite de usos en este combate.");
            return false;
        }

        // Validar posicion del usuario.
        int userSlot = positionManager != null ? positionManager.AbilitySlotOf(user) : user.Position;

        if (!ability.CanUseFromPosition(userSlot))
        {
            Debug.Log(
                user.Data.unitName +
                " no puede usar " +
                ability.abilityName +
                " desde la posicion " +
                userSlot
            );

            return false;
        }

        return CanUserMove(user, ability);
    }

    // Comprueba si una habilidad puede utilizarse.
    public bool CanUseAbility(
        BattleUnit user,
        BattleUnit target,
        AbilityData ability
    )
    {
        // Las habilidades de objetivo propio siempre apuntan al usuario.
        if (ability != null && user != null &&
            ability.targetType == AbilityTargetType.Self)
        {
            target = user;
        }

        // Comprobar referencias.
        if (user == null || target == null || ability == null)
        {
            Debug.LogWarning(
                "Faltan datos para ejecutar la habilidad."
            );
            return false;
        }

        // Comprobar que ambos personajes esten vivos. Excepcion: una habilidad de CURACION puede apuntar a un heroe aliado
        // caido (vida 0): es lo que lo levanta.
        bool healsDownedAlly = ability.abilityType == AbilityType.Heal && target.IsDowned && user.Side == target.Side;

        if (!user.IsAlive || (!target.IsAlive && !healsDownedAlly))
        {
            Debug.LogWarning(
                "El usuario o el objetivo esta derrotado."
            );
            return false;
        }

        if (!CanUserAct(user, ability))
            return false;

        // Validar posicion del objetivo.
        bool targetPositionOk = positionManager != null && target.Side == TeamSide.Player && target.IsAlive
            ? positionManager.IsPlayerUnitInPositions(target, ability.CanTargetPosition)
            : ability.CanTargetPosition(target.Position);

        if (!targetPositionOk)
        {
            Debug.Log(
                "La posicion " +
                target.Position +
                " no es un objetivo valido."
            );

            return false;
        }

        // Validar el equipo objetivo.
        if (ability.abilityType == AbilityType.Attack)
        {
            if (user.Side == target.Side)
            {
                Debug.LogWarning(
                    "No puedes atacar a un aliado."
                );
                return false;
            }
        }
        else
        {
            // Curacion y apoyo: solo aliados.
            if (user.Side != target.Side)
            {
                Debug.LogWarning(
                    "Esta habilidad solo puede usarse en aliados."
                );
                return false;
            }
        }

        return true;
    }

    // Intenta aplicar cada estado de la habilidad. La probabilidad
    // se comprueba por separado y ApplyEffect() decide las reglas
    // propias de cada estado (inmunidad de jefes, Marca, etc.).
    // Devuelve cuantos estados se aplicaron o fallaron por probabilidad
    // (intentos validos). Los rechazados por ApplyEffect() no cuentan.
    // 'referenceDamage' = daño del golpe antes de la proteccion (con critico y bonos), base del daño por turno de los
    // estados con "% del daño" (AbilityStatusEffect.damageScalePercent). -1 = la habilidad no hizo golpe directo: en ese
    // caso se tira un daño de referencia del usuario (con el % de daño y el Poder de la habilidad), una sola vez por aplicacion.
    private int ApplyStatusEffects(
        BattleUnit user,
        BattleUnit target,
        AbilityData ability,
        bool selfPass = false,
        int referenceDamage = -1
    )
    {
        if (ability.statusEffects == null ||
            ability.statusEffects.Count == 0)
            return 0;

        // selfPass = estados con applyToUser (se aplican al usuario); el resto va al objetivo.
        BattleUnit recipient = selfPass ? user : target;

        StatusEffectController controller =
            recipient.GetComponent<StatusEffectController>();

        CharacterProgression progression =
            user.GetComponent<CharacterProgression>();

        int userLevel = progression != null ? progression.Level : 1;
        int validAttempts = 0;

        foreach (AbilityStatusEffect effect in ability.statusEffects)
        {
            if (effect == null)
                continue;

            if (effect.applyToUser != selfPass)
                continue;

            // La regeneracion usa RegenerationEffect, no este sistema.
            if (effect.type == StatusEffectType.Regeneration)
            {
                Debug.LogWarning(
                    "Regeneration se configura con los campos de " +
                    "regeneracion de la habilidad; se ignora aqui."
                );
                continue;
            }

            float chance = effect.chance;

            // Fragmento de la Mascara (antes de La mascara rota): +10 puntos de Aturdimiento en Impacto Quebrantador.
            if (effect.type == StatusEffectType.Stun)
                chance += RelicEffects.StunChanceBonus(user, ability);

            if (chance < 100f &&
                Random.Range(0f, 100f) >= chance)
            {
                Debug.Log(
                    effect.type + " no se aplico (probabilidad)."
                );
                validAttempts++;
                continue;
            }

            int potency = effect.GetPotency(userLevel);
            int duration = effect.GetDuration(userLevel);

            // Sangrado, Quemadura y Veneno con "% del daño": el daño por turno es ese % del daño de referencia (el golpe
            // de esta habilidad o, sin golpe, una tirada del usuario). Se fija al aplicar el estado.
            if (effect.ScalesWithDamage)
            {
                if (referenceDamage < 0)
                {
                    referenceDamage = DamageCalculator.CalculatePreProtection(
                        user,
                        ability.basePower,
                        ability.damagePercent,
                        ability.canCritical,
                        ability.criticalChanceBonus,
                        out bool _
                    );
                }

                potency = effect.PeriodicDamageFrom(referenceDamage);
            }

            // Guard se aplica al usuario; el objetivo (si es otro
            // aliado) pasa a ser el aliado protegido.
            if (effect.type == StatusEffectType.Guard)
            {
                StatusEffectController own =
                    user.GetComponent<StatusEffectController>();

                if (own != null && own.ApplyGuard(
                        potency, duration, user,
                        target != user ? target : null,
                        ability.allyProtectionPercent))
                {
                    validAttempts++;

                    Debug.Log(
                        user.Data.unitName + " adopta Guardia (" +
                        potency + "%, " + duration + " turnos)" +
                        (target != user
                            ? " y protege a " + target.Data.unitName
                            : "") + "."
                    );
                }

                continue;
            }

            if (controller == null)
            {
                Debug.LogWarning(
                    recipient.Data.unitName +
                    " no tiene StatusEffectController."
                );
                continue;
            }

            if (controller.ApplyEffect(
                    effect.type, potency, duration, user))
            {
                validAttempts++;

                Debug.Log(
                    recipient.Data.unitName + " recibe " + effect.type +
                    " (potencia " + potency + ", " +
                    duration + " turnos)."
                );
            }
        }

        return validAttempts;
    }

    // Cooldowns de la unidad. Se crea el componente si falta, para
    // que ninguna ruta de ejecucion pueda saltarse el cooldown.
    private AbilityCooldownController GetCooldowns(BattleUnit unit)
    {
        AbilityCooldownController cooldowns =
            unit.GetComponent<AbilityCooldownController>();

        if (cooldowns == null)
            cooldowns = unit.gameObject
                .AddComponent<AbilityCooldownController>();

        return cooldowns;
    }

    // Entrega un golpe directo (ya con tirada, Poder, critico y bonos, pero SIN proteccion del objetivo).
    // Sin protector: el objetivo aplica su PROT. Con Guardia de Trinchera activa sobre el objetivo: el golpe se reparte
    // (InterceptPercent al protector, el resto al objetivo) y CADA UNO aplica su propia PROT (con sus topes) a la parte que
    // recibe, asi ninguna parte se reduce dos veces. La Marca y Objetivo Prioritario actuan sobre la parte del objetivo.
    // Devuelve el daño total causado (objetivo + protector).
    public int DeliverDirectDamage(BattleUnit attacker, BattleUnit target, int preProtectionDamage, float defensePenetration, out int damageToTarget, int flatBonus = 0)
    {
        StatusEffectController protectorFx = StatusEffectController.FindProtectorOf(target);
        StatusEffectInstance guard = protectorFx != null ? protectorFx.GetEffect(StatusEffectType.Guard) : null;
        int interceptPercent = guard != null ? guard.InterceptPercent : 0;
        int toProtector = interceptPercent > 0 ? Mathf.RoundToInt(preProtectionDamage * interceptPercent / 100f) : 0;
        int toTarget = preProtectionDamage - toProtector;

        damageToTarget = toTarget > 0
            ? DamageCalculator.ApplyProtection(toTarget, DamageCalculator.GetEffectiveProtection(target, defensePenetration))
            : 0;

        // Daño plano extra de reliquias (Colmillo de Lobo, Espejo del Dolor): se suma a la parte que recibe el objetivo.
        if (flatBonus > 0)
            damageToTarget += flatBonus;

        int targetHealthBefore = target.CurrentHealth;
        int protectorDamage = 0;

        if (toProtector > 0)
        {
            BattleUnit protector = protectorFx.GetComponent<BattleUnit>();
            protectorDamage = DamageCalculator.ApplyProtection(toProtector, DamageCalculator.GetEffectiveProtection(protector, defensePenetration));

            Debug.Log(
                protector.Data.unitName + " intercepta " + toProtector + " del golpe (" + protectorDamage +
                " tras su proteccion) dirigido a " + target.Data.unitName + " (" + damageToTarget + " tras la suya)."
            );

            if (damageToTarget > 0)
                target.TakeDirectDamage(damageToTarget, attacker);

            int protectorHealthBefore = protector.CurrentHealth;
            protector.TakeReducedDamage(protectorDamage);

            // El protector tambien recibe un golpe: su Defensa elevada se consume.
            if (protectorDamage > 0)
                protector.ConsumeDefenseBoostOnHit();

            // Insignia de la Trinchera: devuelve parte del daño realmente recibido al atacante.
            int protectorLost = protectorHealthBefore - protector.CurrentHealth;
            RelicEffects.OnIntercept(protector, attacker, protectorLost);
            RelicEffects.OnHitBy(protector, attacker, protectorLost);   // Espina del Juramento del protector
            RelicEffects.OnProtected(protector, target);               // Cadena del Bastion
        }
        else
        {
            target.TakeDirectDamage(damageToTarget, attacker);
        }

        // Espina del Juramento del objetivo (primer daño directo de la ronda).
        RelicEffects.OnHitBy(target, attacker, targetHealthBefore - target.CurrentHealth);

        return damageToTarget + protectorDamage;
    }

    // Resuelve una habilidad ofensiva sobre un objetivo: evasion,
    // daño directo (con penetracion, criticos y Marca) y estados.
    // Devuelve true si hubo algun efecto: daño, esquiva o estados.
    private bool ResolveOffensiveHit(
        BattleUnit user,
        BattleUnit target,
        AbilityData ability
    )
    {
        bool hadEffect = false;
        bool dodged = false;

        // Estado del objetivo antes del golpe (reliquias: Marca, Sangrado, Veneno, Quemadura).
        RelicEffects.PreHit relicPre = RelicEffects.Snapshot(target);

        // Daño del golpe antes de la proteccion (base del daño por turno "% del daño"); -1 si no hubo golpe.
        int hitDamage = -1;

        if (ability.dealsDamage)
        {
            // La evasion se tira una sola vez por objetivo, antes del daño directo (todas las unidades esquivan).
            if (EvasionController.TryEvade(target, ability.attackWeight, -1f, ability.evasionPenetration))
            {
                dodged = true;
                hadEffect = true;
                CombatFeedback.Show(target, "¡Esquivado!", FeedbackKind.Dodge);
                RelicEffects.OnDirectAttackMissed(user);   // Anillo del Segundo Intento

                // Un ataque directo esquivado tambien consume la Marca.
                StatusEffectController dodgedStatuses =
                    target.GetComponent<StatusEffectController>();

                if (dodgedStatuses != null)
                    dodgedStatuses.RemoveEffect(StatusEffectType.Mark);

                Debug.Log(
                    target.Data.unitName + " esquiva " +
                    ability.abilityName + "."
                );
            }
            else
            {
                // Tirada min-max + Poder, critico (solo si impacto) y proteccion %.
                // La Marca y Guard se aplican al recibirlo.
                int damage = DamageCalculator.CalculatePreProtection(
                    user,
                    ability.basePower,
                    ability.damagePercent,
                    ability.canCritical,
                    ability.criticalChanceBonus,
                    out bool isCritical
                );

                // Bono condicional de dano directo (una sola vez por golpe, tras
                // defensa y critico; el dano periodico no pasa por aqui).
                float conditional = ability.DamageMultiplierAgainst(target);

                if (conditional > 1f)
                {
                    damage = Mathf.RoundToInt(damage * conditional);

                    // Consumir el estado que dio el bono (p. ej. Ruptura Ignea consume la Quemadura).
                    if (ability.consumeBonusStatus)
                    {
                        StatusEffectController bonusStatuses = target.GetComponent<StatusEffectController>();

                        if (bonusStatuses != null)
                            bonusStatuses.RemoveEffect(ability.bonusIfTargetHas);
                    }
                }


                hitDamage = damage;

                // Reparto entre el objetivo y su protector (Guardia de Trinchera); cada uno aplica su propia PROT.
                int relicBonus = RelicEffects.DirectHitBonus(user, target, relicPre);
                if (isCritical)
                    CombatFeedback.MarkCritical(target);   // el marcador del daño sera el de un critico

                int dealt = DeliverDirectDamage(user, target, damage, ability.defensePenetration, out int dealtToTarget, relicBonus);
                CombatFeedback.ClearCritical(target);

                Debug.Log(
                    ability.abilityName +
                    " causa " + dealt + " de daño a " +
                    target.Data.unitName + "." +
                    (isCritical ? " ¡CRITICO!" : "")
                );
                hadEffect = true;

                // Reliquias que reaccionan a un golpe directo acertado.
                RelicEffects.OnDirectHitLanded(user, target, relicPre);
            }
        }

        // Los estados ligados al golpe no se aplican si fue esquivado.
        bool blockedByDodge = dodged && ability.statusesRequireHit;

        if (target.IsAlive && !blockedByDodge)
        {
            if (ApplyStatusEffects(user, target, ability, false, hitDamage) > 0)
                hadEffect = true;
        }

        return hadEffect;
    }

    // Cantidad que cura una habilidad de curacion: rango fijo, porcentaje de la vida maxima del objetivo o Poder. Solo la
    // curacion por porcentaje puede ser critica: usa la probabilidad de critico de quien cura (mas el bono de la
    // habilidad) y, si sale, el porcentaje se multiplica por su daño critico (x1,5 por defecto). Los rangos fijos y el
    // Poder no cambian. 'percent' es el porcentaje final aplicado (0 si no es por porcentaje).
    public static int ResolveHeal(
        BattleUnit user,
        BattleUnit target,
        AbilityData ability,
        out bool critical,
        out float percent
    )
    {
        critical = false;
        percent = 0f;

        if (ability.healMax > 0)
            return Random.Range(ability.healMin, ability.healMax + 1);

        if (ability.healPercentMaxHealth > 0f)
        {
            percent = ability.healPercentMaxHealth;
            critical = ability.canCritical &&
                Random.Range(0f, 100f) < user.CriticalChance + ability.criticalChanceBonus;

            if (critical)
                percent *= ability.healCriticalMultiplier > 0f ? ability.healCriticalMultiplier : user.CriticalDamage;

            return Mathf.Max(1, Mathf.RoundToInt(target.MaxHealth * percent / 100f));
        }

        return Mathf.Max(0, ability.basePower);
    }

    // Ejecuta una habilidad previamente validada.
    public bool ExecuteAbility(
        BattleUnit user,
        BattleUnit target,
        AbilityData ability
    )
    {
        if (ability != null && user != null &&
            ability.targetType == AbilityTargetType.Self)
        {
            target = user;
        }

        if (!CanUseAbility(user, target, ability))
        {
            return false;
        }

        Debug.Log(
            user.Data.unitName +
            " utiliza la habilidad: " +
            ability.abilityName +
            " sobre " +
            target.Data.unitName
        );

        if (ability.abilityType == AbilityType.Attack || ability.protectiveAura)
            AbilityStarting?.Invoke(user, target, ability);

        // true si la habilidad hizo daño, curo, esquivo o aplico
        // regeneracion.
        bool otherEffect = false;

        if (ability.abilityType == AbilityType.Attack)
        {
            otherEffect = ResolveOffensiveHit(user, target, ability);
        }
        else if (ability.abilityType == AbilityType.Heal)
        {
            // Evitar curar personajes con la vida completa.
            if (target.CurrentHealth >= target.MaxHealth)
            {
                Debug.Log(
                    target.Data.unitName +
                    " ya tiene toda su vida."
                );

                return false;
            }

            int healAmount = ResolveHeal(user, target, ability, out bool healCritical, out float healPercent);

            if (healAmount <= 0)
            {
                Debug.LogWarning(
                    "La habilidad no tiene poder de curacion."
                );

                return false;
            }

            // Campana del Socorro: HP extra en la primera curacion del turno a un aliado herido.
            healAmount += RelicEffects.HealBonus(user, target);

            Debug.Log(
                user.Data.unitName +
                " cura a " +
                target.Data.unitName +
                " por " +
                healAmount +
                " puntos de vida." +
                (healPercent > 0f ? " (" + healPercent.ToString("0.#") + " % de su vida maxima)" : "") +
                (healCritical ? " ¡CRITICO!" : "")
            );

            target.Heal(healAmount);
            RelicEffects.OnHealApplied(user, target);
            otherEffect = true;
        }
        else if (ability.abilityType == AbilityType.Support)
        {
            bool hasRegeneration =
                ability.HasRegeneration;

            bool hasStatusEffects =
                ability.statusEffects != null &&
                ability.statusEffects.Count > 0;

            if (!hasRegeneration && !hasStatusEffects && !ability.IsSummon)
            {
                Debug.LogWarning(
                    "La habilidad no tiene regeneracion ni estados configurados."
                );
                return false;
            }

            // Invocacion: convoca aliados en las casillas libres. Sin casillas libres no hay accion (no gasta el turno ni la recarga).
            if (ability.IsSummon)
            {
                int summoned = SummonAllies(user, ability);

                if (summoned == 0)
                {
                    Debug.Log(ability.abilityName + " no tuvo efecto: no hay casillas libres.");
                    return false;
                }

                otherEffect = true;
            }

            if (hasRegeneration)
            {
                GetRegeneration(target).ApplyRegeneration(
                    ability.RegenerationFor(target),
                    ability.regenerationDuration
                );

                Debug.Log(
                    user.Data.unitName +
                    " aplica regeneracion a " +
                    target.Data.unitName
                );

                otherEffect = true;
            }
        }

        // Curacion y apoyo aplican sus estados despues del efecto
        // principal. Las ofensivas ya lo hicieron en ResolveOffensiveHit.
        int statusAttempts = 0;

        if (ability.abilityType != AbilityType.Attack &&
            target.IsAlive)
        {
            statusAttempts = ApplyStatusEffects(user, target, ability);
        }

        // Estados que la habilidad se aplica a si misma (applyToUser), tanto si el golpe impacto como si no.
        if (user.IsAlive)
            statusAttempts += ApplyStatusEffects(user, user, ability, true);

        // Una habilidad sin otro efecto cuyos estados fueron todos
        // rechazados no cuenta como accion: no inicia cooldown.
        if (!otherEffect && statusAttempts == 0)
        {
            Debug.Log(
                ability.abilityName + " no tuvo ningun efecto."
            );
            if (ability.abilityType == AbilityType.Attack || ability.protectiveAura)
                AbilityFailed?.Invoke();
            return false;
        }

        // Accion exitosa: movimiento del usuario y cooldown.
        ApplyUserMovement(user, ability);
        GetCooldowns(user).StartCooldown(ability);
        NotifyAbilityExecuted(user, target, ability);

        return true;
    }

    // Casillas de la formacion del usuario donde cabe una unidad nueva (libres y sin reservar por un enemigo grande).
    public List<int> FreeSlots(TeamSide side)
    {
        List<int> free = new List<int>();

        if (positionManager == null)
            return free;

        for (int position = 1; position <= 4; position++)
            if (positionManager.GetUnitOrWideAt(side, position) == null)
                free.Add(position);

        return free;
    }

    // Invoca hasta ability.summonCount unidades en las casillas libres (primero las del frente). Actuan desde la ronda siguiente.
    // Devuelve cuantas aparecieron.
    private int SummonAllies(BattleUnit user, AbilityData ability)
    {
        if (user == null || ability == null || !ability.IsSummon || positionManager == null || turnManager == null)
            return 0;

        int summoned = 0;
        List<int> slots = FreeSlots(user.Side);
        int count = Mathf.Min(ability.summonCount, slots.Count);

        if (count == 0)
            return 0;

        // Los invocados entran por el FRENTE (junto a los heroes): la formacion existente retrocede 'count' casillas. Solo si
        // cabe (formacion compacta, que es lo normal: al caer un secuaz los demas avanzan); si no, ocupan las casillas libres.
        List<BattleUnit> team = new List<BattleUnit>();

        foreach (BattleUnit other in FindObjectsByType<BattleUnit>())
            if (other.Side == user.Side && other.IsAlive && other.gameObject.scene == user.gameObject.scene)
                team.Add(other);

        bool fitsInFront = true;

        foreach (BattleUnit other in team)
            if (other.Position + count + (PositionManager.IsWide(other) ? 1 : 0) > 4)
                fitsInFront = false;

        if (fitsInFront)
        {
            foreach (BattleUnit other in team)
                other.SetPosition(other.Position + count);

            slots = new List<int>();

            for (int i = 1; i <= count; i++)
                slots.Add(i);
        }

        foreach (int position in slots)
        {
            if (summoned >= ability.summonCount)
                break;

            GameObject prefab = ability.RandomSummon();

            if (prefab == null)
                break;

            GameObject go = Instantiate(prefab);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, user.gameObject.scene);
            go.name = prefab.name + " E" + position + " (invocado)";
            go.SetActive(true);

            BattleUnit unit = go.GetComponent<BattleUnit>();
            unit.SetPosition(position);
            unit.InitializeUnit();

            if (!positionManager.RegisterUnit(unit) || !turnManager.AddReinforcement(unit))
            {
                Destroy(go);
                continue;
            }

            summoned++;
            CombatFeedback.Show(unit, "¡Convocado!", FeedbackKind.Buff);
            Debug.Log(user.Data.unitName + " convoca a " + unit.Data.unitName + " en E" + position + " (actuara desde la ronda siguiente).");
        }

        if (summoned > 0)
        {
            positionManager.UpdateVisualPositions();

            // Orden de dibujo estable segun la casilla (el mas cercano a los heroes tapa al siguiente), como al cargar el encuentro.
            foreach (BattleUnit member in FindObjectsByType<BattleUnit>())
            {
                SpriteRenderer body = member.GetComponent<SpriteRenderer>();

                if (member.Side == user.Side && member.IsAlive && body != null && member.gameObject.scene == user.gameObject.scene)
                    body.sortingOrder = Mathf.Max(0, 2 - member.Position);
            }

            foreach (CombatHudController hud in FindObjectsByType<CombatHudController>())
                if (hud.gameObject.scene == user.gameObject.scene)
                    hud.RefreshUnits();
        }

        return summoned;
    }

    // Comprueba (sin ejecutar nada) si un ataque de area inmediato puede usarse ahora: mismas condiciones que ExecuteAreaAbility.
    // Lo usa el enfoque cinematografico para no animar un ataque que va a ser rechazado. Devuelve los objetivos.
    public bool CanUseAreaAbility(BattleUnit user, AbilityData ability, out List<BattleUnit> targets)
    {
        targets = null;

        if (user == null || ability == null || turnManager == null || positionManager == null)
            return false;

        if (ability.abilityType != AbilityType.Attack || ability.targetType != AbilityTargetType.AllEnemies ||
            ability.requiresPreparation || (ability.selectableAreas != null && ability.selectableAreas.Count > 0))
            return false;

        if (!CanUserAct(user, ability))
            return false;

        PreparedAbilityController pending = user.GetComponent<PreparedAbilityController>();

        if (pending != null && pending.HasPreparation)
            return false;

        targets = CollectAreaTargets(user, ability, null);
        return targets.Count > 0;
    }

    // Ataque de area: afecta a todos los enemigos validos con una sola
    // accion y un solo cooldown. Cada objetivo recibe su propia
    // evasion, daño y tiradas de probabilidad.
    // 'area' (opcional) solo se usa con habilidades que definen
    // selectableAreas: fija las posiciones amenazadas al preparar.
    public bool ExecuteAreaAbility(
        BattleUnit user,
        AbilityData ability,
        AbilityPositionArea area = null
    )
    {
        if (user == null || ability == null ||
            turnManager == null || positionManager == null)
        {
            Debug.LogWarning("Faltan referencias.");
            return false;
        }

        if (ability.abilityType != AbilityType.Attack ||
            ability.targetType != AbilityTargetType.AllEnemies)
        {
            Debug.LogWarning(
                "La habilidad no es un ataque de area."
            );
            return false;
        }

        if (!CanUserAct(user, ability))
            return false;

        // Con un ataque preparado pendiente no se lanza otro de area: el
        // turno siguiente se dedica a resolverlo.
        PreparedAbilityController pending =
            user.GetComponent<PreparedAbilityController>();

        if (pending != null && pending.HasPreparation)
        {
            Debug.Log(
                user.Data.unitName + " tiene " +
                pending.PreparedAbility.abilityName + " pendiente."
            );
            return false;
        }

        // Habilidades con areas por posiciones: hay que elegir una de
        // las areas definidas (nunca una inventada).
        if (ability.selectableAreas != null &&
            ability.selectableAreas.Count > 0)
        {
            if (area == null || !ability.selectableAreas.Contains(area))
            {
                Debug.Log(
                    ability.abilityName + " requiere elegir una de sus areas."
                );
                return false;
            }
        }
        else
        {
            area = null;
        }

        List<BattleUnit> targets = CollectAreaTargets(user, ability, area);

        if (targets.Count == 0)
        {
            Debug.Log("No hay objetivos validos para " +
                ability.abilityName + ".");
            return false;
        }

        // Habilidad con preparacion diferida: se anuncia ahora, consume
        // la accion y el cooldown, y el impacto ocurre en el siguiente
        // turno propio (ver ResolvePreparedAbility).
        if (ability.requiresPreparation)
        {
            PreparedAbilityController prepared =
                user.GetComponent<PreparedAbilityController>();

            if (prepared == null)
                prepared = user.gameObject
                    .AddComponent<PreparedAbilityController>();

            if (prepared.HasPreparation)
            {
                Debug.Log(
                    user.Data.unitName + " ya esta preparando " +
                    prepared.PreparedAbility.abilityName + "."
                );
                return false;
            }

            prepared.Prepare(ability, this, area);
            GetCooldowns(user).StartCooldown(ability);

            // Sonido del anuncio (apuntar, cargar...), de fondo como el resto de efectos de habilidades.
            CombatCueSounds.Play(ability.preparationSound, AttackCinematic.AbilitySfxVolume);

            // Animacion del anuncio (el impacto se anima al resolverse).
            if (!string.IsNullOrEmpty(ability.preparationTrigger))
            {
                UnitAnimationDriver announceDriver = user.GetComponent<UnitAnimationDriver>();

                if (announceDriver != null)
                    announceDriver.PlayAttack(ability.preparationTrigger, 1f);
            }

            return true;
        }

        Debug.Log(
            user.Data.unitName + " utiliza " + ability.abilityName +
            " sobre " + targets.Count + " enemigos."
        );

        AbilityStarting?.Invoke(user, targets.Count > 0 ? targets[0] : null, ability);

        if (!ApplyAreaHits(user, ability, targets))
        {
            Debug.Log(
                ability.abilityName + " no tuvo ningun efecto."
            );
            AbilityFailed?.Invoke();
            return false;
        }

        ApplyUserMovement(user, ability);
        GetCooldowns(user).StartCooldown(ability);
        NotifyAbilityExecuted(user, targets.Count > 0 ? targets[0] : null, ability);

        return true;
    }

    // Enemigos vivos alcanzables, en una copia: las derrotas
    // reorganizan posiciones mientras se procesa la habilidad.
    private List<BattleUnit> CollectAreaTargets(
        BattleUnit user,
        AbilityData ability,
        AbilityPositionArea area = null
    )
    {
        TeamSide enemySide = user.Side == TeamSide.Player
            ? TeamSide.Enemy
            : TeamSide.Player;

        List<BattleUnit> targets = new List<BattleUnit>();

        // Heroes: casilla real primero, con respaldo a la posicion compactada (ver PositionManager).
        if (enemySide == TeamSide.Player)
        {
            return positionManager.GetPlayerUnitsAtPositions(
                p => ability.CanTargetPosition(p) && (area == null || area.Contains(p)));
        }


        for (int position = 1; position <= 4; position++)
        {
            if (!ability.CanTargetPosition(position))
                continue;

            // Areas por posiciones: solo las del area elegida, leidas en
            // este momento (quien ocupe la posicion al resolver).
            if (area != null && !area.Contains(position))
                continue;

            // Una unidad grande tambien recibe el area de su casilla extra (pero un solo golpe).
            BattleUnit enemy = positionManager.GetUnitOrWideAt(
                enemySide,
                position
            );

            if (enemy != null && enemy.IsAlive && !targets.Contains(enemy))
                targets.Add(enemy);
        }

        return targets;
    }

    // Resuelve cada golpe por separado. Devuelve true si hubo efecto.
    private bool ApplyAreaHits(
        BattleUnit user,
        AbilityData ability,
        List<BattleUnit> targets
    )
    {
        bool anyEffect = false;

        foreach (BattleUnit target in targets)
        {
            // Si el combate ya termino no se procesan mas golpes.
            if (turnManager.BattleFinished)
                break;

            if (target == null || !target.IsAlive)
                continue;

            if (ResolveOffensiveHit(user, target, ability))
                anyEffect = true;
        }

        return anyEffect;
    }

    // Impacto de una habilidad preparada. Lo llama TurnManager al
    // inicio del siguiente turno propio del usuario. Es la accion de
    // ese turno: NO valida turno ni cooldown ni vuelve a iniciar el
    // cooldown (se inicio al preparar). Devuelve true si impacto.
    public bool ResolvePreparedAbility(
        BattleUnit user,
        AbilityData ability,
        AbilityPositionArea area = null
    )
    {
        if (user == null || ability == null || positionManager == null ||
            turnManager == null || !user.IsAlive)
            return false;

        List<BattleUnit> targets = CollectAreaTargets(user, ability, area);

        if (targets.Count == 0)
        {
            Debug.Log(
                ability.abilityName + " se cancela: no quedan objetivos."
            );
            return false;
        }

        Debug.Log(
            user.Data.unitName + " lanza " + ability.abilityName +
            " sobre " + targets.Count + " enemigos."
        );

        // Enfoque cinematografico + animacion del impacto (mismo camino que un ataque de area inmediato).
        AbilityStarting?.Invoke(user, targets[0], ability);

        bool anyEffect = ApplyAreaHits(user, ability, targets);

        if (anyEffect)
            ApplyUserMovement(user, ability);
        else
            AbilityFailed?.Invoke();

        return anyEffect;
    }

    // RegenerationEffect de la unidad; se agrega si falta, para que
    // las habilidades de regeneracion no fallen por falta de componente.
    private RegenerationEffect GetRegeneration(BattleUnit unit)
    {
        RegenerationEffect regeneration =
            unit.GetComponent<RegenerationEffect>();

        if (regeneration == null)
        {
            regeneration = unit.gameObject
                .AddComponent<RegenerationEffect>();

            Debug.Log(
                "Se agrego RegenerationEffect a " + unit.Data.unitName + "."
            );
        }

        return regeneration;
    }

    // Habilidad de apoyo sobre todo el equipo: regeneracion y/o
    // estados. Mantiene el nombre original por compatibilidad.
    public bool ExecuteGroupRegeneration(
        BattleUnit user,
        AbilityData ability
    )
    {
        // Comprobar referencias.
        if (user == null ||
            ability == null ||
            turnManager == null ||
            positionManager == null)
        {
            Debug.LogWarning("Faltan referencias.");
            return false;
        }

        // Comprobar las caracteristicas de la habilidad.
        if (ability.abilityType != AbilityType.Support ||
            ability.targetType != AbilityTargetType.AllAllies)
        {
            Debug.LogWarning(
                "La habilidad no es de apoyo grupal."
            );
            return false;
        }

        // Turno, cooldown y posicion del personaje.
        if (!CanUserAct(user, ability))
            return false;

        bool hasRegeneration =
            ability.HasRegeneration;

        bool hasStatusEffects =
            ability.statusEffects != null &&
            ability.statusEffects.Count > 0;

        // Validar los valores configurados.
        if (!hasRegeneration && !hasStatusEffects)
        {
            Debug.LogWarning(
                "La habilidad no tiene regeneracion ni estados configurados."
            );
            return false;
        }

        // Contar los aliados vivos afectados.
        int affectedUnits = 0;

        for (int position = 1; position <= 4; position++)
        {
            BattleUnit ally = positionManager.GetUnitAtPosition(
                user.Side,
                position
            );

            if (ally == null || !ally.IsAlive)
                continue;

            affectedUnits++;
        }

        if (affectedUnits == 0)
            return false;

        Debug.Log(
            user.Data.unitName +
            " utiliza " + ability.abilityName +
            " sobre todo su equipo."
        );

        // Aplicar a todos los aliados vivos.
        int statusAttempts = 0;

        for (int position = 1; position <= 4; position++)
        {
            BattleUnit ally = positionManager.GetUnitAtPosition(
                user.Side,
                position
            );

            if (ally == null || !ally.IsAlive)
                continue;

            if (hasRegeneration)
            {
                // El componente se agrega si falta.
                GetRegeneration(ally)
                    .ApplyRegeneration(
                        ability.RegenerationFor(ally),
                        ability.regenerationDuration
                    );
            }

            if (hasStatusEffects)
                statusAttempts += ApplyStatusEffects(user, ally, ability);
        }

        if (!hasRegeneration && statusAttempts == 0)
        {
            Debug.Log(
                ability.abilityName + " no tuvo ningun efecto."
            );
            return false;
        }

        Debug.Log(
            "Efectos aplicados a " +
            affectedUnits +
            " aliados."
        );

        // Accion exitosa: movimiento y cooldown de esta unidad.
        ApplyUserMovement(user, ability);
        GetCooldowns(user).StartCooldown(ability);

        return true;
    }

}
