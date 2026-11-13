using System.Collections.Generic;
using UnityEngine;

// Inventario de reliquias de la tienda del mapa: cuatro espacios, una unidad por espacio. Se renueva solo al completar
// por primera vez cualquier encuentro (combates y eventos obligatorios; no la introduccion), nunca al abrir/cerrar la tienda
// ni al cambiar de escena. Solo se pueden COMPRAR reliquias: no existe venta.
//
// Distribucion segun el avance:
//   inicio y Pradera .......... 4 comunes
//   tras avanzar al Bosque .... 3 comunes + 1 rara
//   desde Corrupcion .......... 2 comunes + 2 raras
// Las reliquias a la venta salen de RelicData.availableInShop (comunes y raras no exclusivas); las raras que solo se consiguen
// como recompensa y las exclusivas tienen availableInShop apagado. En una misma renovacion no se repite una reliquia.
public static class RelicShopStock
{
    public const int Slots = 4;

    public enum Stage
    {
        Start,       // inicio y Pradera
        Forest,      // Bosque
        Corruption   // Corrupcion
    }

    private struct Slot
    {
        public string typeId;
        public bool sold;
    }

    private static readonly List<Slot> slots = new List<Slot>();
    private static bool initialized;

    public static int Rotations { get; private set; }

    // Etapa segun el progreso de la partida: el Bosque se abre al terminar la Pradera (P4); la Corrupcion, tras el
    // informe de Valdoria (el evento que desbloquea C1).
    public static Stage CurrentStage()
    {
        if (GameSession.IsEncounterCompleted(GameSession.ValdoriaEventId) || GameSession.IsEncounterCompleted("C1"))
            return Stage.Corruption;

        if (GameSession.IsEncounterCompleted("P4"))
            return Stage.Forest;

        return Stage.Start;
    }

    public static void Reset()
    {
        slots.Clear();
        initialized = false;
        Rotations = 0;
    }

    // Garantiza que exista un inventario (el primero se genera con el avance actual, sin contar como renovacion).
    public static void EnsureStock()
    {
        if (!initialized)
            Generate();
    }

    // Se llama cuando un encuentro se completa por primera vez.
    public static void OnEncounterCompleted()
    {
        Rotations++;
        Generate();
    }

    private static void Generate()
    {
        slots.Clear();
        initialized = true;
        RelicCatalog catalog = RelicCatalog.Instance;

        if (catalog == null)
            return;

        int rares = CurrentStage() == Stage.Corruption ? 2 : CurrentStage() == Stage.Forest ? 1 : 0;
        List<RelicData> commonPool = Shuffled(catalog, RelicRarity.Common);
        List<RelicData> rarePool = Shuffled(catalog, RelicRarity.Rare);
        rares = Mathf.Min(rares, rarePool.Count);
        int commons = Slots - rares;

        // Si faltaran comunes, se completa con raras (nunca se repite una reliquia en la misma renovacion).
        for (int i = 0; i < commons && i < commonPool.Count; i++)
            slots.Add(new Slot { typeId = commonPool[i].typeId });

        for (int i = 0; i < rares; i++)
            slots.Add(new Slot { typeId = rarePool[i].typeId });

        for (int i = rares; slots.Count < Slots && i < rarePool.Count; i++)
            slots.Add(new Slot { typeId = rarePool[i].typeId });

        Debug.Log("Tienda de reliquias renovada (" + CurrentStage() + "): " + Describe());
    }

    private static List<RelicData> Shuffled(RelicCatalog catalog, RelicRarity rarity)
    {
        List<RelicData> list = new List<RelicData>();

        foreach (RelicData relic in catalog.Relics)
            if (relic != null && relic.availableInShop && !relic.IsExclusive && !relic.eventOnly && relic.rarity == rarity)
                list.Add(relic);

        // Fisher-Yates con el generador de recompensas (reproducible en pruebas).
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Mathf.Min(i, Mathf.FloorToInt(RelicRewards.Rng() * (i + 1)));
            RelicData tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }

        return list;
    }

    // Reliquias de los espacios, en orden (incluidas las agotadas).
    public static List<RelicData> Current()
    {
        EnsureStock();
        List<RelicData> list = new List<RelicData>();
        RelicCatalog catalog = RelicCatalog.Instance;

        if (catalog == null)
            return list;

        foreach (Slot slot in slots)
        {
            RelicData relic = catalog.Find(slot.typeId);

            if (relic != null)
                list.Add(relic);
        }

        return list;
    }

    public static bool Contains(string typeId)
    {
        EnsureStock();

        foreach (Slot slot in slots)
            if (slot.typeId == typeId)
                return true;

        return false;
    }

    public static bool IsSold(string typeId)
    {
        EnsureStock();

        foreach (Slot slot in slots)
            if (slot.typeId == typeId)
                return slot.sold;

        return false;
    }

    public static bool MarkSold(string typeId)
    {
        EnsureStock();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].typeId == typeId && !slots[i].sold)
            {
                Slot slot = slots[i];
                slot.sold = true;
                slots[i] = slot;
                return true;
            }
        }

        return false;
    }

    public static string Describe()
    {
        string text = "";

        foreach (Slot slot in slots)
            text += (text == "" ? "" : ", ") + slot.typeId + (slot.sold ? " (agotada)" : "");

        return text;
    }
}
