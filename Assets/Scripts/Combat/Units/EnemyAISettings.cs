using UnityEngine;

public enum EnemyAIVersion
{
    V01,   // comportamiento original
    V02    // tactico exigente (experimental)
}

// Selector central de la version de IA enemiga. Una sola instancia en la
// escena de combate; los enemigos (y sus prefabs) no se tocan: cada
// EnemyAIController consulta EnemyAISettings.Active. Sin este componente
// (o desactivado) se usa v0.1.
//
// Para volver al comportamiento anterior basta cambiar el campo a V01 en
// el Inspector (tambien en Play Mode) o desactivar el componente.
[DefaultExecutionOrder(-120)]
public class EnemyAISettings : MonoBehaviour
{
    [Tooltip("V01 = IA original. V02 = IA tactica exigente (experimental).")]
    [SerializeField] private EnemyAIVersion version = EnemyAIVersion.V01;

    [Tooltip("Para reiniciar la memoria tactica al empezar cada combate.")]
    [SerializeField] private TurnManager turnManager;

    public static EnemyAIVersion Active { get; private set; } = EnemyAIVersion.V01;

    public static string Label(EnemyAIVersion v)
    {
        return v == EnemyAIVersion.V02 ? "v0.2" : "v0.1";
    }

    // Uso en pruebas o desde codigo (no cambia la escena).
    public static void SetActive(EnemyAIVersion value)
    {
        Active = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Active = EnemyAIVersion.V01;
    }

    private void OnEnable()
    {
        Active = version;
        EnemyAIController.ResetTactics();

        if (turnManager != null)
        {
            turnManager.OnBattleStarted -= HandleBattleStarted;
            turnManager.OnBattleStarted += HandleBattleStarted;
        }
    }

    private void OnDisable()
    {
        Active = EnemyAIVersion.V01;

        if (turnManager != null)
            turnManager.OnBattleStarted -= HandleBattleStarted;
    }

    private void HandleBattleStarted()
    {
        EnemyAIController.ResetTactics();
    }

    // Cambios hechos en el Inspector durante la ejecucion.
    private void OnValidate()
    {
        if (Application.isPlaying && isActiveAndEnabled)
            Active = version;
    }
}
