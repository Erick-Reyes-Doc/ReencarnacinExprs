using System.Collections.Generic;
using UnityEngine;

// Reglas de compra de reliquias en la tienda del mapa. Solo valida y mueve datos: los precios, la disponibilidad y la
// unicidad salen de cada RelicData (availableInShop, shopPrice, uniqueOwnership), las Coronas de GameSession y la
// reliquia comprada entra en RelicInventory, el mismo inventario que usan las fichas de personaje (no se equipa sola).
public static class RelicShop
{
    public enum Result
    {
        Success,
        NoSession,
        InCombat,
        NotForSale,
        AlreadyOwned,
        InsufficientFunds,
        CannotReceive,
        SoldOut
    }

    // Reliquias a la venta ahora: los cuatro espacios del inventario actual (RelicShopStock), en orden e incluidas las
    // agotadas (la tienda las muestra como agotadas hasta la siguiente renovacion).
    public static List<RelicData> Products()
    {
        return RelicShopStock.Current();
    }

    public static int Price(RelicData data)
    {
        return data != null ? Mathf.Max(0, data.shopPrice) : 0;
    }

    // Comprobacion sin cambiar nada.
    public static Result Check(RelicData data)
    {
        if (data == null || !data.availableInShop || data.IsExclusive || data.eventOnly || !RelicShopStock.Contains(data.typeId))
            return Result.NotForSale;

        if (RelicShopStock.IsSold(data.typeId))
            return Result.SoldOut;

        if (!GameSession.IsActive)
            return Result.NoSession;

        if (GameSession.InCombat)
            return Result.InCombat;

        // Unica: cuenta TODAS las unidades poseidas, tambien las equipadas en un personaje.
        if (data.uniqueOwnership && RelicInventory.CountOwned(data.typeId) > 0)
            return Result.AlreadyOwned;

        if (!RelicInventory.CanAdd(data.typeId))
            return Result.CannotReceive;

        if (GameSession.Coins < Price(data))
            return Result.InsufficientFunds;

        return Result.Success;
    }

    // Compra una unidad como una sola operacion: se valida todo antes de descontar Coronas, y si la entrega fallara
    // se devuelven.
    public static Result TryBuy(RelicData data)
    {
        Result result = Check(data);

        if (result != Result.Success)
            return result;

        int price = Price(data);

        if (!GameSession.TrySpendCoins(price))
            return Result.InsufficientFunds;

        if (RelicInventory.Add(data.typeId) == 0)
        {
            if (price > 0)
                GameSession.AddCoins(price);

            return Result.CannotReceive;
        }

        RelicShopStock.MarkSold(data.typeId);   // un solo ejemplar por espacio hasta la siguiente renovacion
        return Result.Success;
    }

    public static string Describe(Result result, RelicData data)
    {
        string name = data != null ? data.displayName : "";

        switch (result)
        {
            case Result.Success: return "Compraste: " + name + " (-" + Price(data) + " CR).";
            case Result.InsufficientFunds: return "Monedas insuficientes";
            case Result.AlreadyOwned: return "Ya poseída";
            case Result.SoldOut: return "Agotada hasta la próxima renovación";
            case Result.InCombat: return "La tienda no esta disponible durante el combate.";
            case Result.NoSession: return "No hay una partida en curso.";
            case Result.NotForSale: return "Esta reliquia no está a la venta.";
            default: return "No se puede recibir la reliquia.";
        }
    }
}
