using UnityEngine;

// Marca un boton del mapa para que UiHoverSound NO reproduzca el sonido de hover mientras 'silent' sea true
// (lugares sin mision disponible: bloqueados o ya completados). El clic sigue igual.
public class MapHoverSoundGate : MonoBehaviour
{
    public bool silent;
}
