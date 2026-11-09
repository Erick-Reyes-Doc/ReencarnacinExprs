
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    [Header("Unidades del combate")]
    [SerializeField]
    private List<BattleUnit> units =
        new List<BattleUnit>();

    private List<BattleUnit> turnOrder =
        new List<BattleUnit>();

    private int currentTurnIndex;
    private int currentRound;

    private bool battleFinished;
    private bool experienceAwarded;

    // Recuperacion poscombate (regla ajustada del GDD): al ganar, los
    // derrotados reviven con este % de su vida maxima efectiva; los
    // supervivientes conservan exactamente su vida (sin curacion).
    public const int ReviveHealthPercent = 20;

    private bool recoveryApplied;

    // Regla de encuentro (solo la activa el encuentro del Capitan): si el jefe cae, los enemigos que sigan vivos HUYEN, se
    // cancela su accion pendiente y el combate termina en victoria (sin muertes adicionales ni recompensas extra).
    private bool minionsFleeWhenBossFalls;
    private string bossFallsMessage = "";

    public void SetBossFallsRule(bool enabled, string message)
    {
        minionsFleeWhenBossFalls = enabled;
        bossFallsMessage = message ?? "";
    }
    private bool partyDefeated;

    [Header("Recompensa del encuentro")]
    [Tooltip("0 = suma de EnemyExperienceReward de los enemigos. Mayor que 0 = XP fija del encuentro (p. ej. Pradera 50, Bosque 100, Corrupcion 150, Jefe 200), entregada una sola vez.")]
    [SerializeField, Min(0)]
    private int encounterExperienceOverride = 0;

    // Identifica la accion enemiga pendiente: invalida ejecuciones
    // repetidas o de turnos anteriores.
    private int enemyActionToken;

    public BattleUnit CurrentUnit { get; private set; }

    public bool BattleFinished => battleFinished;

    public int CurrentRound => currentRound;

    // Copia de solo lectura del orden de la ronda actual (de mayor a menor
    // velocidad). La usa el HUD; modificarla no altera los turnos.
    public List<BattleUnit> GetTurnOrderSnapshot()
    {
        return new List<BattleUnit>(turnOrder);
    }

    // true si el combate termino con la derrota total del equipo.
    public bool PartyDefeated => partyDefeated;

    // Victoria confirmada (ya entregada la XP y aplicada la recuperacion).
    public event System.Action OnBattleWon;

    // Derrota total del equipo.
    public event System.Action OnPartyDefeated;

    // true mientras hay un combate en curso (iniciado y sin terminar).
    public bool BattleActive => currentRound > 0 && !battleFinished;

    // Eventos de solo lectura para el diagnostico de balance.
    // Empieza un combate (estado ya reiniciado).
    public event System.Action OnBattleStarted;

    // El combate termino (true = victoria de los protagonistas). Se
    // dispara una sola vez, antes de la XP y de la recuperacion
    // poscombate, para poder leer la vida final.
    public event System.Action<bool> OnBattleEnding;

    private bool endingAnnounced;

    // XP entregada de verdad en la ultima victoria.
    public int LastExperienceAwarded { get; private set; }

    // ------------------------------------------------------------
    // Registro de unidades (carga de encuentros)
    // ------------------------------------------------------------

    // Anade una unidad al combate. Solo se permite fuera de un combate
    // activo; StartBattle() la suscribe a los eventos de derrota.
    public bool RegisterUnit(BattleUnit unit)
    {
        if (unit == null || BattleActive || units.Contains(unit))
            return false;

        units.Add(unit);
        return true;
    }

    // Anade una unidad a un combate EN CURSO (refuerzos experimentales de jefe). Se suscribe a la derrota y empieza sin
    // cooldowns; entra en el orden de turnos en la ronda siguiente (el orden de la ronda actual no se toca).
    public bool AddReinforcement(BattleUnit unit)
    {
        if (unit == null || !BattleActive || battleFinished || units.Contains(unit))
            return false;

        units.Add(unit);
        unit.OnDefeated -= HandleUnitDefeated;
        unit.OnDefeated += HandleUnitDefeated;

        AbilityCooldownController cooldowns = unit.GetComponent<AbilityCooldownController>();

        if (cooldowns != null)
            cooldowns.ResetAll();

        return true;
    }

    // Quita todas las unidades de un equipo y las devuelve.
    // Solo se permite fuera de un combate activo.
    public List<BattleUnit> RemoveUnitsOfSide(TeamSide side)
    {
        List<BattleUnit> removed = new List<BattleUnit>();

        if (BattleActive)
            return removed;

        foreach (BattleUnit unit in units.ToArray())
        {
            if (unit == null || unit.Side != side)
                continue;

            unit.OnDefeated -= HandleUnitDefeated;
            units.Remove(unit);
            removed.Add(unit);
        }

        units.RemoveAll(unit => unit == null);

        return removed;
    }

    public bool HasAliveUnit(TeamSide side)
    {
        return units.Any(unit =>
            unit != null && unit.Side == side && unit.IsAlive);
    }

    // XP fija del encuentro (0 = suma de las recompensas de los enemigos).
    public void SetEncounterExperience(int experience)
    {
        encounterExperienceOverride = Mathf.Max(0, experience);
    }

    private void Start()
    {
        // El cargador de encuentros puede haber iniciado ya el combate
        // elegido en el mapa.
        if (currentRound == 0)
            StartBattle();
    }

    public void StartBattle()
    {
        currentRound = 0;
        currentTurnIndex = 0;
        battleFinished = false;
        experienceAwarded = false;
        recoveryApplied = false;
        partyDefeated = false;
        endingAnnounced = false;
        LastExperienceAwarded = 0;
        CurrentUnit = null;

        // Reliquias: estado de combate limpio (usos por combate / turno / ronda).
        RelicEffects.ResetCombat();

        // Descarta cualquier accion enemiga pendiente del encuentro anterior.
        enemyActionToken++;
        StopAllCoroutines();

        SubscribeToUnits();

        OnBattleStarted?.Invoke();

        StartNewRound();
    }

    private void OnDestroy()
    {
        foreach (BattleUnit unit in units)
        {
            if (unit != null)
                unit.OnDefeated -= HandleUnitDefeated;
        }
    }

    // Quitar antes de agregar evita suscripciones duplicadas
    // si el encuentro se reinicia.
    private void SubscribeToUnits()
    {
        foreach (BattleUnit unit in units)
        {
            if (unit == null)
                continue;

            unit.OnDefeated -= HandleUnitDefeated;
            unit.OnDefeated += HandleUnitDefeated;

            // Un encuentro nuevo empieza sin cooldowns.
            AbilityCooldownController cooldowns =
                unit.GetComponent<AbilityCooldownController>();

            if (cooldowns != null)
                cooldowns.ResetAll();

            PreparedAbilityController prepared =
                unit.GetComponent<PreparedAbilityController>();

            if (prepared != null)
                prepared.Clear();
        }
    }

    // Tras cualquier derrota se comprueba el fin del combate.
    // No avanza el turno: eso sigue haciendolo EndTurn().
    private void HandleUnitDefeated(BattleUnit unit)
    {
        if (battleFinished)
            return;

        CheckBattleEnd();
    }

    private void StartNewRound()
    {
        if (battleFinished)
            return;

        // Comprobar si algun equipo fue derrotado.
        if (CheckBattleEnd())
            return;

        currentRound++;

        // Ordenar solamente las unidades vivas.
        turnOrder = units
            .Where(unit => unit != null && unit.IsAlive)
            .OrderByDescending(unit => unit.Speed)
            .ToList();

        currentTurnIndex = 0;

        Debug.Log("=== RONDA " + currentRound + " ===");

        foreach (BattleUnit unit in turnOrder)
        {
            Debug.Log(
                unit.Data.unitName +
                " - Velocidad: " + unit.Speed
            );
        }

        StartNextTurn();
    }

    private void StartNextTurn()
    {
        if (battleFinished)
            return;

        if (CheckBattleEnd())
            return;

        // Buscar la siguiente unidad viva.
        while (currentTurnIndex < turnOrder.Count)
        {
            BattleUnit unit = turnOrder[currentTurnIndex];


            if (unit != null && unit.IsAlive)
            {
                CurrentUnit = unit;
                RelicEffects.OnTurnStart(unit, currentRound);

                Debug.Log(
                    "Turno de: " +
                    CurrentUnit.Data.unitName
                );

                // Los cooldowns avanzan en cada turno propio, incluso
                // si la unidad pierde la accion por Stun.
                AbilityCooldownController cooldowns =
                    CurrentUnit.GetComponent<AbilityCooldownController>();

                if (cooldowns != null)
                    cooldowns.TickTurnStart();

                // Daño periodico al comenzar el turno propio.
                StatusEffectController statusEffects =
                    CurrentUnit.GetComponent<StatusEffectController>();

                if (statusEffects != null)
                {
                    statusEffects.ProcessTurnStart();

                    // La muerte ya notifico a OnDefeated; si el
                    // combate termino no hay nada mas que hacer.
                    if (battleFinished)
                        return;

                    // Murio por daño periodico: pasar al siguiente.
                    if (!unit.IsAlive)
                    {
                        currentTurnIndex++;
                        StartNextTurn();
                        return;
                    }
                }

                // Procesar efectos de regeneracion
                // al comenzar el turno del personaje.
                RegenerationEffect regeneration =
                    CurrentUnit.GetComponent<RegenerationEffect>();

                if (regeneration != null && regeneration.IsActive)
                {
                    regeneration.ProcessTurnStart();
                }

                bool stunned = statusEffects != null &&
                    statusEffects.SkipsCurrentAction;

                // Habilidad preparada (ej. Meteorito): si esta aturdida
                // se cancela; si no, el impacto es la accion de este
                // turno y se pasa al siguiente turno valido.
                PreparedAbilityController prepared =
                    CurrentUnit.GetComponent<PreparedAbilityController>();

                if (prepared != null && prepared.HasPreparation)
                {
                    if (stunned)
                    {
                        prepared.Cancel("la unidad esta aturdida");
                    }
                    else
                    {
                        prepared.Resolve();

                        // La victoria pudo ocurrir durante el impacto.
                        if (battleFinished)
                            return;

                        currentTurnIndex++;
                        StartNextTurn();
                        return;
                    }
                }

                // Aturdida: pierde la accion y pasa al siguiente
                // turno valido. Se comprueba al final para que el
                // daño periodico y la regeneracion ya se hayan aplicado.
                if (stunned)
                {
                    Debug.Log(
                        CurrentUnit.Data.unitName +
                        " esta aturdido y pierde su accion."
                    );

                    currentTurnIndex++;
                    StartNextTurn();
                    return;
                }

                // Enemigo con IA: ya se procesaron estados, regeneracion y
                // Stun, asi que decide y actua (una vez) en este turno.
                EnemyAIController ai =
                    CurrentUnit.GetComponent<EnemyAIController>();

                if (ai != null && ai.isActiveAndEnabled &&
                    unit.Side == TeamSide.Enemy)
                {
                    BeginEnemyAction(unit, ai);
                }

                return;
            }


            // Saltar unidad derrotada.
            currentTurnIndex++;
        }

        // Si terminaron los turnos, iniciar otra ronda.
        StartNewRound();
    }

    // Programa la accion de un enemigo con IA. Con retardo (solo en
    // ejecucion) se usa una corrutina; sin retardo se ejecuta al momento.
    private void BeginEnemyAction(BattleUnit unit, EnemyAIController ai)
    {
        int token = ++enemyActionToken;

        if (ai.ActionDelay > 0f && Application.isPlaying)
            StartCoroutine(EnemyActionRoutine(unit, ai, token));
        else
            ExecuteEnemyAction(unit, ai, token);
    }

    [Header("Ritmo del turno enemigo")]
    [Tooltip("Segundos que el enemigo espera antes de actuar (se ve quien tiene el turno). Se usa el mayor entre este valor y el retardo de su arquetipo.")]
    [SerializeField, Min(0f)] private float enemyPreActionSeconds = 2f;

    [Tooltip("Segundos que se mantiene la escena tras la accion del enemigo (se leen el daño, los estados y el mensaje) antes de pasar al siguiente turno.")]
    [SerializeField, Min(0f)] private float enemyPostActionSeconds = 3f;

    private IEnumerator EnemyActionRoutine(
        BattleUnit unit,
        EnemyAIController ai,
        int token
    )
    {
        yield return new WaitForSeconds(Mathf.Max(ai.ActionDelay, enemyPreActionSeconds));

        if (!ExecuteEnemyAction(unit, ai, token, false))
            yield break;

        // El combate termino con esta accion (victoria o derrota): no hay mas turnos.
        if (battleFinished)
            yield break;

        // Pausa para distinguir lo que hizo el enemigo antes del siguiente turno.
        yield return new WaitForSeconds(enemyPostActionSeconds);

        if (!battleFinished && CurrentUnit == unit)
            EndTurn();
    }

    // Una accion como maximo y un unico EndTurn por turno enemigo.
    // Devuelve false si no se ejecuto (turno ya invalido). endTurnNow = pasar de turno al momento (sin pausa).
    private bool ExecuteEnemyAction(
        BattleUnit unit,
        EnemyAIController ai,
        int token,
        bool endTurnNow = true
    )
    {
        if (token != enemyActionToken || battleFinished ||
            CurrentUnit != unit || unit == null || !unit.IsAlive)
            return false;

        // Invalida cualquier otra ejecucion de este mismo turno.
        enemyActionToken++;

        ai.PerformTurn();

        // No hace nada si la accion termino el combate.
        if (endTurnNow)
            EndTurn();

        return true;
    }

    public void EndTurn()
    {
        if (battleFinished)
            return;

        currentTurnIndex++;
        StartNextTurn();
    }

    // Suma la XP de los enemigos originales del encuentro
    // y la entrega completa a cada protagonista con progresion,
    // aunque haya caido. Solo se entrega una vez por combate.
    private void AwardVictoryExperience()
    {
        if (experienceAwarded)
            return;

        experienceAwarded = true;

        int totalExperience = 0;

        if (encounterExperienceOverride > 0)
        {
            // XP fija del encuentro, independiente del numero de enemigos.
            totalExperience = encounterExperienceOverride;
        }
        else
        {
            foreach (BattleUnit unit in units)
            {
                if (unit == null || unit.Side != TeamSide.Enemy)
                    continue;

                EnemyExperienceReward reward =
                    unit.GetComponent<EnemyExperienceReward>();

                if (reward != null)
                    totalExperience += reward.ExperienceReward;
            }
        }

        if (totalExperience <= 0)
            return;

        LastExperienceAwarded = totalExperience;

        Debug.Log("Experiencia de victoria: " + totalExperience);

        foreach (BattleUnit unit in units)
        {
            if (unit == null || unit.Side != TeamSide.Player)
                continue;

            CharacterProgression progression =
                unit.GetComponent<CharacterProgression>();

            if (progression != null)
                progression.GainExperience(totalExperience);
        }
    }

    // Recuperacion poscombate: una sola vez por victoria. Solo revive a los
    // derrotados (ReviveHealthPercent de su vida maxima efectiva); los vivos
    // no reciben curacion.
    private void ApplyPostBattleRecovery()
    {
        if (recoveryApplied)
            return;

        recoveryApplied = true;

        foreach (BattleUnit unit in units)
        {
            if (unit == null || unit.Side != TeamSide.Player)
                continue;

            unit.ReviveAfterBattle(ReviveHealthPercent);
        }
    }

    // Cuando cae el jefe con la regla activa, los demas enemigos vivos huyen. Se llama al principio de CheckBattleEnd, asi
    // la victoria sigue el camino normal (XP y Coronas una sola vez, recuperacion de siempre).
    private void TryMinionsFlee()
    {
        if (!minionsFleeWhenBossFalls || battleFinished)
            return;

        bool bossFell = units.Any(u => u != null && u.Side == TeamSide.Enemy && u.Data != null && u.Data.isBoss && !u.IsAlive && !u.HasFled);

        if (!bossFell)
            return;

        List<BattleUnit> fleeing = units.Where(u => u != null && u.Side == TeamSide.Enemy && u.IsAlive).ToList();

        if (fleeing.Count == 0)
            return;

        // Cancela cualquier accion enemiga pendiente.
        enemyActionToken++;
        Debug.Log(string.IsNullOrEmpty(bossFallsMessage) ? "¡El jefe ha caído! El enemigo restante huye del campo de batalla." : bossFallsMessage);

        foreach (BattleUnit unit in fleeing)
            unit.Flee();
    }

    private bool CheckBattleEnd()
    {
        TryMinionsFlee();

        bool playersAlive = units.Any(unit =>
            unit != null &&
            unit.Side == TeamSide.Player &&
            unit.IsAlive
        );

        bool enemiesAlive = units.Any(unit =>
            unit != null &&
            unit.Side == TeamSide.Enemy &&
            unit.IsAlive
        );

        if (playersAlive && enemiesAlive)
            return false;

        battleFinished = true;
        CurrentUnit = null;

        // Nada pendiente del combate sobrevive a su final: acciones
        // enemigas programadas, preparaciones, estados y regeneracion.
        enemyActionToken++;
        StopAllCoroutines();

        foreach (BattleUnit unit in units)
        {
            if (unit == null)
                continue;

            PreparedAbilityController prepared =
                unit.GetComponent<PreparedAbilityController>();

            if (prepared != null)
                prepared.Cancel("el combate termino");

            StatusEffectController statusEffects =
                unit.GetComponent<StatusEffectController>();

            if (statusEffects != null)
                statusEffects.ClearAllEffects();

            RegenerationEffect regeneration =
                unit.GetComponent<RegenerationEffect>();

            if (regeneration != null)
                regeneration.ClearEffect();
        }

        if (!endingAnnounced)
        {
            endingAnnounced = true;
            OnBattleEnding?.Invoke(playersAlive && !enemiesAlive);
        }

        if (!enemiesAlive && playersAlive)
        {
            Debug.Log("¡VICTORIA!");
            // El resultado se anuncia una sola vez por combate.
            bool firstTime = !recoveryApplied;

            AwardVictoryExperience();

            // Despues de la XP y las subidas de nivel, para usar las
            // estadisticas efectivas actualizadas.
            ApplyPostBattleRecovery();

            if (firstTime)
                OnBattleWon?.Invoke();
        }
        else if (!playersAlive && enemiesAlive)
        {
            bool firstTime = !partyDefeated;
            partyDefeated = true;
            Debug.Log("DERROTA");

            if (firstTime)
                OnPartyDefeated?.Invoke();
        }
        else
        {
            Debug.Log("Combate finalizado sin ganador.");
        }

        return true;
    }
}
