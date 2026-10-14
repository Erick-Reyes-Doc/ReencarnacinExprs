
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Panel de opciones")]
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private CanvasGroup optionsCanvasGroup;
    [SerializeField] private RectTransform optionsWindow;

    [Header("Animación")]
    [SerializeField] private float animationDuration = 0.3f;

    [Header("Audio")]
    [SerializeField] private AudioSource menuMusic;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;

    private Coroutine currentAnimation;

    private void Start()
    {
        optionsCanvasGroup.alpha = 0f;
        optionsCanvasGroup.interactable = false;
        optionsCanvasGroup.blocksRaycasts = false;

        optionsWindow.localScale = Vector3.one * 0.95f;
        optionsPanel.SetActive(false);

        masterVolumeSlider.SetValueWithoutNotify(AudioListener.volume);
        musicVolumeSlider.SetValueWithoutNotify(menuMusic.volume);

        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
    }

    public void NewGame()
    {
        SceneManager.LoadScene("IntroCinematic");
    }

    public void OpenOptions()
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        optionsPanel.SetActive(true);

        currentAnimation = StartCoroutine(
            AnimateOptions(true)
        );
    }

    public void CloseOptions()
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        currentAnimation = StartCoroutine(
            AnimateOptions(false)
        );
    }

    private IEnumerator AnimateOptions(bool opening)
    {
        float startAlpha = optionsCanvasGroup.alpha;
        float targetAlpha = opening ? 1f : 0f;

        Vector3 startScale = optionsWindow.localScale;
        Vector3 targetScale = Vector3.one *
            (opening ? 1f : 0.95f);

        optionsCanvasGroup.interactable = false;
        optionsCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        float duration = Mathf.Max(animationDuration, 0.01f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            t = t * t * (3f - 2f * t);

            optionsCanvasGroup.alpha =
                Mathf.Lerp(startAlpha, targetAlpha, t);

            optionsWindow.localScale =
                Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        optionsCanvasGroup.alpha = targetAlpha;
        optionsWindow.localScale = targetScale;

        if (opening)
        {
            optionsCanvasGroup.interactable = true;
            optionsCanvasGroup.blocksRaycasts = true;
        }
        else
        {
            optionsCanvasGroup.blocksRaycasts = false;
            optionsPanel.SetActive(false);
        }

        currentAnimation = null;
    }

    public void SetMasterVolume(float volume)
    {
        AudioListener.volume = volume;
    }

    public void SetMusicVolume(float volume)
    {
        menuMusic.volume = volume;
    }

    public void QuitGame()
    {
        Debug.Log("Saliendo de Reencarnación Express...");
        Application.Quit();
    }

    private void OnDestroy()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(SetMusicVolume);
    }
}
