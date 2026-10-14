using System.Collections.Generic;
using UnityEngine;

// Une la escena de combate con GameSession.
//
//  - Partida nueva (sin foto del equipo): deja al equipo en nivel 1,
//    XP 0, vida maxima, Noelle enmascarada, sin estados ni cooldowns y
//    en sus posiciones iniciales, y guarda esa foto.
//  - Partida en curso (hay foto): restaura niveles, XP, vida y etapa
//    de Noelle.
//  - Tras cada victoria guarda el progreso y anota el encuentro; tras
//    la derrota total marca la sesion como perdida.
//
// Se ejecuta antes que el resto (orden -100) para que el combate arranque
// ya con el equipo correcto.
[DefaultExecutionOrder(-100)]
public class CombatSessionBinder : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private EncounterLoader encounterLoader;

    [Tooltip("Protagonistas de la partida.")]
    [SerializeField] private List<BattleUnit> party =
        new List<BattleUnit>();

    private readonly Dictionary<BattleUnit, int> initialPositions =
        new Dictionary<BattleUnit, int>();

    private void Awake()
    {
        foreach (BattleUnit unit in party)
        {
            if (unit != null)
                initialPositions[unit] = unit.Position;
        }
    }

    private void Start()
    {
        // Escena abierta directamente (editor): sesion de desarrollo.
        GameSession.EnsureSession();

        // La tienda no puede abrirse mientras esta escena de combate
        // este cargada.
        GameSession.SetInCombat(true);

        if (GameSession.HasPartySnapshot)
            RestoreParty();
        else
        {
            ResetPartyForNewGame();
            GameSession.SavePartySnapshot(CaptureParty());
        }

        Subscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        GameSession.SetInCombat(false);
    }

    private void Subscribe()
    {
        if (turnManager == null)
            return;

        turnManager.OnBattleWon -= HandleBattleWon;
        turnManager.OnBattleWon += HandleBattleWon;
        turnManager.OnPartyDefeated -= HandlePartyDefeated;
        turnManager.OnPartyDefeated += HandlePartyDefeated;
    }

    private void Unsubscribe()
    {
        if (turnManager == null)
            return;

        turnManager.OnBattleWon -= HandleBattleWon;
        turnManager.OnPartyDefeated -= HandlePartyDefeated;
    }

    // Victoria: ya se entrego la XP y se aplico la recuperacion.
    private void HandleBattleWon()
    {
        GameSession.SavePartySnapshot(CaptureParty());

        if (encounterLoader == null || encounterLoader.Current == null)
            return;

        EncounterData encounter = encounterLoader.Current;

        // Las Coronas pertenecen al encuentro y se entregan solo la
        // primera vez que se supera (una sola vez por victoria; repetirlo
        // no da ganancias).
        if (!GameSession.IsEncounterCompleted(encounter.encounterId))
        {
            if (GameSession.AddCoins(encounter.coinReward))
                Debug.Log(
                    "Recompensa: +" + encounter.coinReward +
                    " Coronas (total " + GameSession.Coins + ")."
                );
        }
        else
        {
            Debug.Log("Encuentro ya completado: sin Coronas.");
        }

        GameSession.MarkEncounterCompleted(encounter.encounterId);
    }

    private void HandlePartyDefeated()
    {
        GameSession.MarkDefeated();
    }

    // ------------------------------------------------------------
    // Foto del equipo
    // ------------------------------------------------------------

    public List<GameSession.PartyMemberState> CaptureParty()
    {
        List<GameSession.PartyMemberState> list =
            new List<GameSession.PartyMemberState>();

        foreach (BattleUnit unit in party)
        {
            if (unit == null)
                continue;

            CharacterProgression progression =
                unit.GetComponent<CharacterProgression>();

            CharacterAbilityLoadout loadout =
                unit.GetComponent<CharacterAbilityLoadout>();

            list.Add(new GameSession.PartyMemberState
            {
                id = unit.gameObject.name,
                level = progression != null ? progression.Level : 1,
                experience = progression != null
                    ? progression.CurrentExperience : 0,
                health = unit.CurrentHealth,
                maxHealth = unit.MaxHealth,
                transformed = loadout != null && loadout.IsTransformed,
                hasCurableStatus = ConsumableService.HasCurableStatus(unit)
            });
        }

        return list;
    }

    // Partida nueva: todo desde cero.
    public void ResetPartyForNewGame()
    {
        foreach (BattleUnit unit in party)
        {
            if (unit == null)
                continue;

            ClearTemporaryState(unit);

            CharacterProgression progression =
                unit.GetComponent<CharacterProgression>();

            if (progression != null)
                progression.ResetProgress();

            CharacterAbilityLoadout loadout =
                unit.GetComponent<CharacterAbilityLoadout>();

            if (loadout != null)
                loadout.SetTransformed(false);   // Noelle enmascarada

            // Estadisticas de nivel 1 y vida maxima.
            unit.InitializeUnit();

            if (initialPositions.TryGetValue(unit, out int position))
                unit.SetPosition(position);
        }
    }

    // Partida en curso: aplica la foto guardada.
    public void RestoreParty()
    {
        foreach (BattleUnit unit in party)
        {
            if (unit == null)
                continue;

            ClearTemporaryState(unit);

            if (!GameSession.TryGetMember(unit.gameObject.name,
                    out GameSession.PartyMemberState state))
                continue;

            CharacterProgression progression =
                unit.GetComponent<CharacterProgression>();

            if (progression != null)
                progression.SetProgress(state.level, state.experience);

            CharacterAbilityLoadout loadout =
                unit.GetComponent<CharacterAbilityLoadout>();

            if (loadout != null)
                loadout.SetTransformed(state.transformed);

            unit.InitializeUnit();
            unit.RestoreHealth(state.health);

            if (initialPositions.TryGetValue(unit, out int position))
                unit.SetPosition(position);
        }
    }

    // Estados, regeneracion, preparaciones y cooldowns no pasan de una
    // partida (o escena) a otra.
    private static void ClearTemporaryState(BattleUnit unit)
    {
        StatusEffectController statuses =
            unit.GetComponent<StatusEffectController>();

        if (statuses != null)
            statuses.ClearAllEffects();

        RegenerationEffect regeneration =
            unit.GetComponent<RegenerationEffect>();

        if (regeneration != null)
            regeneration.ClearEffect();

        PreparedAbilityController prepared =
            unit.GetComponent<PreparedAbilityController>();

        if (prepared != null)
            prepared.Clear();

        AbilityCooldownController cooldowns =
            unit.GetComponent<AbilityCooldownController>();

        if (cooldowns != null)
            cooldowns.ResetAll();
    }
}
