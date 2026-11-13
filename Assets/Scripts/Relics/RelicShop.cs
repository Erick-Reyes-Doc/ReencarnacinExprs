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

    // ------------------------------------------------------------
    // Venta
    // ------------------------------------------------------------

    // La tienda paga la mitad del precio de la reliquia (minimo 1 Corona).
    public const int SellPercent = 50;

    public static int SellPrice(RelicData data)
    {
        return data != null ? Mathf.Max(1, (Price(data) * SellPercent + 50) / 100) : 0;
    }

    // Las reliquias de evento y las exclusivas de un heroe son irrepetibles: no se pueden vender.
    public static bool CanBeSold(RelicData data)
    {
        return data != null && !data.eventOnly && !data.IsExclusive;
    }

    public enum SellResult { Success, NoSession, InCombat, NotSellable, Equipped, Missing }

    public static SellResult CheckSell(int uid)
    {
        if (!GameSession.IsActive)
            return SellResult.NoSession;

        if (GameSession.InCombat)
            return SellResult.InCombat;

        if (!RelicInventory.TryGetItem(uid, out RelicInventory.Item item))
            return SellResult.Missing;

        if (!CanBeSold(RelicInventory.DataOf(uid)))
            return SellResult.NotSellable;

        return RelicInventory.IsEquipped(uid) ? SellResult.Equipped : SellResult.Success;
    }

    public static SellResult TrySell(int uid, out string message)
    {
        SellResult result = CheckSell(uid);
        RelicData data = RelicInventory.DataOf(uid);
        string name = data != null ? data.displayName : "";
        message = DescribeSell(result, name, SellPrice(data));

        if (result != SellResult.Success)
            return result;

        int price = SellPrice(data);

        if (!RelicInventory.TryRemove(uid))
        {
            message = DescribeSell(SellResult.Missing, name, price);
            return SellResult.Missing;
        }

        GameSession.AddCoins(price);
        GameSession.NotifySaleCompleted();
        return SellResult.Success;
    }

    public static string DescribeSell(SellResult result, string name, int price)
    {
        switch (result)
        {
            case SellResult.Success: return "Vendiste: " + name + " (+" + price + " CR).";
            case SellResult.Equipped: return "Equipada: desequípala en la ficha del personaje para venderla";
            case SellResult.NotSellable: return "Esta reliquia no se puede vender";
            case SellResult.InCombat: return "La tienda no esta disponible durante el combate.";
            case SellResult.NoSession: return "No hay una partida en curso.";
            default: return "Esa reliquia ya no está en tu inventario";
        }
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
