
using UnityEngine;

public class AbilitySystem : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PositionManager positionManager;

    // Comprueba si una habilidad puede utilizarse.
    public bool CanUseAbility(
        BattleUnit user,
        BattleUnit target,
        AbilityData ability
    )
    {
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
        if (!GetCooldowns(user).IsReady(ability))
        {
            Debug.Log(
                ability.abilityName + " esta en cooldown (" +
                GetCooldowns(user).GetRemainingTurns(ability) +
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
        AbilityData ability
    )
    {
        if (ability.statusEffects == null ||
            ability.statusEffects.Count == 0)
            return 0;

        StatusEffectController controller =
            target.GetComponent<StatusEffectController>();

        if (controller == null)
        {
            Debug.LogWarning(
                target.Data.unitName +
                " no tiene StatusEffectController."
            );
            return 0;
        }

        int validAttempts = 0;

        CharacterProgression progression =
            user.GetComponent<CharacterProgression>();

        int userLevel = progression != null ? progression.Level : 1;

        foreach (AbilityStatusEffect effect in ability.statusEffects)
        {
            if (effect == null)
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

            if (controller.ApplyEffect(
                    effect.type, potency, duration, user))
            {
                validAttempts++;

                Debug.Log(
                    target.Data.unitName + " recibe " + effect.type +
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

    // Ejecuta una habilidad previamente validada.
    public bool ExecuteAbility(
        BattleUnit user,
        BattleUnit target,
        AbilityData ability
    )
    {
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
        // true si la habilidad hizo daño, curo o aplico regeneracion.
        bool otherEffect = false;

        // Aplicar el efecto principal de la habilidad.
        if (ability.abilityType == AbilityType.Attack &&
            ability.dealsDamage)
        {
            int damage = DamageCalculator.CalculateDamage(
                user,
                target,
                out bool isCritical
            );

            // La potencia de la habilidad aumenta el daño.
            damage += ability.basePower;

            // Si la habilidad no permite criticos,
            // recalculamos el daño sin ese multiplicador.
            if (!ability.canCritical && isCritical)
            {
                int normalDamage = Mathf.Max(
                    1,
                    user.Attack - target.Defense
                );

                damage = normalDamage + ability.basePower;
                isCritical = false;
            }

            Debug.Log(
                ability.abilityName +
                " causa " + damage + " de daño." +
                (isCritical ? " ¡CRITICO!" : "")
            );

            target.TakeDirectDamage(damage);
            otherEffect = true;
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

            int healAmount = Mathf.Max(0, ability.basePower);

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
                RegenerationEffect regeneration =
                    target.GetComponent<RegenerationEffect>();

                if (regeneration == null)
                {
                    Debug.LogWarning(
                        target.Data.unitName +
                        " no tiene el componente RegenerationEffect."
                    );
                    return false;
                }

                regeneration.ApplyRegeneration(
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

        // Despues del efecto principal, intentar los estados
        // alterados si el objetivo sigue vivo.
        int statusAttempts = 0;

        if (target.IsAlive)
        {
            statusAttempts = ApplyStatusEffects(user, target, ability);
        }

        // Una habilidad sin otro efecto cuyos estados fueron todos
        // rechazados no cuenta como accion: no inicia cooldown.
        if (!otherEffect && statusAttempts == 0)
        {
            Debug.Log(
                ability.abilityName + " no tuvo ningun efecto."
            );
            return false;
        }

        // Accion exitosa: iniciar el cooldown de esta unidad.
        GetCooldowns(user).StartCooldown(ability);

        // Pendiente: desplazamientos.
        return true;
    }


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

        // Comprobar el turno y el estado del usuario.
        if (!user.IsAlive ||
            turnManager.CurrentUnit != user)
        {
            Debug.LogWarning(
                "No es el turno de este personaje."
            );
            return false;
        }

        // Comprobar las caracteristicas de la habilidad.
        if (ability.abilityType != AbilityType.Support ||
            ability.targetType != AbilityTargetType.AllAllies)
        {
            Debug.LogWarning(
                "La habilidad no es de regeneracion grupal."
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

        // Validar la posicion del personaje.
        if (!ability.CanUseFromPosition(user.Position))
        {
            Debug.Log(
                "No puedes usar esta habilidad desde la posicion " +
                user.Position
            );
            return false;
        }

        // Validar los valores de regeneracion.
        if (ability.regenerationPerTurn <= 0 ||
            ability.regenerationDuration <= 0)
        {
            Debug.LogWarning(
                "La regeneracion no esta configurada."
            );
            return false;
        }

        // Validar los componentes antes de aplicar el efecto.
        int affectedUnits = 0;

        for (int position = 1; position <= 4; position++)
        {
            BattleUnit ally = positionManager.GetUnitAtPosition(
                user.Side,
                position
            );

            if (ally == null || !ally.IsAlive)
                continue;

            RegenerationEffect regeneration =
                ally.GetComponent<RegenerationEffect>();

            if (regeneration == null)
            {
                Debug.LogWarning(
                    ally.Data.unitName +
                    " no tiene RegenerationEffect."
                );
                return false;
            }

            affectedUnits++;
        }

        if (affectedUnits == 0)
            return false;

        Debug.Log(
            user.Data.unitName +
            " utiliza " + ability.abilityName +
            " sobre todo su equipo."
        );

        // Aplicar regeneracion a todos los aliados vivos.
        for (int position = 1; position <= 4; position++)
        {
            BattleUnit ally = positionManager.GetUnitAtPosition(
                user.Side,
                position
            );

            if (ally == null || !ally.IsAlive)
                continue;

            RegenerationEffect regeneration =
                ally.GetComponent<RegenerationEffect>();

            // Ya validamos los componentes anteriormente.
            regeneration.ApplyRegeneration(
                ability.regenerationPerTurn,
                ability.regenerationDuration
            );
        }

        Debug.Log(
            "Regeneracion aplicada a " +
            affectedUnits +
            " aliados."
        );

        // Accion exitosa: iniciar el cooldown de esta unidad.
        cooldowns.StartCooldown(ability);

        return true;
    }

}
