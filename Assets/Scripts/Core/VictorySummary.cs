using System.Collections.Generic;

// Resumen de la ultima victoria, guardado en memoria para que la escena "Victory" lo muestre (la escena de combate se
// descarga antes). Solo son DATOS ya calculados por los sistemas existentes (XP de TurnManager/CharacterProgression, Coronas de
// CombatSessionBinder y reliquia de RelicRewards): leerlo no entrega nada, asi que abrir Victory o pulsar Continuar nunca
// repite recompensas. Se descarta al continuar.
public class VictorySummary
{
    public class HeroEntry
    {
        public string heroId;
        public int levelBefore, xpBefore;
        public int levelAfter, xpAfter;
        public int xpGained;
        public int maxLevel = 10;
        public CharacterProgressionData progression;

        public bool LeveledUp => levelAfter > levelBefore;

        // XP necesaria para pasar de 'level' al siguiente (0 = nivel maximo o sin datos).
        public int Required(int level)
        {
            return progression != null && level < maxLevel ? progression.GetExperienceRequired(level) : 0;
        }
    }

    public EncounterData encounter;
    public string encounterId;
    public bool firstClear;                 // primera victoria de la mision (la unica que da Coronas y reliquia)
    public int coins;                       // Coronas entregadas ahora (0 si la mision ya estaba completada)
    public RelicData relic;                 // reliquia entregada ahora (null = sin reliquia)
    public readonly List<HeroEntry> heroes = new List<HeroEntry>();

    public static VictorySummary Current { get; private set; }

    public static void Set(VictorySummary summary)
    {
        Current = summary;
    }

    public static void Clear()
    {
        Current = null;
    }

    public HeroEntry Find(string heroId)
    {
        foreach (HeroEntry entry in heroes)
            if (entry.heroId == heroId)
                return entry;

        return null;
    }
}
