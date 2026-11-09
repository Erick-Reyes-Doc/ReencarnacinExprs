using System;
using System.Collections.Generic;
using UnityEngine;

// Conversacion editable: lineas habladas (con quien habla) y lineas de tutorial (sin hablante, mismo cuadro).
// Los nombres de hablante deben coincidir con los de los retratos del DialogueController ("Elaina", "Evanora",
// "Noelle", "Valerian"). "Dios" no tiene retrato: solo muestra su nombre y los heroes siguen visibles.
// Efecto de luz sencillo asociado a una linea (p. ej. el destello del santuario en "La mascara rota").
public enum DialogueLight
{
    None,
    Flash,      // destello dorado que queda como un resplandor suave
    FadeOut     // el resplandor se apaga lentamente
}

// Accion puntual al mostrar una linea (retrato de Noelle y sonido de "La mascara rota"). Se ejecuta una sola vez por linea.
public enum DialogueAction
{
    None,
    MaskCrack,   // retrato agrietado + sonido de la mascara + pausa dramatica
    MaskOff      // retrato sin mascara
}

[CreateAssetMenu(fileName = "NuevoDialogo", menuName = "Reencarnacion Express/Dialogo")]
public class DialogueData : ScriptableObject
{
    [Serializable]
    public class Line
    {
        [Tooltip("Quien habla. Vacio = linea de tutorial (sin placa de nombre).")]
        public string speaker;

        [TextArea(2, 5)]
        public string text;

        [Tooltip("Efecto de luz al mostrar la linea (necesita 'Light Image' en el DialogueController).")]
        public DialogueLight light = DialogueLight.None;

        [Tooltip("Accion puntual al mostrar la linea (retrato / sonido).")]
        public DialogueAction action = DialogueAction.None;

        public Line() { }

        public Line(string speaker, string text, DialogueLight light = DialogueLight.None, DialogueAction action = DialogueAction.None)
        {
            this.action = action;
            this.speaker = speaker;
            this.text = text;
            this.light = light;
        }
    }

    public List<Line> lines = new List<Line>();
}
