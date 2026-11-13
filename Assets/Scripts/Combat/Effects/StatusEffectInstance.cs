// Datos de un estado activo sobre una unidad.
// Solo almacena informacion; la logica vive en
// StatusEffectController y en los sistemas de combate.
public class StatusEffectInstance
{
    public StatusEffectType Type { get; private set; }

    // Valor del efecto (daño, curacion, porcentaje, etc.).
    // Su significado depende del tipo de estado.
    public int Potency { get; private set; }

    public int RemainingTurns { get; private set; }

    // Unidad que aplico el efecto (puede ser null).
    public BattleUnit Source { get; private set; }

    public bool IsExpired => RemainingTurns <= 0;

    // Solo para Guard: aliado protegido y porcentaje de su daño
    // directo que intercepta el portador del efecto.
    public BattleUnit ProtectedAlly { get; private set; }
    public int InterceptPercent { get; private set; }

    public void SetProtection(BattleUnit ally, int interceptPercent)
    {
        ProtectedAlly = ally;
        InterceptPercent = ally == null
            ? 0
            : UnityEngine.Mathf.Clamp(interceptPercent, 0, 100);
    }

    public StatusEffectInstance(
        StatusEffectType type,
        int potency,
        int duration,
        BattleUnit source
    )
    {
        Type = type;
        Potency = potency;
        RemainingTurns = duration;
        Source = source;
    }

    // Sustituye potencia, duracion y origen.
    public void Refresh(int potency, int duration, BattleUnit source)
    {
        Potency = potency;
        RemainingTurns = duration;
        Source = source;
    }

    // Solo extiende la duracion, sin acortarla.
    public void ExtendDuration(int duration)
    {
        if (duration > RemainingTurns)
            RemainingTurns = duration;
    }

    public void ReduceDuration()
    {
        if (RemainingTurns > 0)
            RemainingTurns--;
    }
}
