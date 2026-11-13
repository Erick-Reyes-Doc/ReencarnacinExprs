using UnityEngine;

// Estadisticas de la partida en curso (se muestran en los creditos). Solo cuenta: no cambia nada del juego. Se reinician con
// cada Nueva partida (GameSession.StartNewGame) y se conservan al terminar la sesion para poder mostrarlas despues.
// Dano, curacion, derrotas y habilidades se leen de los eventos del combate; las victorias las registra CombatSessionBinder.
public static class GameStats
{
    public static bool Started { get; private set; }
    public static int Victories { get; private set; }
    public static int EnemiesDefeated { get; private set; }
    public static int HeroesDowned { get; private set; }
    public static int AbilitiesUsed { get; private set; }
    public static int CoinsEarned { get; private set; }
    public static int RelicsFound { get; private set; }
    public static int LevelsGained { get; private set; }
    public static long DamageDealt { get; private set; }
    public static long DamageTaken { get; private set; }
    public static long HealingDone { get; private set; }

    private static double startTime;

    // Segundos desde que empezo la partida (tiempo real; incluye pausas).
    public static double ElapsedSeconds => Started ? System.Math.Max(0.0, Time.realtimeSinceStartupAsDouble - startTime) : 0.0;

    public static void Reset()
    {
        Started = true;
        startTime = Time.realtimeSinceStartupAsDouble;
        Victories = EnemiesDefeated = HeroesDowned = AbilitiesUsed = CoinsEarned = RelicsFound = LevelsGained = 0;
        DamageDealt = DamageTaken = HealingDone = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        BattleUnit.DamageApplied -= OnDamage;
        BattleUnit.HealApplied -= OnHeal;
        AbilitySystem.AbilityExecuted -= OnAbility;
        BattleUnit.DamageApplied += OnDamage;
        BattleUnit.HealApplied += OnHeal;
        AbilitySystem.AbilityExecuted += OnAbility;
    }

    private static void OnDamage(BattleUnit unit, int amount, bool periodic)
    {
        if (!GameSession.IsActive || unit == null || amount <= 0)
            return;

        if (unit.Side == TeamSide.Enemy)
            DamageDealt += amount;
        else
            DamageTaken += amount;

        // La unidad cae justo con este daño (TakeDamage ignora a las ya derrotadas, asi que cada caida cuenta una vez).
        if (unit.CurrentHealth <= 0)
        {
            if (unit.Side == TeamSide.Enemy)
                EnemiesDefeated++;
            else
                HeroesDowned++;
        }
    }

    private static void OnHeal(BattleUnit unit, int amount)
    {
        if (GameSession.IsActive && unit != null && unit.Side == TeamSide.Player && amount > 0)
            HealingDone += amount;
    }

    private static void OnAbility(BattleUnit user, BattleUnit target, AbilityData ability)
    {
        if (GameSession.IsActive && user != null && user.Side == TeamSide.Player)
            AbilitiesUsed++;
    }

    // Una victoria de combate (la llama CombatSessionBinder con lo ya calculado).
    public static void RecordVictory(int coins, bool relic, int levels)
    {
        if (!GameSession.IsActive)
            return;

        Victories++;
        CoinsEarned += Mathf.Max(0, coins);
        LevelsGained += Mathf.Max(0, levels);

        if (relic)
            RelicsFound++;
    }

    // Texto h:mm:ss (o m:ss si dura menos de una hora).
    public static string FormatTime(double seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt((float)seconds));
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;
        return h > 0 ? h + ":" + m.ToString("00") + ":" + s.ToString("00") : m + ":" + s.ToString("00");
    }
}
