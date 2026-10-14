using System.Collections.Generic;
using UnityEngine;

// Recoge las metricas de cada combate escuchando los eventos existentes
// (TurnManager y BattleUnit.DamageApplied). Es de solo lectura: no
// cambia ni el combate ni el balance. Genera un unico informe por
// combate y lo limpia al empezar el siguiente.
//
// Orden -90: se suscribe despues del CombatSessionBinder (-100), asi al
// recibir la victoria ya se han entregado las Coronas, y antes del
// EncounterLoader (-50), que puede iniciar el combate en su Start.
[DefaultExecutionOrder(-90)]
public class CombatMetricsCollector : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private EncounterLoader encounterLoader;

    [Tooltip("Imprime el resumen en la consola al terminar cada combate.")]
    [SerializeField] private bool logSummary = true;

    private CombatMetrics.Report current;
    private readonly Dictionary<BattleUnit, CombatMetrics.MemberReport> members =
        new Dictionary<BattleUnit, CombatMetrics.MemberReport>();

    private int potionsAtStart;
    private int antidotesAtStart;
    private int coinsAtStart;
    private bool finished;

    // Informe del ultimo combate terminado (null si aun no hay).
    public CombatMetrics.Report LastReport { get; private set; }

    private void Start()
    {
        if (turnManager == null)
            return;

        turnManager.OnBattleStarted -= HandleStarted;
        turnManager.OnBattleStarted += HandleStarted;
        turnManager.OnBattleEnding -= HandleEnding;
        turnManager.OnBattleEnding += HandleEnding;
        turnManager.OnBattleWon -= HandleWon;
        turnManager.OnBattleWon += HandleWon;
        turnManager.OnPartyDefeated -= HandleDefeated;
        turnManager.OnPartyDefeated += HandleDefeated;
    }

    private void OnEnable()
    {
        BattleUnit.DamageApplied += HandleDamage;
        EnemyAIController.ActionPerformed += HandleEnemyAction;
    }

    private void OnDisable()
    {
        BattleUnit.DamageApplied -= HandleDamage;
        EnemyAIController.ActionPerformed -= HandleEnemyAction;
    }

    // Actividad de la IA enemiga (solo lectura).
    private void HandleEnemyAction(BattleUnit self, AbilityData ability, BattleUnit target, bool special)
    {
        if (current == null || finished || ability == null)
            return;

        if (special)
            current.enemySpecialUses++;

        if (ability.abilityType == AbilityType.Heal)
        {
            current.enemyHeals++;
            return;
        }

        if (target != null && target.Side == TeamSide.Player)
        {
            string key = target.gameObject.name;
            current.enemyTargets.TryGetValue(key, out int n);
            current.enemyTargets[key] = n + 1;
        }
    }

    private void OnDestroy()
    {
        if (turnManager == null)
            return;

        turnManager.OnBattleStarted -= HandleStarted;
        turnManager.OnBattleEnding -= HandleEnding;
        turnManager.OnBattleWon -= HandleWon;
        turnManager.OnPartyDefeated -= HandleDefeated;
    }

    // Nuevo combate: las metricas anteriores no se arrastran.
    private void HandleStarted()
    {
        current = new CombatMetrics.Report();
        members.Clear();
        finished = false;

        EncounterData encounter = encounterLoader != null ? encounterLoader.Current : null;

        current.aiVersion = EnemyAISettings.Label(EnemyAISettings.Active);
        current.encounterId = encounter != null ? encounter.encounterId : "(sin encuentro)";
        EnemyArchetypeData.Zone zone = encounter != null
            ? encounter.zone : EnemyArchetypeData.Zone.Pradera;
        current.zone = encounter != null ? zone.ToString() : "-";

        if (encounter != null)
            CombatMetrics.TargetRounds(zone, out current.targetMin, out current.targetMax);

        potionsAtStart = GameSession.Potions;
        antidotesAtStart = GameSession.Antidotes;
        coinsAtStart = GameSession.Coins;

        foreach (BattleUnit unit in FindObjectsByType<BattleUnit>())
        {
            if (unit.gameObject.scene != gameObject.scene ||
                unit.Side != TeamSide.Player)
                continue;

            CombatMetrics.MemberReport member = new CombatMetrics.MemberReport
            {
                name = unit.gameObject.name,
                maxHealth = unit.MaxHealth,
                finalHealth = unit.CurrentHealth
            };

            members[unit] = member;
            current.members.Add(member);
        }
    }

    // Vida realmente perdida: ya incluye evasion, defensa, Marca, Guard
    // y proteccion. Cada golpe pasa una sola vez por TakeDamage.
    private void HandleDamage(BattleUnit unit, int hpLost, bool periodic)
    {
        if (current == null || finished || hpLost <= 0 ||
            !members.TryGetValue(unit, out CombatMetrics.MemberReport member))
            return;

        if (periodic)
            member.periodicDamage += hpLost;
        else
            member.directDamage += hpLost;

        // Se comprueba tras aplicar el daño (el evento va antes de OnDefeated).
        if (!unit.IsAlive)
            member.defeated = true;
    }

    // Antes de la XP y de la recuperacion del 40%.
    private void HandleEnding(bool victory)
    {
        if (current == null || finished)
            return;

        current.victory = victory;
        current.rounds = turnManager.CurrentRound;
        current.duration = CombatMetrics.Classify(
            current.rounds, current.targetMin, current.targetMax);

        foreach (KeyValuePair<BattleUnit, CombatMetrics.MemberReport> pair in members)
        {
            pair.Value.finalHealth = pair.Key.CurrentHealth;
            pair.Value.maxHealth = pair.Key.MaxHealth;
            pair.Value.defeated = !pair.Key.IsAlive;
        }

        current.potionsUsed = Mathf.Max(0, potionsAtStart - GameSession.Potions);
        current.antidotesUsed = Mathf.Max(0, antidotesAtStart - GameSession.Antidotes);
    }

    private void HandleWon()
    {
        Finish(turnManager.LastExperienceAwarded);
    }

    private void HandleDefeated()
    {
        Finish(0);
    }

    // Un solo informe por combate. Las recompensas se leen de lo que
    // realmente se entrego (Coronas: diferencia en GameSession).
    private void Finish(int experience)
    {
        if (current == null || finished)
            return;

        finished = true;

        current.experienceAwarded = experience;
        current.coinsAwarded = current.victory
            ? Mathf.Max(0, GameSession.Coins - coinsAtStart)
            : 0;

        LastReport = current;
        CombatMetrics.Add(current);

        if (logSummary)
            Debug.Log(CombatMetrics.Summary(current));
    }

    [ContextMenu("Exportar metricas a CSV")]
    public void ExportCsv()
    {
        string path = CombatMetrics.ExportCsv();
        Debug.Log(path != null
            ? "Metricas exportadas: " + path
            : "Exportacion disponible solo en editor o compilaciones de desarrollo.");
    }
}
