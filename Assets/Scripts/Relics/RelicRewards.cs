using System;
using System.Collections.Generic;
using UnityEngine;

// Reliquias que se obtienen jugando: recompensa aleatoria de la PRIMERA victoria de cada combate y la reliquia unica del
// evento "La mascara rota". Solo decide y entrega (RelicInventory guarda lo poseido); no hay venta de reliquias.
//
// Reglas (Pradera / Bosque / Corrupcion / Jefe):
//   probabilidad de reliquia: 65 % / 80 % / 90 % / 100 %
//   rareza: 100 % comun | 60 % comun-40 % rara | 30 % comun-70 % rara | rara o exclusiva disponible
//   garantia: tras dos combates seguidos sin reliquia, el tercero entrega una (el contador se reinicia al obtener una)
//   desde B2: 20 % de que la recompensa sea una exclusiva disponible (no poseida y que no sea el Fragmento de la Mascara)
//   C2: si aun no se consiguio ninguna exclusiva ademas del Fragmento, se garantiza una exclusiva disponible
// Todo el estado vive en memoria con la sesion (GameSession lo reinicia al empezar o descartar la partida).
public static class RelicRewards
{
    public const string FragmentTypeId = "fragmento_mascara";

    // Recompensa del evento narrativo de Valdoria ("El informe del ultimo vigia"): el Broche del Vigia.
    public const string ValdoriaRewardTypeId = "broche_vigia";
    public const float ExclusiveChance = 0.20f;
    public const int PityThreshold = 2;

    // Origen de los numeros aleatorios (0-1); las pruebas pueden sustituirlo para repetir resultados.
    public static Func<float> Rng = () => UnityEngine.Random.value;

    private static int missesInARow;
    private static readonly HashSet<string> rolled = new HashSet<string>();
    private static bool fragmentGranted;
    private static bool valdoriaGranted;

    // Ultima reliquia entregada por un combate (la pantalla de victoria la muestra); se limpia al empezar cada combate.
    public static RelicData LastReward { get; private set; }

    public static int MissesInARow => missesInARow;
    public static bool FragmentGranted => fragmentGranted;
    public static bool HasRolled(string encounterId) => rolled.Contains(encounterId);

    public static void ResetAll()
    {
        missesInARow = 0;
        rolled.Clear();
        fragmentGranted = false;
        valdoriaGranted = false;
        LastReward = null;
    }

    public static void ClearLast()
    {
        LastReward = null;
    }

    // ------------------------------------------------------------
    // Recompensa aleatoria de combate
    // ------------------------------------------------------------

    // Llamar una sola vez al ganar por primera vez 'encounter'. Devuelve la reliquia entregada (ya en el inventario) o null.
    public static RelicData GrantForEncounter(EncounterData encounter)
    {
        LastReward = null;

        if (encounter == null || RelicCatalog.Instance == null || !GameSession.IsActive)
            return null;

        // Una sola tirada por mision, pase lo que pase (recargar, repetir o perder no da otra oportunidad).
        if (!rolled.Add(encounter.encounterId))
            return null;

        RelicData reward = Roll(encounter);

        if (reward == null)
        {
            missesInARow++;
            Debug.Log("Reliquia: sin recompensa en " + encounter.encounterId + " (combates seguidos sin reliquia: " + missesInARow + ").");
            return null;
        }

        if (RelicInventory.Add(reward.typeId) == 0)
        {
            missesInARow++;
            return null;
        }

        missesInARow = 0;
        LastReward = reward;
        Debug.Log("Reliquia obtenida en " + encounter.encounterId + ": " + reward.displayName + " (" + RelicData.RarityName(reward.rarity) + ").");
        return reward;
    }

    private static RelicData Roll(EncounterData encounter)
    {
        EnemyArchetypeData.Zone zone = encounter.zone;
        bool boss = zone == EnemyArchetypeData.Zone.Jefe;

        // C2: garantia de exclusiva si aun no se consiguio ninguna (ademas del Fragmento).
        if (encounter.encounterId == "C2" && !OwnsAnyDropExclusive())
        {
            RelicData guaranteed = PickExclusive();

            if (guaranteed != null)
                return guaranteed;
        }

        float chance = zone == EnemyArchetypeData.Zone.Pradera ? 0.65f
            : zone == EnemyArchetypeData.Zone.Bosque ? 0.80f
            : zone == EnemyArchetypeData.Zone.Corrupcion ? 0.90f
            : 1f;

        bool success = boss || missesInARow >= PityThreshold || Rng() < chance;

        if (!success)
            return null;

        RelicData relic = null;

        if (boss)
        {
            // Jefe final: rara o exclusiva disponible.
            RelicData exclusive = Rng() < 0.5f ? PickExclusive() : null;
            relic = exclusive != null ? exclusive : PickOfRarity(RelicRarity.Rare);
        }
        else
        {
            if (IsB2OrLater(encounter.encounterId) && Rng() < ExclusiveChance)
                relic = PickExclusive();

            if (relic == null)
            {
                float rareShare = zone == EnemyArchetypeData.Zone.Pradera ? 0f
                    : zone == EnemyArchetypeData.Zone.Bosque ? 0.40f
                    : 0.70f;

                relic = PickOfRarity(Rng() < rareShare ? RelicRarity.Rare : RelicRarity.Common);
            }
        }

        // Sin candidatas de esa rareza: cualquiera del grupo normal.
        if (relic == null)
            relic = PickOfRarity(RelicRarity.Common);

        return relic;
    }

    // B2 y los combates posteriores de la cadena (no los eventos ni el jefe).
    private static bool IsB2OrLater(string encounterId)
    {
        if (MapProgression.IsEvent(encounterId) || encounterId == "BOSS")
            return false;

        int index = MapProgression.IndexOf(encounterId);
        return index >= 0 && index >= MapProgression.IndexOf("B2");
    }

    // ------------------------------------------------------------
    // Grupos de reliquias
    // ------------------------------------------------------------

    private static List<RelicData> Pool(Predicate<RelicData> filter)
    {
        List<RelicData> list = new List<RelicData>();
        RelicCatalog catalog = RelicCatalog.Instance;

        if (catalog == null)
            return list;

        foreach (RelicData relic in catalog.Relics)
            if (relic != null && filter(relic))
                list.Add(relic);

        return list;
    }

    private static RelicData PickRandom(List<RelicData> list)
    {
        return list.Count == 0 ? null : list[Mathf.Min(list.Count - 1, Mathf.FloorToInt(Rng() * list.Count))];
    }

    // Comunes o raras entregables (las repetidas estan permitidas). El Fragmento y las exclusivas quedan fuera.
    private static RelicData PickOfRarity(RelicRarity rarity)
    {
        return PickRandom(Pool(r => r.rarity == rarity && !r.IsExclusive && !r.eventOnly));
    }

    // Exclusivas disponibles: aun no poseidas y con origen normal (no el Fragmento de la Mascara).
    public static List<RelicData> AvailableExclusives()
    {
        return Pool(r => r.IsExclusive && !r.eventOnly && RelicInventory.CountOwned(r.typeId) == 0);
    }

    private static RelicData PickExclusive()
    {
        return PickRandom(AvailableExclusives());
    }

    private static bool OwnsAnyDropExclusive()
    {
        foreach (RelicData relic in Pool(r => r.IsExclusive && !r.eventOnly))
            if (RelicInventory.CountOwned(relic.typeId) > 0)
                return true;

        return false;
    }

    // ------------------------------------------------------------
    // Evento de Noelle
    // ------------------------------------------------------------

    // Entrega el Broche del Vigia al terminar el evento de Valdoria. Una sola vez por partida (el evento no se repite).
    public static bool GrantValdoriaReward()
    {
        RelicCatalog catalog = RelicCatalog.Instance;
        RelicData relic = catalog != null ? catalog.Find(ValdoriaRewardTypeId) : null;

        if (relic == null || valdoriaGranted)
            return false;

        if (RelicInventory.Add(ValdoriaRewardTypeId) == 0)
            return false;

        valdoriaGranted = true;
        Debug.Log("Reliquia obtenida: " + relic.displayName + " (El informe del último vigía).");
        return true;
    }

    // Entrega el Fragmento de la Mascara al terminar "La mascara rota". Una sola vez por partida; no sale de la tienda ni de
    // las recompensas aleatorias. Devuelve true si se entrego ahora.
    public static bool GrantNoelleFragment()
    {
        RelicCatalog catalog = RelicCatalog.Instance;
        RelicData fragment = catalog != null ? catalog.Find(FragmentTypeId) : null;

        if (fragment == null || fragmentGranted || RelicInventory.CountOwned(FragmentTypeId) > 0)
            return false;

        if (RelicInventory.Add(FragmentTypeId) == 0)
            return false;

        fragmentGranted = true;
        Debug.Log("Reliquia obtenida: " + fragment.displayName + " (La máscara rota).");
        return true;
    }
}
