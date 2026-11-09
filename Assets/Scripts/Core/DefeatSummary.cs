// Dato minimo para la escena "Defeat": el encuentro perdido (su fondo se muestra muy oscurecido). Solo se lee; no cambia nada del juego.
public static class DefeatSummary
{
    public static EncounterData Encounter { get; private set; }

    public static void Set(EncounterData encounter)
    {
        Encounter = encounter;
    }

    public static void Clear()
    {
        Encounter = null;
    }
}
