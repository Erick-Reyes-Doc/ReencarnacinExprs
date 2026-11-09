using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla "Defeat": se muestra cuando el equipo completo cae en combate. Fondo muy oscuro (el del encuentro perdido, casi negro),
// sombras de los heroes caidos y un unico boton que descarta la partida y vuelve al menu de inicio. Entrada con fundido lento.
// Solo presenta: la derrota ya se registro en el combate (GameSession.MarkDefeated); aqui solo se cierra la sesion al salir.
public class DefeatController : MonoBehaviour
{
    [System.Serializable]
    public class Shadow
    {
        public CanvasGroup group;
        public Image portrait;
        public Image glow;
    }

    [SerializeField] private Image background;
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private CanvasGroup subtitleGroup;
    [SerializeField] private System.Collections.Generic.List<Shadow> shadows = new System.Collections.Generic.List<Shadow>();
    [SerializeField] private CanvasGroup buttonGroup;
    [SerializeField] private Button menuButton;
    [SerializeField] private Image fade;
    [SerializeField] private string menuScene = "Menu";

    private bool leaving;
    private bool ready;

    private void Start()
    {
        Time.timeScale = 1f;

        EncounterData encounter = DefeatSummary.Encounter;

        if (background != null && encounter != null && encounter.background != null)
        {
            background.sprite = encounter.background;
            background.color = new Color(0.30f, 0.30f, 0.34f, 1f);   // muy apagado; el velo oscuro termina el trabajo
        }

        if (menuButton != null)
            menuButton.onClick.AddListener(ReturnToMenu);

        SetAlpha(titleGroup, 0f);
        SetAlpha(subtitleGroup, 0f);
        SetAlpha(buttonGroup, 0f);

        if (buttonGroup != null)
            buttonGroup.interactable = false;

        foreach (Shadow s in shadows)
            SetAlpha(s.group, 0f);

        StartCoroutine(Intro());
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (ready && !leaving && keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            ReturnToMenu();
    }

    private static void SetAlpha(CanvasGroup g, float a)
    {
        if (g != null)
            g.alpha = a;
    }

    private static IEnumerator Tween(float seconds, System.Action<float> step)
    {
        for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.1f))
        {
            step(Mathf.Clamp01(t / seconds));
            yield return null;
        }

        step(1f);
    }

    private IEnumerator Intro()
    {
        // Del negro hacia la escena, lento.
        if (fade != null)
        {
            yield return Tween(1.4f, k => fade.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, k)));
            fade.raycastTarget = false;
        }

        // Titulo: aparece con un leve descenso.
        Vector2 basePos = titleRect != null ? titleRect.anchoredPosition : Vector2.zero;

        yield return Tween(1.1f, k =>
        {
            float e = Mathf.SmoothStep(0f, 1f, k);
            SetAlpha(titleGroup, e);

            if (titleRect != null)
                titleRect.anchoredPosition = basePos + new Vector2(0f, 24f * (1f - e));
        });

        // Sombras de los heroes caidos, una tras otra.
        foreach (Shadow s in shadows)
        {
            Shadow shadow = s;
            StartCoroutine(Tween(1.0f, k => SetAlpha(shadow.group, Mathf.SmoothStep(0f, 1f, k))));
            yield return new WaitForSecondsRealtime(0.35f);
        }

        yield return new WaitForSecondsRealtime(0.5f);
        yield return Tween(0.8f, k => SetAlpha(subtitleGroup, k));
        yield return Tween(0.6f, k => SetAlpha(buttonGroup, k));

        if (buttonGroup != null)
            buttonGroup.interactable = true;

        ready = true;

        // Latido muy lento de las brasas rojas tras las sombras.
        while (!leaving)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.9f);

            foreach (Shadow s in shadows)
                if (s.glow != null)
                    s.glow.color = new Color(0.65f, 0.08f, 0.08f, 0.10f + 0.10f * pulse);

            yield return null;
        }
    }

    // Descarta la partida (sin reintento: se pierde el progreso) y vuelve al menu de inicio con un fundido a negro.
    public void ReturnToMenu()
    {
        if (leaving || !ready)
            return;

        leaving = true;
        StartCoroutine(Leave());
    }

    private IEnumerator Leave()
    {
        if (buttonGroup != null)
            buttonGroup.interactable = false;

        if (fade != null)
        {
            fade.raycastTarget = true;
            yield return Tween(0.8f, k => fade.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, k)));
        }

        DefeatSummary.Clear();
        VictorySummary.Clear();
        GameSession.EndSession();
        SceneManager.LoadScene(menuScene);
    }
}
