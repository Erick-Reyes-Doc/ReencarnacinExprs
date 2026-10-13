using System;
using System.Collections.Generic;
using UnityEngine;

// Administra los estados alterados activos de una unidad.
// Por ahora aplica daño periodico (Bleeding, Burn, Poison);
// el resto de estados solo cuenta duraciones.
public class StatusEffectController : MonoBehaviour
{
    private readonly Dictionary<StatusEffectType, StatusEffectInstance>
        activeEffects =
            new Dictionary<StatusEffectType, StatusEffectInstance>();

    private readonly List<StatusEffectType> expiredBuffer =
        new List<StatusEffectType>();

    private BattleUnit unit;

    public int ActiveCount => activeEffects.Count;

    // true si, tras el ultimo ProcessTurnStart(), la unidad
    // debe perder su accion por Stun.
    public bool SkipsCurrentAction { get; private set; }

    public event Action<StatusEffectInstance> OnEffectApplied;
    public event Action<StatusEffectInstance> OnEffectRemoved;

    // Se dispara por cada efecto activo al inicio del turno,
    // antes de reducir su duracion. Los sistemas futuros
    // (daño periodico, etc.) se suscribiran aqui.
    public event Action<StatusEffectInstance> OnEffectTick;

    public bool HasEffect(StatusEffectType type)
    {
        return activeEffects.ContainsKey(type);
    }

    public StatusEffectInstance GetEffect(StatusEffectType type)
    {
        activeEffects.TryGetValue(type, out StatusEffectInstance effect);
        return effect;
    }

    // Devuelve una copia para poder recorrerla con seguridad.
    public List<StatusEffectInstance> GetActiveEffects()
    {
        return new List<StatusEffectInstance>(activeEffects.Values);
    }

    // Aplica un estado. Devuelve true si se aplico o renovo.
    // - Marca: se rechaza si ya esta activa.
    // - Otros: se conserva la mayor potencia y la mayor duracion.
    public bool ApplyEffect(
        StatusEffectType type,
        int potency,
        int duration,
        BattleUnit source
    )
    {
        if (type == StatusEffectType.None)
        {
            Debug.LogWarning("No se puede aplicar un estado None.");
            return false;
        }

        if (potency < 0 || duration <= 0)
        {
            Debug.LogWarning(
                "Valores invalidos para el estado " + type + "."
            );
            return false;
        }

        // Los jefes son inmunes al aturdimiento.
        if (type == StatusEffectType.Stun &&
            Unit != null && Unit.Data != null && Unit.Data.isBoss)
        {
            Debug.Log(Unit.Data.unitName + " es inmune al aturdimiento.");
            return false;
        }

        if (activeEffects.TryGetValue(
                type, out StatusEffectInstance current))
        {
            // La Marca no se reaplica mientras este activa.
            if (type == StatusEffectType.Mark)
                return false;

            if (potency > current.Potency)
            {
                current.Refresh(
                    potency,
                    Mathf.Max(duration, current.RemainingTurns),
                    source
                );
            }
            else
            {
                // Conserva la potencia (y el origen) actuales.
                current.ExtendDuration(duration);
            }

            return true;
        }

        StatusEffectInstance effect =
            new StatusEffectInstance(type, potency, duration, source);

        activeEffects.Add(type, effect);
        OnEffectApplied?.Invoke(effect);

        return true;
    }

    public bool RemoveEffect(StatusEffectType type)
    {
        if (!activeEffects.TryGetValue(
                type, out StatusEffectInstance effect))
            return false;

        activeEffects.Remove(type);
        OnEffectRemoved?.Invoke(effect);

        return true;
    }

    public void ClearAllEffects()
    {
        foreach (StatusEffectInstance effect in GetActiveEffects())
        {
            RemoveEffect(effect.Type);
        }
    }

    private BattleUnit Unit
    {
        get
        {
            if (unit == null)
                unit = GetComponent<BattleUnit>();

            return unit;
        }
    }

    // Los modificadores tacticos siguen activos durante el turno en
    // que su duracion llega a 0 y se retiran al inicio del siguiente.
    // Asi una duracion N los mantiene durante N turnos completos del
    // objetivo, y un Debilitamiento de duracion 1 afecta su accion.
    private static bool DefersExpiry(StatusEffectType type)
    {
        return type == StatusEffectType.Mark ||
               type == StatusEffectType.Weaken ||
               type == StatusEffectType.Vulnerability;
    }

    // Potencia del estado activo, 0 si no lo tiene.
    public int GetPotency(StatusEffectType type)
    {
        StatusEffectInstance effect = GetEffect(type);
        return effect == null ? 0 : effect.Potency;
    }

    private static bool IsPeriodicDamage(StatusEffectType type)
    {
        return type == StatusEffectType.Bleeding ||
               type == StatusEffectType.Burn ||
               type == StatusEffectType.Poison;
    }

    // Procesa los estados al inicio del turno de la unidad:
    // aplica el daño periodico, avisa de cada efecto, reduce
    // su duracion y elimina los vencidos. El ultimo tick ocurre
    // antes de que el efecto expire.
    // Devuelve false si la unidad murio durante el proceso.
    public bool ProcessTurnStart()
    {
        BattleUnit target = Unit;

        SkipsCurrentAction = false;

        if (target != null && !target.IsAlive)
            return false;

        // Retira los modificadores cuya duracion termino en el
        // turno anterior (ver DefersExpiry).
        foreach (StatusEffectInstance lingering in GetActiveEffects())
        {
            if (lingering.IsExpired)
                RemoveEffect(lingering.Type);
        }

        // Se decide antes de reducir duraciones, para que un Stun
        // de duracion 1 cuente aunque expire en este mismo proceso.
        bool stunned = HasEffect(StatusEffectType.Stun);

        if (activeEffects.Count == 0)
            return true;

        expiredBuffer.Clear();

        // Se recorre una copia: la coleccion original puede
        // cambiar por los eventos que se disparan.
        foreach (StatusEffectInstance effect in GetActiveEffects())
        {
            if (IsPeriodicDamage(effect.Type) && target != null)
            {
                Debug.Log(
                    target.Data.unitName + " sufre " +
                    effect.Potency + " de daño por " + effect.Type + "."
                );

                // Dispara OnDefeated si la unidad cae.
                target.TakeDamage(effect.Potency);

                if (!target.IsAlive)
                {
                    // Los efectos restantes ya no se procesan.
                    ClearAllEffects();
                    return false;
                }
            }

            OnEffectTick?.Invoke(effect);

            effect.ReduceDuration();

            if (effect.IsExpired && !DefersExpiry(effect.Type))
                expiredBuffer.Add(effect.Type);
        }

        foreach (StatusEffectType type in expiredBuffer)
        {
            RemoveEffect(type);
        }

        // Solo pierde la accion si sobrevivio al daño periodico.
        SkipsCurrentAction = stunned;

        return true;
    }
}
