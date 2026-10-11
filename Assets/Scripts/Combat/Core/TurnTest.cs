
using UnityEngine;
using UnityEngine.InputSystem;

public class TurnTest : MonoBehaviour
{
    [Header("Administrador de turnos")]
    [SerializeField] private TurnManager turnManager;

    private void Update()
    {
        // Detectar la tecla Espacio con el nuevo Input System.
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (turnManager == null)
            {
                Debug.LogWarning(
                    "No se asigno el TurnManager."
                );
                return;
            }

            Debug.Log("Turno finalizado.");

            turnManager.EndTurn();
        }
    }
}
