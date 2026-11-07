
using System.Collections.Generic;
using UnityEngine;

public class AbilitySystem : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PositionManager positionManager;

    // Direccion de PositionManager.MoveUnit: -1 avanzar, +1 retroceder.
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

        // Validar posicion del usuario.
        if (!ability.CanUseFromPosition(user.Position))
        {
            Debug.Log(
                user.Data.unitName +
                " no puede usar " +
                ability.abilityName +
                " desde la posicion " +
                user.Position
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

        // Comprobar que ambos personajes esten vivos.
        if (!user.IsAlive || !target.IsAlive)
        {
            Debug.LogWarning(
                "El usuario o el objetivo esta derrotado."
            );
            return false;
        }

        if (!CanUserAct(user, ability))
            return false;

        // Validar posicion del objetivo.
        if (!ability.CanTargetPosition(target.Position))
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
    private int ApplyStatusEffects(
        BattleUnit user,
        BattleUnit target,
        AbilityData ability,
        bool selfPass = false
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

            if (effect.chance < 100f &&
                Random.Range(0f, 100f) >= effect.chance)
            {
                Debug.Log(
                    effect.type + " no se aplico (probabilidad)."
                );
                validAttempts++;
                continue;
            }

            int potency = effect.GetPotency(userLevel);
            int duration = effect.GetDuration(userLevel);

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
    public int DeliverDirectDamage(BattleUnit attacker, BattleUnit target, int preProtectionDamage, float defensePenetration, out int damageToTarget)
    {
        StatusEffectController protectorFx = StatusEffectController.FindProtectorOf(target);
        StatusEffectInstance guard = protectorFx != null ? protectorFx.GetEffect(StatusEffectType.Guard) : null;
        int interceptPercent = guard != null ? guard.InterceptPercent : 0;
        int toProtector = interceptPercent > 0 ? Mathf.RoundToInt(preProtectionDamage * interceptPercent / 100f) : 0;
        int toTarget = preProtectionDamage - toProtector;

        damageToTarget = toTarget > 0
            ? DamageCalculator.ApplyProtection(toTarget, DamageCalculator.GetEffectiveProtection(target, defensePenetration))
            : 0;

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

            protector.TakeReducedDamage(protectorDamage);
        }
        else
        {
            target.TakeDirectDamage(damageToTarget, attacker);
        }

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

        if (ability.dealsDamage)
        {
            // La evasion se tira una sola vez por objetivo, antes del daño directo (todas las unidades esquivan).
            if (EvasionController.TryEvade(target, ability.attackWeight, -1f, ability.evasionPenetration))
            {
                dodged = true;
                hadEffect = true;

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


                // Reparto entre el objetivo y su protector (Guardia de Trinchera); cada uno aplica su propia PROT.
                int dealt = DeliverDirectDamage(user, target, damage, ability.defensePenetration, out int dealtToTarget);

                Debug.Log(
                    ability.abilityName +
                    " causa " + dealt + " de daño a " +
                    target.Data.unitName + "." +
                    (isCritical ? " ¡CRITICO!" : "")
                );
                hadEffect = true;
            }
        }

        // Los estados ligados al golpe no se aplican si fue esquivado.
        bool blockedByDodge = dodged && ability.statusesRequireHit;

        if (target.IsAlive && !blockedByDodge)
        {
            if (ApplyStatusEffects(user, target, ability) > 0)
                hadEffect = true;
        }

        return hadEffect;
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

            // Curacion fija (Poder) o porcentaje de la vida maxima.
            int healAmount = ability.healMax > 0
                ? Random.Range(ability.healMin, ability.healMax + 1)
                : ability.healPercentMaxHealth > 0f
                    ? Mathf.Max(1, Mathf.RoundToInt(
                        target.MaxHealth * ability.healPercentMaxHealth / 100f))
                    : Mathf.Max(0, ability.basePower);

            if (healAmount <= 0)
            {
                Debug.LogWarning(
                    "La habilidad no tiene poder de curacion."
                );

                return false;
            }

            Debug.Log(
                user.Data.unitName +
                " cura a " +
                target.Data.unitName +
                " por " +
                healAmount +
                " puntos de vida."
            );

            target.Heal(healAmount);
            otherEffect = true;
        }
        else if (ability.abilityType == AbilityType.Support)
        {
            bool hasRegeneration =
                ability.regenerationPerTurn > 0 &&
                ability.regenerationDuration > 0;

            bool hasStatusEffects =
                ability.statusEffects != null &&
                ability.statusEffects.Count > 0;

            if (!hasRegeneration && !hasStatusEffects)
            {
                Debug.LogWarning(
                    "La habilidad no tiene regeneracion ni estados configurados."
                );
                return false;
            }

            if (hasRegeneration)
            {
                GetRegeneration(target).ApplyRegeneration(
                    ability.regenerationPerTurn,
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
            return false;
        }

        // Accion exitosa: movimiento del usuario y cooldown.
        ApplyUserMovement(user, ability);
        GetCooldowns(user).StartCooldown(ability);

        return true;
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

            return true;
        }

        Debug.Log(
            user.Data.unitName + " utiliza " + ability.abilityName +
            " sobre " + targets.Count + " enemigos."
        );

        if (!ApplyAreaHits(user, ability, targets))
        {
            Debug.Log(
                ability.abilityName + " no tuvo ningun efecto."
            );
            return false;
        }

        ApplyUserMovement(user, ability);
        GetCooldowns(user).StartCooldown(ability);

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

        for (int position = 1; position <= 4; position++)
        {
            if (!ability.CanTargetPosition(position))
                continue;

            // Areas por posiciones: solo las del area elegida, leidas en
            // este momento (quien ocupe la posicion al resolver).
            if (area != null && !area.Contains(position))
                continue;

            BattleUnit enemy = positionManager.GetUnitAtPosition(
                enemySide,
                position
            );

            if (enemy != null && enemy.IsAlive)
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

        bool anyEffect = ApplyAreaHits(user, ability, targets);

        if (anyEffect)
            ApplyUserMovement(user, ability);

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
            ability.regenerationPerTurn > 0 &&
            ability.regenerationDuration > 0;

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
                        ability.regenerationPerTurn,
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
