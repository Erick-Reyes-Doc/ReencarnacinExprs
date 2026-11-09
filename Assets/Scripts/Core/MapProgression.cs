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
        "P1", "P2", "P3", "P4", "B1", GameSession.NoelleEventId, "B2", "B3", GameSession.ValdoriaEventId, "C1", "C2",
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
        { GameSession.ValdoriaEventId, "El informe del último vigía" },
        { BossId, "La última orden" },
    };

    // Titulo narrativo del encuentro, o null si no tiene.
    public static string TitleOf(string encounterId)
    {
        return !string.IsNullOrEmpty(encounterId) && Titles.TryGetValue(encounterId, out string title) ? title : null;
    }

    // Descripcion narrativa breve del encuentro (solo texto de interfaz). Vacia si aun no se ha escrito.
    private static readonly System.Collections.Generic.Dictionary<string, string> Descriptions =
        new System.Collections.Generic.Dictionary<string, string>
    {
        // Eventos narrativos
        { GameSession.NoelleEventId, "Las antiguas ruinas del santuario despiertan recuerdos fragmentados en Noelle. Su misteriosa máscara comienza a reaccionar ante una presencia desconocida." },

        // Pradera
        { "P1", "Un cobrador del campamento no ha regresado. Parece que tuvo un encuentro desagradable en los caminos de la pradera." },
        { "P2", "Los goblins continúan atacando a los viajeros. Sus emboscadas parecen demasiado organizadas para ser simples robos." },
        { "P3", "Las pistas conducen a las antiguas Ruinas de Liria, donde varios goblins vigilan los accesos como si protegieran algo oculto." },
        { "P4", "Las ruinas esconden señales de una operación mayor. Un poderoso goblin custodia el lugar e intenta impedir que los héroes descubran hacia dónde conducen las pistas." },
        // Bosque
        { "B1", "Los héroes siguen las pistas hasta el bosque, donde los lobos atacan sin provocación. Algo parece haber alterado el equilibrio de la naturaleza." },
        { "B2", "Un bandido y un chamán han reunido criaturas en el Sendero Sombrío. La extraña influencia que los rodea apunta a una amenaza mayor." },
        { "B3", "Un guardián forestal y sus aliados impiden el paso. Entre los restos del refugio podría encontrarse información sobre el origen de los ataques." },
        { GameSession.ValdoriaEventId, "Los informes recuperados en el bosque podrían revelar quién dirige los ataques. Los héroes regresan a Valdoria en busca de respuestas." },
        // Corrupcion
        { "C1", "El informe de Valdoria conduce a los héroes hasta las Tierras Marchitas. Criaturas corrompidas y hechiceros protegen las rutas del enemigo." },
        { "C2", "La corrupción se intensifica cerca del Trono Sellado. Un temible guardián bloquea el último camino hacia el comandante de las fuerzas enemigas." },
        // Jefe final
        { BossId, "El Capitán de las Fuerzas Expedicionarias aguarda en el Trono Sellado. Los héroes deben detener su avance antes de que la corrupción consuma estas tierras." },
    };

    public static string DescriptionOf(string encounterId)
    {
        return !string.IsNullOrEmpty(encounterId) && Descriptions.TryGetValue(encounterId, out string text) ? text : null;
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

        if (IsEvent(encounterId))
            return "Evento narrativo";

        string zone = ZoneDisplayName(ZoneOf(encounterId));
        string detail = encounterId == BossId
            ? "Jefe final"
            : "Encuentro " + encounterId.Substring(1);
        return zone + " · " + detail;
    }

    // Nodos narrativos: no son combates y no dan XP ni Coronas.
    public static bool IsEvent(string id)
    {
        return id == GameSession.NoelleEventId || id == GameSession.ValdoriaEventId;
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

    // SOLO PARA PRUEBAS EN EL EDITOR: con el interruptor activo todo el mapa esta desbloqueado (menu
    // "Reencarnacion Express > Desbloquear todo (pruebas)"). En una build siempre vale false.
    public static bool DebugUnlockAll
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool("RE_DebugUnlockAll", false);
#else
            return false;
#endif
        }
    }

    public static NodeState GetState(string encounterId)
    {
        int index = IndexOf(encounterId);

        if (index < 0 || !GameSession.IsActive)
            return NodeState.Locked;

        if (GameSession.IsEncounterCompleted(encounterId))
            return NodeState.Completed;

        // Disponible solo si el anterior esta completado (o es el primero).
        if (DebugUnlockAll || index == 0 || GameSession.IsEncounterCompleted(Order[index - 1]))
            return NodeState.Available;

        return NodeState.Locked;
    }

    // Se puede iniciar: desbloqueado y todavia sin completar.
    public static bool CanStart(string encounterId)
    {
        // En pruebas tambien se pueden repetir los encuentros ya completados.
        if (DebugUnlockAll && GetState(encounterId) == NodeState.Completed)
            return true;

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
        if (DebugUnlockAll && GameSession.IsActive && (zone == Meadow || zone == Forest || zone == Corruption))
            return true;

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
        return DebugUnlockAll ||  GameSession.IsEncounterCompleted("C2");
    }

    // Jefe superado: el jugador termino todos los encuentros de la demo.
    public static bool AllCompleted()
    {
        return GameSession.IsEncounterCompleted(BossId);
    }
}
