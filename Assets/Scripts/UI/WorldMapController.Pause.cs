using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Integracion del menu de pausa (MapPauseMenu) con el mapa. Mientras la pausa esta abierta el
// mapa no admite seleccion de lugares, tienda, inventario, evento ni inicio de combates.
public partial class WorldMapController
{
    [Header("Menu de pausa")]
    [SerializeField] private MapPauseMenu pauseMenu;

    // La pausa esta abierta: el resto de acciones del mapa se bloquean.
    public bool PauseOpen => pauseMenu != null && pauseMenu.IsOpen;

    // La pausa solo se abre si no hay otro modal (tienda, inventario, evento) ni una carga en curso.
    public bool CanOpenPause()
    {
        return !loading && !eventOpen && (shop == null || (!shop.IsOpen && !shop.ClosedThisFrame));
    }

    // Boton de menu de la esquina superior izquierda.
    public void OpenPause()
    {
        if (pauseMenu != null)
            pauseMenu.Open();
    }

    [Header("Transicion al menu")]
    [SerializeField] private float exitFadeDuration = 2f;
    [Tooltip("Imagen negra a pantalla completa (Canvas Overlay por encima de toda la interfaz).")]
    [SerializeField] private Image exitFadeOverlay;

    // Salida al menu principal ya confirmada en la pausa: pantalla negra de 2 s y despues el mismo
    // flujo que "Volver al menu" (PrepareMenu descarta la sesion y se carga la escena del menu).
    // Una sola carga: loading bloquea el mapa y la imagen negra bloquea el resto de la interfaz.
    public bool ExitToMenu()
    {
        if (loading)
            return false;

        // Sin menu en Build Settings no hay transicion: PrepareMenu registra el error como siempre.
        if (!GameSession.IsSceneInBuild(menuSceneName))
            return PrepareMenu();

        loading = true;
        StartCoroutine(ExitToMenuTransition());
        return true;
    }

    // La sesion sigue intacta mientras dura el fundido; solo se descarta justo antes de cargar.
    private IEnumerator ExitToMenuTransition()
    {
        if (exitFadeOverlay != null)
        {
            exitFadeOverlay.raycastTarget = true;
            float duration = Mathf.Max(exitFadeDuration, 0.01f);
            float startTime = Time.unscaledTime;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed = Time.unscaledTime - startTime;
                Color c = exitFadeOverlay.color;
                c.a = Mathf.Clamp01(elapsed / duration);
                exitFadeOverlay.color = c;
                yield return null;
            }

            Color end = exitFadeOverlay.color;
            end.a = 1f;
            exitFadeOverlay.color = end;
        }

        if (!PrepareMenu())
        {
            // No deberia ocurrir (se comprobo antes): se devuelve el mapa a su estado normal.
            if (exitFadeOverlay != null)
            {
                Color c = exitFadeOverlay.color;
                c.a = 0f;
                exitFadeOverlay.color = c;
                exitFadeOverlay.raycastTarget = false;
            }

            loading = false;
            yield break;
        }

        SceneManager.LoadScene(menuSceneName);
    }
}
