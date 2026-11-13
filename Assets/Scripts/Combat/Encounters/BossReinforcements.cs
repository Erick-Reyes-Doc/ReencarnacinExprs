using UnityEngine;

// EXPERIMENTAL (prototipo de balance). Refuerzo unico de un jefe con fases: cuando el jefe entra en la fase 2 (o mas tarde, en
// cuanto no quede ningun secuaz vivo) aparece UN refuerzo. Reglas:
//  - Solo se activa si alguien agrega este componente (las herramientas de prueba); ningun encuentro lo tiene por defecto.
//  - El refuerzo solo puede aparecer una vez.
//  - Nunca hay mas de un secuaz activo: si queda un secuaz vivo, no aparece nada todavia.
//  - Entra en la primera posicion enemiga libre (la configurada primero) y actua desde la ronda siguiente.
public class BossReinforcements : MonoBehaviour
{
    public BattleUnit boss;
    public GameObject reinforcementPrefab;
    public int[] preferredPositions = { 2, 3, 4 };
    [Min(1)] public int phaseIndexToTrigger = 1;   // 1 = fase 2

    private BossPhaseController phases;
    private bool phaseReached;

    public bool Spawned { get; private set; }

    private void OnEnable()
    {
        BossPhaseController.PhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        BossPhaseController.PhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(BossPhaseController controller, BossPhaseData.Phase previous, BossPhaseData.Phase next)
    {
        if (boss == null || controller.Unit != boss)
            return;

        phases = controller;

        if (controller.CurrentIndex >= phaseIndexToTrigger)
        {
            phaseReached = true;
            TrySpawn();
        }
    }

    // Si al empezar la fase 2 aun vive un secuaz, el refuerzo espera a que caiga.
    private void Update()
    {
        if (phaseReached && !Spawned)
            TrySpawn();
    }

    private bool HasActiveMinion()
    {
        foreach (BattleUnit unit in FindObjectsByType<BattleUnit>())
            if (unit != boss && unit.Side == TeamSide.Enemy && unit.IsAlive)
                return true;

        return false;
    }

    private void TrySpawn()
    {
        if (Spawned || boss == null || !boss.IsAlive || reinforcementPrefab == null)
            return;

        TurnManager turnManager = FindAnyObjectByType<TurnManager>();
        PositionManager positionManager = FindAnyObjectByType<PositionManager>();

        if (turnManager == null || positionManager == null || turnManager.BattleFinished || HasActiveMinion())
            return;

        int position = 0;

        foreach (int candidate in preferredPositions)
        {
            if (positionManager.GetUnitAtPosition(TeamSide.Enemy, candidate) == null)
            {
                position = candidate;
                break;
            }
        }

        if (position == 0)
            return;

        GameObject go = Instantiate(reinforcementPrefab);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, boss.gameObject.scene);
        go.name = reinforcementPrefab.name + " E" + position;
        go.SetActive(true);

        BattleUnit unit = go.GetComponent<BattleUnit>();
        unit.SetPosition(position);
        unit.InitializeUnit();

        if (!positionManager.RegisterUnit(unit) || !turnManager.AddReinforcement(unit))
        {
            Destroy(go);
            return;
        }

        Spawned = true;
        positionManager.UpdateVisualPositions();

        foreach (CombatHudController hud in FindObjectsByType<CombatHudController>())
            hud.RefreshUnits();

        Debug.Log("Refuerzo: " + unit.Data.unitName + " entra en E" + position + " (actuara desde la ronda siguiente).");
    }
}
