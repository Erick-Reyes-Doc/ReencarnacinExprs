using System.Collections.Generic;
using UnityEngine;

// Sesion de juego en memoria. Es una clase estatica (no un objeto de
// escena), asi que no puede duplicarse al cambiar de escena. Guarda el
// progreso de la partida en curso y se descarta por completo con
// EndSession(): al volver al menu o antes de empezar otra partida.
//
// No usa PlayerPrefs ni ScriptableObjects. Esta pensada para crecer con
// monedas, inventario y zonas desbloqueadas.
public static class GameSession
{
    // Valores iniciales de una partida nueva.
    public const int StartingCoins = 50;     // Coronas
    public const int StartingPotions = 1;
    public const int StartingAntidotes = 0;

    // Maximo de unidades de cada consumible.
    public const int MaxPerItem = 5;

    // Existencias de la tienda por producto al empezar una partida. No hay reposicion automatica y
    // son independientes del inventario del jugador.
    public const int StartingShopStock = 3;

    public enum Consumable
    {
        Potion,
        Antidote
    }

    public enum PurchaseResult
    {
        Success,
        NoSession,
        InsufficientFunds,
        InventoryFull,
        OutOfStock
    }

    // Progreso de un protagonista.
    public struct PartyMemberState
    {
        public string id;
        public int level;
        public int experience;
        public int health;
        public int maxHealth;
        public bool transformed;
        public bool hasCurableStatus;
    }

    // true mientras la escena activa es un combate. La tienda no puede
    // abrirse en ese caso.
    public static bool InCombat { get; private set; }

    public static void SetInCombat(bool value)
    {
        InCombat = value;
    }

    // Inicia una sesion de desarrollo si se abre una escena sin pasar
    // por el menu (editor).
    public static void EnsureSession()
    {
        if (!IsActive)
        {
            Debug.Log("Sin sesion activa: se inicia una de desarrollo.");
            StartNewGame();
        }
    }

    public static int GetCount(Consumable type)
    {
        return type == Consumable.Potion ? Potions : Antidotes;
    }

    // Marca de existencias ilimitadas (las pociones de curacion siempre estan a la venta).
    public const int UnlimitedStock = int.MaxValue;

    // Existencias que le quedan a la tienda de este producto en la partida actual. Las pociones son ilimitadas: solo las
    // frena el limite de inventario (MaxPerItem) y las Coronas.
    public static int GetShopStock(Consumable type)
    {
        return type == Consumable.Potion ? UnlimitedStock : AntidoteShopStock;
    }

    // Texto de existencias para la tienda: "Disponibles: Ilimitadas" (pociones), "Disponibles: N" o "Agotado".
    public static string ShopStockLabel(Consumable type)
    {
        int stock = GetShopStock(type);

        if (stock == UnlimitedStock)
            return "Disponibles: Ilimitadas";

        return stock > 0 ? "Disponibles: " + stock : "Agotado";
    }

    // ------------------------------------------------------------
    // Coronas
    // ------------------------------------------------------------

    // Suma Coronas (solo cantidades positivas y con sesion activa).
    public static bool AddCoins(int amount)
    {
        if (!IsActive || amount <= 0)
            return false;

        Coins += amount;
        return true;
    }

    // Gasta Coronas (con sesion y fondos suficientes). Lo usan las compras que no son de consumibles.
    public static bool TrySpendCoins(int amount)
    {
        if (!IsActive || amount < 0 || Coins < amount)
            return false;

        Coins -= amount;
        return true;
    }

    // ------------------------------------------------------------
    // Inventario
    // ------------------------------------------------------------

    // Compra una unidad como una sola operacion: se comprueba todo
    // antes de descontar las Coronas y anadir el objeto.
    public static PurchaseResult TryBuy(Consumable type, int price)
    {
        if (!IsActive || price < 0)
            return PurchaseResult.NoSession;

        if (GetCount(type) >= MaxPerItem)
            return PurchaseResult.InventoryFull;

        if (GetShopStock(type) <= 0)
            return PurchaseResult.OutOfStock;

        if (Coins < price)
            return PurchaseResult.InsufficientFunds;

        // Todo se comprobo antes: una compra rechazada no descuenta Coronas ni existencias.
        Coins -= price;

        if (type == Consumable.Potion)
        {
            Potions++;   // las pociones no gastan existencias de la tienda
        }
        else
        {
            Antidotes++;
            AntidoteShopStock--;
        }

        return PurchaseResult.Success;
    }

    // Gasta una unidad. Falla si no queda ninguna.
    public static bool TryConsume(Consumable type)
    {
        if (!IsActive || GetCount(type) <= 0)
            return false;

        if (type == Consumable.Potion)
            Potions--;
        else
            Antidotes--;

        return true;
    }

    // Actualiza la vida de un protagonista en la foto (uso de objetos
    // fuera del combate).
    public static bool TryUpdateMemberHealth(string id, int health)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i].id != id)
                continue;

            PartyMemberState state = party[i];
            state.health = Mathf.Clamp(health, 1, state.maxHealth);
            party[i] = state;
            return true;
        }

        return false;
    }

    // Cambia la vida maxima de un protagonista en la foto (p. ej. al equipar una reliquia en el mapa). La vida actual
    // no sube sola; solo se recorta si supera el nuevo maximo.
    public static bool TryUpdateMemberMaxHealth(string id, int maxHealth)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i].id != id)
                continue;

            PartyMemberState state = party[i];
            state.maxHealth = Mathf.Max(1, maxHealth);
            state.health = Mathf.Clamp(state.health, 1, state.maxHealth);
            party[i] = state;
            return true;
        }

        return false;
    }

    public static bool TryClearCurableStatus(string id)
    {
        for (int i = 0; i < party.Count; i++)
        {
            if (party[i].id != id)
                continue;

            PartyMemberState state = party[i];
            state.hasCurableStatus = false;
            party[i] = state;
            return true;
        }

        return false;
    }

    // Encuentro elegido en el mapa que el combate debe cargar.
    public static string PendingEncounterId { get; private set; }

    public static bool SetPendingEncounter(string encounterId)
    {
        if (!IsActive || string.IsNullOrEmpty(encounterId))
            return false;

        PendingEncounterId = encounterId;
        return true;
    }

    // Devuelve el encuentro pendiente y lo olvida (se usa una sola vez).
    public static string TakePendingEncounter()
    {
        string id = PendingEncounterId;
        PendingEncounterId = null;
        return id;
    }

    public static bool IsActive { get; private set; }

    // El equipo fue derrotado: la partida no puede continuar.
    public static bool IsDefeated { get; private set; }

    public static int Coins { get; private set; }
    public static int Potions { get; private set; }
    public static int Antidotes { get; private set; }

    private static int AntidoteShopStock { get; set; }

    // Encuentros superados en esta partida.
    private static readonly HashSet<string> completedEncounters =
        new HashSet<string>();

    // Zonas desbloqueadas (reservado para el mapa).
    private static readonly HashSet<string> unlockedZones =
        new HashSet<string>();

    private static readonly List<PartyMemberState> party =
        new List<PartyMemberState>();

    public static bool HasPartySnapshot => party.Count > 0;

    public static IReadOnlyList<PartyMemberState> Party => party;

    public static int CompletedEncounterCount => completedEncounters.Count;

    // Evita que un estado antiguo sobreviva si el editor no recarga el
    // dominio al entrar en Play Mode.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        EndSession();
    }

    // Empieza una partida nueva desde cero, descartando la anterior.
    public static void StartNewGame()
    {
        EndSession();

        IsActive = true;
        Coins = StartingCoins;
        Potions = StartingPotions;
        Antidotes = StartingAntidotes;
        AntidoteShopStock = StartingShopStock;

        RelicInventory.StartNewGame();   // reliquias de prueba, sin equipar

        Debug.Log("Nueva partida: sesion iniciada desde cero.");
    }

    // Descarta toda la sesion.
    public static void EndSession()
    {
        IsActive = false;
        IsDefeated = false;
        InCombat = false;
        PendingEncounterId = null;
        Coins = 0;
        Potions = 0;
        Antidotes = 0;
        AntidoteShopStock = 0;

        completedEncounters.Clear();
        unlockedZones.Clear();
        party.Clear();
        RelicInventory.Clear();
    }

    public static void MarkDefeated()
    {
        if (IsActive)
            IsDefeated = true;
    }

    public static void SavePartySnapshot(IEnumerable<PartyMemberState> members)
    {
        party.Clear();
        party.AddRange(members);
    }

    public static bool TryGetMember(string id, out PartyMemberState state)
    {
        foreach (PartyMemberState member in party)
        {
            if (member.id == id)
            {
                state = member;
                return true;
            }
        }

        state = default;
        return false;
    }

    public static void MarkEncounterCompleted(string encounterId)
    {
        if (IsActive && !string.IsNullOrEmpty(encounterId))
            completedEncounters.Add(encounterId);
    }

    public static bool IsEncounterCompleted(string encounterId)
    {
        return completedEncounters.Contains(encounterId);
    }

    // Evento "La mascara rota": libera la etapa divina de Noelle. Solo
    // cuenta en la sesion; el combate la aplica al restaurar el equipo
    // (CombatSessionBinder -> CharacterAbilityLoadout.SetTransformed).
    public const string NoelleEventId = "EVT_NOELLE";
    public const string NoelleId = "PlayerNoelle";

    public static bool IsNoelleLiberated =>
        IsEncounterCompleted(NoelleEventId);

    // Marca el evento como completado y a Noelle como liberada. Solo una
    // vez por partida y solo tras completar B1. No toca nivel, XP ni vida.
    public static bool TryLiberateNoelle()
    {
        if (!IsActive || IsDefeated || IsNoelleLiberated ||
            !IsEncounterCompleted("B1"))
            return false;

        for (int i = 0; i < party.Count; i++)
        {
            if (party[i].id != NoelleId)
                continue;

            PartyMemberState state = party[i];
            state.transformed = true;
            party[i] = state;

            MarkEncounterCompleted(NoelleEventId);
            return true;
        }

        return false;
    }

    public static void UnlockZone(string zoneId)
    {
        if (IsActive && !string.IsNullOrEmpty(zoneId))
            unlockedZones.Add(zoneId);
    }

    public static bool IsZoneUnlocked(string zoneId)
    {
        return unlockedZones.Contains(zoneId);
    }

    // La escena esta en Build Settings (en el editor se consulta la
    // lista de escenas; en una build, CanStreamedLevelBeLoaded).
    public static bool IsSceneInBuild(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

#if UNITY_EDITOR
        foreach (UnityEditor.EditorBuildSettingsScene scene in
                 UnityEditor.EditorBuildSettings.scenes)
        {
            if (scene.enabled &&
                System.IO.Path.GetFileNameWithoutExtension(scene.path) ==
                sceneName)
                return true;
        }

        return false;
#else
        return Application.CanStreamedLevelBeLoaded(sceneName);
#endif
    }
}
