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
                string target = leaveScene;
                leaveScene = null;
                SceneManager.LoadScene(target);
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
