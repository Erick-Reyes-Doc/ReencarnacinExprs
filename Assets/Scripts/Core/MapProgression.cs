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
