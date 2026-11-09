using UnityEngine;

// Reglas de los consumibles y de la tienda. Funciona igual dentro y fuera
// del combate: el combate usa unidades (BattleUnit) y el mapa usa la foto
// del equipo guardada en GameSession. Nada se escribe en ScriptableObjects
// ni PlayerPrefs.
public static class ConsumableService
{
    public const int PotionPrice = 35;
    public const int AntidotePrice = 20;

    // % de la vida maxima efectiva que cura la pocion.
    public const int PotionHealPercent = 30;

    public static int Price(GameSession.Consumable type)
    {
        return type == GameSession.Consumable.Potion
            ? PotionPrice
            : AntidotePrice;
    }

    public static string Name(GameSession.Consumable type)
    {
        return type == GameSession.Consumable.Potion
            ? "Poción de curación"
            : "Antídoto";
    }

    // Vida que cura la pocion (redondeo al entero mas cercano, minimo 1).
    public static int PotionHealAmount(int maxHealth)
    {
        return Mathf.Max(1, (maxHealth * PotionHealPercent + 50) / 100);
    }

    // Texto informativo para el HUD. Se genera con las mismas constantes y
    // la misma lista de estados curables que usa TryUse, asi no queda
    // desactualizado. No cambia ninguna regla.
    public static string Description(GameSession.Consumable type)
    {
        if (type == GameSession.Consumable.Potion)
            return "Restaura el " + PotionHealPercent + "% del HP máximo.\nObjetivo: un aliado vivo.";

        string list = "";

        foreach (StatusEffectType status in System.Enum.GetValues(typeof(StatusEffectType)))
        {
            if (!IsCurable(status))
                continue;

            string name = status == StatusEffectType.Bleeding ? "sangrado"
                : status == StatusEffectType.Poison ? "veneno"
                : status == StatusEffectType.Burn ? "quemadura"
                : status.ToString();
            list += (list == "" ? "" : ", ") + name;
        }

        return "Elimina " + list + ".\nObjetivo: un aliado vivo con alguno de esos estados.";
    }

    private static bool IsCurable(StatusEffectType type)
    {
        return type == StatusEffectType.Bleeding ||
               type == StatusEffectType.Poison ||
               type == StatusEffectType.Burn;
    }

    // ------------------------------------------------------------
    // Tienda
    // ------------------------------------------------------------

    // Compra una unidad. No se puede abrir durante un combate.
    public static string Buy(
        GameSession.Consumable type,
        out bool success
    )
    {
        success = false;

        if (GameSession.InCombat)
            return "La tienda no esta disponible durante el combate.";

        GameSession.PurchaseResult result =
            GameSession.TryBuy(type, Price(type));

        switch (result)
        {
            case GameSession.PurchaseResult.Success:
                success = true;
                return "Compraste: " + Name(type) + " (-" + Price(type) +
                       " CR).";

            case GameSession.PurchaseResult.InventoryFull:
                return "Inventario lleno: maximo " +
                       GameSession.MaxPerItem + " de " + Name(type) + ".";

            case GameSession.PurchaseResult.OutOfStock:
                return "Agotado: la tienda ya no tiene " + Name(type) + ".";

            case GameSession.PurchaseResult.InsufficientFunds:
                return "Fondos insuficientes: " + Name(type) + " cuesta " +
                       Price(type) + " CR.";

            default:
                return "No hay una partida en curso.";
        }
    }

    // ------------------------------------------------------------
    // Combate (unidades reales)
    // ------------------------------------------------------------

    // Comprobacion sin consumir nada. 'reason' explica el rechazo.
    public static bool CanUse(
        GameSession.Consumable type,
        BattleUnit target,
        out string reason
    )
    {
        reason = "";

        if (!GameSession.IsActive)
        {
            reason = "No hay una partida en curso.";
            return false;
        }

        if (GameSession.GetCount(type) <= 0)
        {
            reason = "No te quedan " + Name(type) + ".";
            return false;
        }

        if (target == null || !target.IsAlive)
        {
            reason = "El objetivo esta derrotado: los objetos no reviven.";
            return false;
        }

        if (type == GameSession.Consumable.Potion)
        {
            if (target.CurrentHealth >= target.MaxHealth)
            {
                reason = target.Data.unitName + " ya tiene la vida completa.";
                return false;
            }

            return true;
        }

        if (!HasCurableStatus(target))
        {
            reason = target.Data.unitName + " no tiene estados curables.";
            return false;
        }

        return true;
    }

    public static bool HasCurableStatus(BattleUnit target)
    {
        StatusEffectController effects =
            target.GetComponent<StatusEffectController>();

        if (effects == null)
            return false;

        foreach (StatusEffectInstance effect in effects.GetActiveEffects())
        {
            if (IsCurable(effect.Type))
                return true;
        }

        return false;
    }

    // Usa el objeto sobre una unidad. Solo consume el objeto si el uso es
    // valido; el llamador consume la accion del turno si devuelve true.
    // 'user' (opcional) es quien usa el objeto en combate: lo necesitan las reliquias (Campana del Socorro, Botas del
    // Mensajero, Fragmento de la Mascara).
    public static bool TryUse(
        GameSession.Consumable type,
        BattleUnit target,
        out string message,
        BattleUnit user = null
    )
    {
        if (!CanUse(type, target, out message))
            return false;

        if (!GameSession.TryConsume(type))
        {
            message = "No te quedan " + Name(type) + ".";
            return false;
        }

        if (type == GameSession.Consumable.Potion)
        {
            int before = target.CurrentHealth;
            int relicBonus = RelicEffects.HealBonus(user, target);
            target.Heal(PotionHealAmount(target.MaxHealth) + relicBonus);
            RelicEffects.OnHealApplied(user, target);
            RelicEffects.OnConsumableUsed(user);

            message = target.Data.unitName + " usa " + Name(type) +
                      " y recupera " + (target.CurrentHealth - before) +
                      " HP (" + target.CurrentHealth + "/" +
                      target.MaxHealth + ").";
            return true;
        }

        StatusEffectController effects =
            target.GetComponent<StatusEffectController>();

        foreach (StatusEffectInstance effect in effects.GetActiveEffects())
        {
            if (IsCurable(effect.Type))
                effects.RemoveEffect(effect.Type);
        }

        RelicEffects.OnConsumableUsed(user);

        message = target.Data.unitName + " usa " + Name(type) +
                  " y se cura de sangrado, veneno y quemadura.";
        return true;
    }

    // ------------------------------------------------------------
    // Fuera del combate (foto del equipo en la sesion)
    // ------------------------------------------------------------

    public static bool TryUseOnMember(
        GameSession.Consumable type,
        string memberId,
        out string message
    )
    {
        message = "";

        if (GameSession.InCombat)
        {
            message = "Durante el combate los objetos se usan desde el HUD.";
            return false;
        }

        if (!GameSession.IsActive ||
            !GameSession.TryGetMember(memberId,
                out GameSession.PartyMemberState member))
        {
            message = "No hay datos del equipo todavia.";
            return false;
        }

        if (GameSession.GetCount(type) <= 0)
        {
            message = "No te quedan " + Name(type) + ".";
            return false;
        }

        // Los objetos no reviven: la foto siempre tiene vida >= 1.
        if (member.health <= 0)
        {
            message = "Los objetos no pueden revivir aliados.";
            return false;
        }

        if (type == GameSession.Consumable.Potion)
        {
            if (member.health >= member.maxHealth)
            {
                message = "Ya tiene la vida completa.";
                return false;
            }

            if (!GameSession.TryConsume(type))
                return false;

            int healed = Mathf.Min(
                member.maxHealth - member.health,
                PotionHealAmount(member.maxHealth)
            );

            GameSession.TryUpdateMemberHealth(memberId, member.health + healed);

            message = memberId + " recupera " + healed + " HP.";
            return true;
        }

        if (!member.hasCurableStatus)
        {
            message = "No tiene estados curables.";
            return false;
        }

        if (!GameSession.TryConsume(type))
            return false;

        GameSession.TryClearCurableStatus(memberId);
        message = memberId + " se cura de sus estados.";
        return true;
    }
}
