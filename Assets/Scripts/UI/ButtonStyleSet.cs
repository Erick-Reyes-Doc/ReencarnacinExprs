using UnityEngine;

// Jerarquia visual de los botones del menu (una sola configuracion para menu principal, pausa y confirmaciones):
//   Principal  = marco dorado (menuNormal): NUEVA PARTIDA, OPCIONES, SALIR y los cuatro botones de PAUSA.
//   Secundario = marco cafe oscuro (menuDisable): VOLVER, CONFIRMAR y CANCELAR.
// Los sprites se asignan en el asset Resources/ButtonStyleSet; ButtonFeedback lo lee al preparar cada boton.
[CreateAssetMenu(fileName = "ButtonStyleSet", menuName = "Reencarnacion Express/UI/Button Style Set")]
public class ButtonStyleSet : ScriptableObject
{
    public Sprite primary;      // menuNormal
    public Sprite secondary;    // cafe oscuro (prueba: "boton cafe"; antes menuDisable)

    // Escala de los bordes 9-slice del sprite secundario. Con 3,9 los bordes de "boton cafe" (252/297/253/301 px) no se
    // estiran ni se solapan en un boton de 464 x 156 y el marco visible mide casi lo mismo que el dorado.
    public float secondaryMultiplier = 3.9f;

    // Botones secundarios por nombre de objeto (el resto de botones del menu son principales).
    public string[] secondaryNames = { "BackButton", "BotonVolver", "BotonConfirmar", "BotonCancelar" };

    private static ButtonStyleSet instance;

    public static ButtonStyleSet Instance
    {
        get
        {
            if (instance == null)
                instance = Resources.Load<ButtonStyleSet>("ButtonStyleSet");

            return instance;
        }
    }

    public bool IsSecondary(string buttonName)
    {
        foreach (string n in secondaryNames)
            if (n == buttonName)
                return true;

        return false;
    }
}
