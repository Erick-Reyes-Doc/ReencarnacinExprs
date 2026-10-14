using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Carga un EncounterData en una escena de combate: limpia los enemigos
// anteriores, instancia los nuevos en sus posiciones, los registra en
// TurnManager y PositionManager con sus APIs de registro, configura la
// XP del encuentro e inicia la batalla. No toca a los protagonistas.
//
// Solo carga cuando no hay un combate activo. Control de depuracion:
// L carga el siguiente encuentro de la lista.
public class EncounterLoader : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PositionManager positionManager;

    [Tooltip("Padre opcional de los enemigos instanciados.")]
    [SerializeField] private Transform spawnParent;

    [Header("Encuentros")]
    [SerializeField] private List<EncounterData> encounters =
        new List<EncounterData>();

    private readonly List<GameObject> spawned = new List<GameObject>();
    private int nextIndex;

    public EncounterData Current { get; private set; }

    public IReadOnlyList<EncounterData> Encounters => encounters;

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.lKey.wasPressedThisFrame)
        {
            LoadNext();
        }
    }

    // Carga el siguiente encuentro de la lista (ciclico).
    public bool LoadNext()
    {
        if (encounters.Count == 0)
        {
            Debug.Log("No hay encuentros configurados.");
            return false;
        }

        EncounterData next = encounters[nextIndex % encounters.Count];

        if (!LoadEncounter(next))
            return false;

        nextIndex++;
        return true;
    }

    public bool LoadEncounter(EncounterData data)
    {
        if (data == null || turnManager == null || positionManager == null)
        {
            Debug.LogWarning("Faltan referencias para cargar el encuentro.");
            return false;
        }

        if (turnManager.BattleActive)
        {
            Debug.Log(
                "No se puede cargar un encuentro durante un combate activo."
            );
            return false;
        }

        // Tras una derrota total no se continua: se vuelve al menu.
        if (turnManager.PartyDefeated ||
            !turnManager.HasAliveUnit(TeamSide.Player))
        {
            Debug.Log(
                "No se puede cargar un encuentro: el equipo fue derrotado."
            );
            return false;
        }

        if (!data.Validate(out string error))
        {
            Debug.LogWarning(
                "Encuentro invalido (" + data.name + "): " + error
            );
            return false;
        }

        ClearEnemies();

        foreach (EncounterData.EnemySlot slot in data.enemies)
        {
            GameObject go = spawnParent != null
                ? Instantiate(slot.prefab, spawnParent)
                : Instantiate(slot.prefab);

            // Siempre en la escena del cargador (no en la activa).
            if (spawnParent == null)
                UnityEngine.SceneManagement.SceneManager
                    .MoveGameObjectToScene(go, gameObject.scene);

            go.name = slot.prefab.name + " E" + slot.position;
            go.SetActive(true);

            BattleUnit unit = go.GetComponent<BattleUnit>();
            unit.SetPosition(slot.position);

            // Vida inicial completa (Awake ya lo hace al instanciar en
            // ejecucion; esto lo garantiza tambien fuera de ella).
            unit.InitializeUnit();

            spawned.Add(go);

            // Si el registro falla se deshace todo lo cargado.
            if (!positionManager.RegisterUnit(unit) ||
                !turnManager.RegisterUnit(unit))
            {
                Debug.LogWarning(
                    "No se pudo registrar " + go.name + "; se cancela la carga."
                );
                ClearEnemies();
                return false;
            }
        }

        turnManager.SetEncounterExperience(data.experienceReward);
        positionManager.UpdateVisualPositions();

        // El HUD vuelve a leer las unidades de la escena.
        foreach (CombatHudController hud in FindObjectsByType<CombatHudController>())
        {
            if (hud.gameObject.scene == gameObject.scene)
                hud.RefreshUnits();
        }

        Current = data;

        Debug.Log(
            "Encuentro cargado: " + data.displayName + " (" +
            data.enemies.Count + " enemigos, " + data.experienceReward + " XP)."
        );

        turnManager.StartBattle();
        return true;
    }

    // Quita los enemigos actuales: los instanciados por este cargador se
    // destruyen; los que ya estaban en la escena se desactivan.
    public void ClearEnemies()
    {
        if (turnManager.BattleActive)
            return;

        List<BattleUnit> removed =
            turnManager.RemoveUnitsOfSide(TeamSide.Enemy);

        positionManager.ClearTeam(TeamSide.Enemy);

        foreach (BattleUnit unit in removed)
        {
            if (unit != null && !spawned.Contains(unit.gameObject))
                unit.gameObject.SetActive(false);
        }

        foreach (GameObject go in spawned)
        {
            if (go == null)
                continue;

            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        spawned.Clear();
        Current = null;
    }
}
