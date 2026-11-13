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
[DefaultExecutionOrder(-50)]   // despues del binder (-100), antes de TurnManager
public class EncounterLoader : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PositionManager positionManager;

    [Tooltip("SpriteRenderer del escenario (detras de las unidades). Recibe el fondo de cada encuentro.")]
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Tooltip("Padre opcional de los enemigos instanciados.")]
    [SerializeField] private Transform spawnParent;

    [Header("Encuentros")]
    [SerializeField] private List<EncounterData> encounters =
        new List<EncounterData>();

    private readonly List<GameObject> spawned = new List<GameObject>();
    private int nextIndex;

    public EncounterData Current { get; private set; }

    public IReadOnlyList<EncounterData> Encounters => encounters;

    [Header("Depuracion")]
    [Tooltip("Tecla L = siguiente encuentro. Desactivada en el flujo normal para no saltar encuentros.")]
    [SerializeField] private bool allowDebugKey = false;

    // Carga el encuentro elegido en el mapa antes de que TurnManager
    // inicie el combate con los enemigos por defecto de la escena.
#if UNITY_EDITOR
    // Solo pruebas de desarrollo: encuentro que se carga al entrar en la
    // escena sin pasar por el mapa ni por la progresion (p. ej. el jefe).
    // Nunca se asigna en el flujo normal; se consume una sola vez.
    public static EncounterData DevEncounter;
#endif

    private void Start()
    {
#if UNITY_EDITOR
        if (DevEncounter != null)
        {
            EncounterData dev = DevEncounter;
            DevEncounter = null;
            LoadEncounter(dev);
            return;
        }
#endif

        string id = GameSession.TakePendingEncounter();

        if (string.IsNullOrEmpty(id))
            return;

        EncounterData data = encounters.Find(e => e != null && e.encounterId == id);

        if (data == null)
        {
            Debug.LogError("Encuentro pendiente no encontrado: " + id);
            return;
        }

        if (!MapProgression.CanStart(id))
        {
            Debug.LogWarning("Encuentro no disponible en la progresion: " + id);
            return;
        }

        LoadEncounter(data);
    }

    private void Update()
    {
        if (allowDebugKey && Debug.isDebugBuild && Keyboard.current != null &&
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

            // Orden de dibujo estable entre vecinos: el enemigo mas cercano a los heroes (casilla menor) tapa al siguiente.
            // Sin esto todos comparten orden y, al solaparse, se pisan de forma inestable (parpadeo / "trabados").
            SpriteRenderer body = go.GetComponent<SpriteRenderer>();

            if (body != null)
                body.sortingOrder += Mathf.Max(0, data.enemies.Count - 1 - slot.position);

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

        // Algunos enemigos portan una reliquia aleatoria (nunca en jefes, prologo ni B1).
        List<BattleUnit> loadedUnits = new List<BattleUnit>();

        foreach (GameObject spawnedObject in spawned)
            if (spawnedObject != null)
                loadedUnits.Add(spawnedObject.GetComponent<BattleUnit>());

        EnemyRelics.AssignForEncounter(data, loadedUnits);

        turnManager.SetEncounterExperience(data.experienceReward);
        turnManager.SetBossFallsRule(data.minionsFleeWhenBossFalls, data.bossFallsMessage);
        positionManager.UpdateVisualPositions();

        // El HUD vuelve a leer las unidades de la escena.
        foreach (CombatHudController hud in FindObjectsByType<CombatHudController>())
        {
            if (hud.gameObject.scene == gameObject.scene)
                hud.RefreshUnits();
        }

        Current = data;
        ApplyBackground(data.background);

        Debug.Log(
            "Encuentro cargado: " + data.displayName + " (" +
            data.enemies.Count + " enemigos, " + data.experienceReward + " XP)."
        );

        turnManager.StartBattle();
        return true;
    }

    private float fittedAspect;

    // Muestra el fondo del encuentro ajustado a la camara (cubre todo el encuadre sin deformarse).
    // Si el encuentro no define fondo se conserva el actual.
    private void ApplyBackground(Sprite sprite)
    {
        if (backgroundRenderer == null || sprite == null)
            return;

        backgroundRenderer.sprite = sprite;
        FitBackground();
    }

    private void LateUpdate()
    {
        // Ventana redimensionada: se reajusta al nuevo encuadre.
        Camera cam = Camera.main;

        if (backgroundRenderer != null && backgroundRenderer.sprite != null &&
            cam != null && !Mathf.Approximately(cam.aspect, fittedAspect))
        {
            FitBackground();
        }
    }

    private void FitBackground()
    {
        Camera cam = Camera.main;

        if (cam == null || !cam.orthographic)
            return;

        fittedAspect = cam.aspect;
        Vector2 size = backgroundRenderer.sprite.bounds.size;
        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;
        float scale = Mathf.Max(viewW / size.x, viewH / size.y);

        Transform t = backgroundRenderer.transform;
        t.localScale = new Vector3(scale, scale, 1f);
        t.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 5f);
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
