using System.Collections.Generic;
using UnityEngine;

// Reliquias que portan algunos enemigos (aleatorias, para variar los combates). Al cargar un encuentro normal, cada enemigo
// tiene una probabilidad de recibir UNA reliquia de una lista curada de reliquias universales; sus bonificaciones y
// desventajas cuentan en sus estadisticas (BattleUnit.UpdateStats) y sus efectos usan los mismos ganchos que las de los
// heroes (RelicEffects.Has). Solo existe durante el combate: no se entregan al derrotarlo ni tocan el inventario del jugador.
//
// Reglas: ningun encuentro con jefe, ni los del prologo (P*) ni el primer combate (B1); como maximo MaxPerEncounter
// portadores por combate. La lista excluye las reliquias cuyo efecto solo tiene sentido para los heroes (consumibles,
// curacion, golpes directos de heroe) y las exclusivas de un personaje.
public static class EnemyRelics
{
    public const float Chance = 0.25f;
    public const int MaxPerEncounter = 2;

    // typeId de las reliquias que pueden portar los enemigos (las 5 del GDD, todas del catalogo). Antes la lista incluia 4 reliquias
    // de solo estadisticas que no estan en el catalogo: nunca se asignaban y gastaban el sorteo (la probabilidad real bajaba del 25 %).
    private static readonly string[] Pool =
    {
        "amuleto_corteza", "broche_vigia", "espina_juramento",                    // efectos defensivos
        "idolo_ultimo_aliento", "reloj_arena_roto"
    };

    private static readonly Dictionary<BattleUnit, RelicData> held = new Dictionary<BattleUnit, RelicData>();

    public static void Clear()
    {
        held.Clear();
    }

    public static IEnumerable<KeyValuePair<BattleUnit, RelicData>> Holders => held;

    public static RelicData Of(BattleUnit unit)
    {
        return unit != null && held.TryGetValue(unit, out RelicData relic) ? relic : null;
    }

    public static bool HasEffect(BattleUnit unit, RelicEffect effect)
    {
        RelicData relic = Of(unit);
        return relic != null && relic.effect == effect && effect != RelicEffect.None;
    }

    // Suma de bonificaciones y desventajas de la reliquia del enemigo (vacio si no porta ninguna).
    public static StatMods GetMods(BattleUnit unit)
    {
        StatMods mods = new StatMods();
        RelicData relic = Of(unit);

        if (relic == null)
            return mods;

        foreach (RelicModifier m in relic.bonuses)
            Add(ref mods, m.stat, m.amount);

        foreach (RelicModifier m in relic.penalties)
            Add(ref mods, m.stat, -m.amount);

        return mods;
    }

    private static void Add(ref StatMods mods, RelicStat stat, int amount)
    {
        switch (stat)
        {
            case RelicStat.Health: mods.health += amount; break;
            case RelicStat.Damage: mods.damage += amount; break;
            case RelicStat.Protection: mods.protection += amount; break;
            case RelicStat.Speed: mods.speed += amount; break;
            case RelicStat.Evasion: mods.evasion += amount; break;
            default: mods.crit += amount; break;
        }
    }

    // Sortea las reliquias del encuentro que se acaba de cargar y recalcula las estadisticas de los portadores.
    public static void AssignForEncounter(EncounterData data, IList<BattleUnit> enemies)
    {
        held.Clear();

        if (data == null || enemies == null || RelicCatalog.Instance == null)
            return;

        string id = data.encounterId ?? "";

        if (id.StartsWith("P") || id == "B1" || id.ToUpperInvariant().Contains("BOSS"))
            return;

        foreach (BattleUnit unit in enemies)
            if (unit == null || unit.Data == null || unit.Data.isBoss)
                return;   // combate de jefe: sin cambios

        int given = 0;

        foreach (BattleUnit unit in enemies)
        {
            if (given >= MaxPerEncounter || Random.value > Chance)
                continue;

            RelicData relic = RelicCatalog.Instance.Find(Pool[Random.Range(0, Pool.Length)]);

            if (relic == null)
                continue;

            held[unit] = relic;
            given++;
            unit.InitializeUnit();   // recalcula estadisticas con la reliquia y deja la vida al maximo
            Debug.Log(unit.gameObject.name + " porta " + relic.displayName + ".");
        }
    }
}
