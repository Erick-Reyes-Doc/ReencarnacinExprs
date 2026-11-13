using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fundido desde negro al entrar en una escena. Solo actua cuando la escena anterior lo pidio
// (GameSession.MapFadeInPending, que activa la escena de dialogo inicial); en cualquier otro caso se apaga sola.
public class SceneFadeIn : MonoBehaviour
{
    [SerializeField] private Image overlay;
    [SerializeField] private float seconds = 1f;

    private float elapsed;
    private bool active;

    // Verdadero mientras el fundido (de entrada o de salida) sigue en curso.
    public bool IsFading => active || !string.IsNullOrEmpty(leaveScene);

    // Salida: fundido a negro y carga de otra escena.
    private string leaveScene;
    private float leaveSeconds;
    private float leaveElapsed;

    // 0 mientras el mapa sigue en pantalla; sube a 1 durante el fundido de salida (la musica baja a la par).
    public float LeaveProgress => string.IsNullOrEmpty(leaveScene) ? 0f : Mathf.Clamp01(leaveElapsed / Mathf.Max(leaveSeconds, 0.01f));

    private void Start()
    {
        active = GameSession.ConsumeMapFadeIn();

        if (overlay == null)
            return;

        overlay.raycastTarget = false;

        if (active)
            SetAlpha(1f);
        else
            overlay.gameObject.SetActive(false);
    }

    // Funde a negro (sin que el mapa se vea cortar) y carga la escena indicada.
    public void FadeOutAndLoad(string sceneName, float seconds)
    {
        if (overlay == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        active = false;
        leaveScene = sceneName;
        leaveSeconds = seconds;
        leaveElapsed = 0f;
        overlay.gameObject.SetActive(true);
        SetAlpha(0f);

        // La escena se va cargando en segundo plano MIENTRAS se funde a negro (sin congelar el mapa); se activa al terminar el fundido.
        measureStart = Time.realtimeSinceStartup;
        loadOp = SceneManager.LoadSceneAsync(sceneName);

        if (loadOp != null)
            loadOp.allowSceneActivation = false;
    }

    private AsyncOperation loadOp;
    private static float measureStart = -1f;   // medicion temporal de tiempos de carga (quitar tras medir)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void HookMeasure()
    {
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (measureStart < 0f)
                return;

            Debug.Log("[CargaMedida] '" + scene.name + "': " + (Time.realtimeSinceStartup - measureStart).ToString("0.00") + " s desde que se pidio la carga hasta que la escena estuvo activa.");
            measureStart = -1f;
        };
    }

    private void Update()
    {
        if (!string.IsNullOrEmpty(leaveScene) && overlay != null)
        {
            leaveElapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float k = Mathf.Clamp01(leaveElapsed / Mathf.Max(leaveSeconds, 0.01f));
            SetAlpha(Mathf.SmoothStep(0f, 1f, k));

            if (k >= 1f)
            {
                if (loadOp != null)
                {
                    // Pantalla en negro: se espera a que la carga en segundo plano este lista y se activa la escena.
                    if (loadOp.progress >= 0.9f)
                    {
                        Debug.Log("[CargaMedida] carga en segundo plano lista a los " + (Time.realtimeSinceStartup - measureStart).ToString("0.00") + " s (el fundido dura " + leaveSeconds.ToString("0.0") + " s).");
                        loadOp.allowSceneActivation = true;
                        loadOp = null;
                    }
                }
                else
                {
                    string target = leaveScene;
                    leaveScene = null;
                    SceneManager.LoadScene(target);
                }
            }

            return;
        }

        if (!active || overlay == null)
            return;

        elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        float t = Mathf.Clamp01(elapsed / Mathf.Max(seconds, 0.01f));
        SetAlpha(1f - Mathf.SmoothStep(0f, 1f, t));

        if (t >= 1f)
        {
            active = false;
            overlay.gameObject.SetActive(false);
        }
    }

    private void SetAlpha(float alpha)
    {
        Color c = overlay.color;
        c.a = alpha;
        overlay.color = c;
    }
}
