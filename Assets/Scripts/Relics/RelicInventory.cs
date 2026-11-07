using System;
using System.Collections.Generic;
using UnityEngine;

// Suma de modificadores de reliquias para un personaje.
public struct StatMods
{
    public int health;
    public int damage;
    public int protection;
    public int speed;
    public float crit;   // puntos porcentuales (ya con el tope aplicado)
    public int evasion;  // puntos porcentuales

    public int Get(RelicStat stat)
    {
        switch (stat)
        {
            case RelicStat.Health: return health;
            case RelicStat.Damage: return damage;
            case RelicStat.Protection: return protection;
            case RelicStat.Speed: return speed;
            case RelicStat.Evasion: return evasion;
            default: return Mathf.RoundToInt(crit);
        }
    }
}

// Reliquias de la partida en curso: unidades poseidas y equipamiento. Es estatico y vive en memoria como GameSession
// (se descarta con ella: GameSession.StartNewGame / EndSession). Cada unidad tiene un 'uid' unico, asi una misma
// unidad nunca esta en dos personajes ni se duplica; el tipo (RelicData) se resuelve por typeId en RelicCatalog.
//
// Equipar y desequipar solo pasan por aqui, y los modificadores se calculan siempre desde el equipamiento actual
// (GetMods), nunca se acumulan: no hay valores que se queden al abrir ventanas, cambiar de personaje o recalcular.
public static class RelicInventory
{
    public const int SlotsPerHero = 2;

    // Limite de bonificacion de critico que pueden dar las reliquias en conjunto (puntos porcentuales).
    public const float MaxCritBonus = 10f;

    public struct Item
    {
        public int uid;
        public string typeId;
    }

    private static readonly List<Item> items = new List<Item>();
    private static readonly Dictionary<string, int[]> equipped = new Dictionary<string, int[]>();
    private static int nextUid = 1;

    public static event Action Changed;

    public static IReadOnlyList<Item> Items => items;

    // ------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------

    public static void Clear()
    {
        items.Clear();
        equipped.Clear();
        nextUid = 1;
        Changed?.Invoke();
    }

    // Partida nueva: sin equipamiento y sin reliquias. El conjunto de prueba (una unidad de cada tipo del catalogo)
    // solo existe para desarrollo: requiere activar RelicCatalog.startWithTestSet (desactivado por defecto) y solo se
    // aplica en el editor o en builds de desarrollo, nunca en una partida normal.
    public static void StartNewGame()
    {
        Clear();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RelicCatalog catalog = RelicCatalog.Instance;

        if (catalog == null || !catalog.StartWithTestSet)
            return;

        foreach (RelicData relic in catalog.Relics)
            if (relic != null)
                Add(relic.typeId);
#endif
    }

    // Unidades de un tipo que posee el jugador (incluidas las equipadas: siguen en la lista de unidades).
    public static int CountOwned(string typeId)
    {
        int count = 0;

        foreach (Item item in items)
            if (item.typeId == typeId)
                count++;

        return count;
    }

    // Se puede recibir una unidad de este tipo (hay partida y el tipo existe en el catalogo).
    public static bool CanAdd(string typeId)
    {
        return GameSession.IsActive && RelicCatalog.Instance != null && RelicCatalog.Instance.Find(typeId) != null;
    }

    public static int Add(string typeId)
    {
        if (RelicCatalog.Instance == null || RelicCatalog.Instance.Find(typeId) == null)
            return 0;

        Item item = new Item { uid = nextUid++, typeId = typeId };
        items.Add(item);
        Changed?.Invoke();
        return item.uid;
    }

    // ------------------------------------------------------------
    // Consulta
    // ------------------------------------------------------------

    public static bool TryGetItem(int uid, out Item item)
    {
        foreach (Item candidate in items)
        {
            if (candidate.uid == uid)
            {
                item = candidate;
                return true;
            }
        }

        item = default;
        return false;
    }

    public static RelicData DataOf(int uid)
    {
        return TryGetItem(uid, out Item item) && RelicCatalog.Instance != null
            ? RelicCatalog.Instance.Find(item.typeId)
            : null;
    }

    public static int EquippedUid(string heroId, int slot)
    {
        return equipped.TryGetValue(heroId, out int[] slots) && slot >= 0 && slot < SlotsPerHero ? slots[slot] : 0;
    }

    // Donde esta equipada una unidad (false si esta en el inventario).
    public static bool IsEquipped(int uid, out string heroId, out int slot)
    {
        foreach (KeyValuePair<string, int[]> pair in equipped)
        {
            for (int i = 0; i < pair.Value.Length; i++)
            {
                if (pair.Value[i] == uid)
                {
                    heroId = pair.Key;
                    slot = i;
                    return true;
                }
            }
        }

        heroId = null;
        slot = -1;
        return false;
    }

    public static bool IsEquipped(int uid)
    {
        return IsEquipped(uid, out _, out _);
    }

    // ------------------------------------------------------------
    // Validacion y cambios
    // ------------------------------------------------------------

    // Comprueba si 'uid' puede quedar en la casilla indicada. No cambia nada.
    public static bool CanEquip(int uid, string heroId, int slot, out string reason)
    {
        reason = "";

        if (!GameSession.IsActive)
        {
            reason = "No hay una partida activa.";
            return false;
        }

        if (GameSession.InCombat)
        {
            reason = "No se pueden cambiar reliquias durante un combate.";
            return false;
        }

        if (string.IsNullOrEmpty(heroId) || slot < 0 || slot >= SlotsPerHero)
        {
            reason = "Casilla no válida.";
            return false;
        }

        RelicData data = DataOf(uid);

        if (data == null)
        {
            reason = "La reliquia no existe.";
            return false;
        }

        if (!data.CanBeUsedBy(heroId))
        {
            reason = "Reliquia exclusiva de otro personaje.";
            return false;
        }

        if (IsEquipped(uid, out string owner, out _) && owner != heroId)
        {
            reason = "Ya está equipada por otro personaje.";
            return false;
        }

        // Un personaje no puede llevar dos reliquias del mismo tipo (se compara el identificador del tipo, no el
        // nombre). Se ignoran la casilla que se va a ocupar (su reliquia vuelve al inventario) y la propia unidad si
        // solo cambia de casilla. Dos personajes distintos si pueden llevar el mismo tipo (una unidad cada uno).
        if (equipped.TryGetValue(heroId, out int[] heroSlots))
        {
            for (int i = 0; i < heroSlots.Length; i++)
            {
                if (i == slot || heroSlots[i] == 0 || heroSlots[i] == uid)
                    continue;

                RelicData other = DataOf(heroSlots[i]);

                if (other != null && other.typeId == data.typeId)
                {
                    reason = "Este personaje ya lleva una reliquia de este tipo.";
                    return false;
                }
            }
        }

        return true;
    }

    // Retira de forma segura los duplicados que pudieran estar equipados (dos reliquias del mismo tipo en un mismo
    // personaje): se conserva la de la casilla mas baja y la otra queda sin equipar, de vuelta en el inventario (las
    // unidades poseidas no se tocan, asi que no se pierde ningun objeto). Devuelve cuantas se retiraron.
    public static int RemoveDuplicateEquipment()
    {
        int removed = 0;

        foreach (KeyValuePair<string, int[]> pair in equipped)
        {
            int[] slots = pair.Value;

            for (int i = 0; i < slots.Length; i++)
            {
                RelicData first = slots[i] != 0 ? DataOf(slots[i]) : null;

                if (first == null)
                    continue;

                for (int j = i + 1; j < slots.Length; j++)
                {
                    RelicData second = slots[j] != 0 ? DataOf(slots[j]) : null;

                    if (second != null && second.typeId == first.typeId)
                    {
                        slots[j] = 0;
                        removed++;
                    }
                }
            }
        }

        if (removed > 0)
            Changed?.Invoke();

        return removed;
    }

    private static bool HasDuplicateEquipment(string heroId)
    {
        if (!equipped.TryGetValue(heroId, out int[] slots))
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            RelicData first = slots[i] != 0 ? DataOf(slots[i]) : null;

            for (int j = i + 1; first != null && j < slots.Length; j++)
            {
                RelicData second = slots[j] != 0 ? DataOf(slots[j]) : null;

                if (second != null && second.typeId == first.typeId)
                    return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    // Solo para pruebas del editor: equipa sin validar (para simular una partida con duplicados ya equipados).
    public static void DebugForceEquip(int uid, string heroId, int slot)
    {
        SlotsOf(heroId)[slot] = uid;
    }
#endif

    // Equipa 'uid' en la casilla. Si la casilla estaba ocupada, esa reliquia vuelve al inventario (o, si 'uid' venia
    // de otra casilla del mismo personaje, ocupa la casilla que quedo libre). Atomico: o se hace todo o no cambia nada.
    public static bool TryEquip(int uid, string heroId, int slot, out string reason)
    {
        if (!CanEquip(uid, heroId, slot, out reason))
            return false;

        int[] slots = SlotsOf(heroId);
        int displaced = slots[slot];

        if (displaced == uid)
            return true;   // ya estaba ahi

        // Si venia de otra casilla del mismo personaje: intercambio.
        if (IsEquipped(uid, out string owner, out int fromSlot) && owner == heroId)
            slots[fromSlot] = displaced;   // el desplazado ocupa la casilla de origen (0 si estaba vacia)

        slots[slot] = uid;
        Changed?.Invoke();
        return true;
    }

    public static bool TryUnequip(string heroId, int slot)
    {
        if (!GameSession.IsActive || GameSession.InCombat || !equipped.TryGetValue(heroId, out int[] slots) ||
            slot < 0 || slot >= SlotsPerHero || slots[slot] == 0)
            return false;

        slots[slot] = 0;
        Changed?.Invoke();
        return true;
    }

    private static int[] SlotsOf(string heroId)
    {
        if (!equipped.TryGetValue(heroId, out int[] slots))
        {
            slots = new int[SlotsPerHero];
            equipped[heroId] = slots;
        }

        return slots;
    }

    // ------------------------------------------------------------
    // Modificadores
    // ------------------------------------------------------------

    // Suma de lo que aportan las reliquias equipadas del personaje (calculada de cero cada vez).
    public static StatMods GetMods(string heroId)
    {
        // Si hubiera duplicados equipados, se devuelven al inventario antes de calcular: el bonus nunca se duplica.
        if (HasDuplicateEquipment(heroId))
            RemoveDuplicateEquipment();

        return Sum(heroId, -1, 0);
    }

    // Igual, como quedaria si 'uid' (0 = vacia) ocupase la casilla 'slot' (y, si venia de otra casilla del mismo
    // personaje, esa quedase con la reliquia desplazada). Sirve para la comparacion previa.
    public static StatMods PreviewMods(string heroId, int slot, int uid)
    {
        return Sum(heroId, slot, uid);
    }

    private static StatMods Sum(string heroId, int overrideSlot, int overrideUid)
    {
        int[] current = equipped.TryGetValue(heroId, out int[] slots) ? (int[])slots.Clone() : new int[SlotsPerHero];

        if (overrideSlot >= 0 && overrideSlot < SlotsPerHero)
        {
            int displaced = current[overrideSlot];

            for (int i = 0; i < SlotsPerHero; i++)
                if (overrideUid != 0 && current[i] == overrideUid && i != overrideSlot)
                    current[i] = displaced;   // intercambio dentro del mismo personaje

            current[overrideSlot] = overrideUid;
        }

        StatMods mods = default;
        float crit = 0f;

        foreach (int uid in current)
        {
            RelicData data = uid != 0 ? DataOf(uid) : null;

            if (data == null)
                continue;

            mods.health += data.NetModifier(RelicStat.Health);
            mods.damage += data.NetModifier(RelicStat.Damage);
            mods.protection += data.NetModifier(RelicStat.Protection);
            mods.speed += data.NetModifier(RelicStat.Speed);
            mods.evasion += data.NetModifier(RelicStat.Evasion);
            crit += data.NetModifier(RelicStat.CritChance);
        }

        // El tope solo limita la bonificacion total de critico (una penalizacion neta no se recorta).
        mods.crit = Mathf.Min(crit, MaxCritBonus);
        return mods;
    }
}
