// Reglas de progresion del mapa. Los encuentros son secuenciales
// (P1 > P2 > P3 > P4 > B1 > B2 > B3 > C1 > C2 > BOSS) y el estado de cada nodo
// se deduce de los encuentros completados en GameSession: no hay un
// segundo registro que pueda desincronizarse.
public static class MapProgression
{
    public enum NodeState
    {
        Locked,
        Available,
        Completed
    }

    public static readonly string[] Order =
    {
        "P1", "P2", "P3", "P4", "B1", GameSession.NoelleEventId, "B2", "B3", "C1", "C2",
        BossId
    };

    // Jefe final (Trono Sellado). Se desbloquea al completar C2.
    public const string BossId = "BOSS";

    public const string Meadow = "Pradera";
    public const string Forest = "Bosque";
    public const string Corruption = "Corrupcion";

    // Titulos narrativos visibles de los encuentros (mapa y combate). Solo texto de interfaz: los ids no cambian.
    private static readonly System.Collections.Generic.Dictionary<string, string> Titles =
        new System.Collections.Generic.Dictionary<string, string>
    {
        { "P1", "Un mal día para cobrar" },
        { "P2", "Flechas en el camino" },
        { "P3", "Ecos de Liria" },
        { "P4", "Secretos bajo las ruinas" },
        { "B1", "Huellas en la espesura" },
        { "B2", "Susurros en la oscuridad" },
        { "B3", "La última vigilia" },
        { "C1", "El aliento del Ocaso" },
        { "C2", "Donde muere la luz" },
        { BossId, "La última orden" },
    };

    // Titulo narrativo del encuentro, o null si no tiene.
    public static string TitleOf(string encounterId)
    {
        return !string.IsNullOrEmpty(encounterId) && Titles.TryGetValue(encounterId, out string title) ? title : null;
    }

    // Region con su nombre visible (con tilde).
    public static string ZoneDisplayName(string zone)
    {
        return zone == Corruption ? "Corrupción" : zone;
    }

    // Texto secundario del encuentro: "Pradera · Encuentro 1" ("Corrupción · Jefe final" para el jefe).
    public static string SubtitleOf(string encounterId)
    {
        if (string.IsNullOrEmpty(encounterId))
            return "";

        string zone = ZoneDisplayName(ZoneOf(encounterId));
        string detail = encounterId == BossId
            ? "Jefe final"
            : "Encuentro " + encounterId.Substring(1);
        return zone + " · " + detail;
    }

    // Nodos narrativos: no son combates y no dan XP ni Coronas.
    public static bool IsEvent(string id)
    {
        return id == GameSession.NoelleEventId;
    }

    public static string ZoneOf(string encounterId)
    {
        if (string.IsNullOrEmpty(encounterId))
            return "";

        // El jefe vive en la Corrupcion, pero no cuenta en su progreso.
        if (encounterId == BossId)
            return Corruption;

        switch (encounterId[0])
        {
            case 'P': return Meadow;
            case 'B': return Forest;
            case 'C': return Corruption;
            default: return "";
        }
    }

    public static int IndexOf(string encounterId)
    {
        for (int i = 0; i < Order.Length; i++)
        {
            if (Order[i] == encounterId)
                return i;
        }

        return -1;
    }

    public static NodeState GetState(string encounterId)
    {
        int index = IndexOf(encounterId);

        if (index < 0 || !GameSession.IsActive)
            return NodeState.Locked;

        if (GameSession.IsEncounterCompleted(encounterId))
            return NodeState.Completed;

        // Disponible solo si el anterior esta completado (o es el primero).
        if (index == 0 || GameSession.IsEncounterCompleted(Order[index - 1]))
            return NodeState.Available;

        return NodeState.Locked;
    }

    // Se puede iniciar: desbloqueado y todavia sin completar.
    public static bool CanStart(string encounterId)
    {
        return GetState(encounterId) == NodeState.Available;
    }

    // Siguiente encuentro recomendado (null si ya se completaron todos).
    public static string NextRecommended()
    {
        foreach (string id in Order)
        {
            if (GetState(id) == NodeState.Available)
                return id;
        }

        return null;
    }

    public static bool IsZoneUnlocked(string zone)
    {
        switch (zone)
        {
            case Meadow:
                return GameSession.IsActive;
            case Forest:
                return GameSession.IsEncounterCompleted("P4");
            case Corruption:
                return GameSession.IsEncounterCompleted("B3");
            default:
                return false;
        }
    }

    public static void ZoneProgress(string zone, out int done, out int total)
    {
        done = 0;
        total = 0;

        foreach (string id in Order)
        {
            if (id == BossId || ZoneOf(id) != zone)
                continue;

            total++;

            if (GameSession.IsEncounterCompleted(id))
                done++;
        }
    }

    // C2 completado: el jefe esta disponible (mientras no se supere).
    public static bool BossUnlocked()
    {
        return GameSession.IsEncounterCompleted("C2");
    }

    // Jefe superado: el jugador termino todos los encuentros de la demo.
    public static bool AllCompleted()
    {
        return GameSession.IsEncounterCompleted(BossId);
    }
}
