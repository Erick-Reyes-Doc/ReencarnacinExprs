using UnityEngine;

// Tipografia del juego: Merriweather (OFL). Los TMP usan los assets de Assets/Fonts (MerriweatherRegular_SDF /
// MerriweatherBold_SDF); los Text antiguos (uGUI clasico) usan estas dos fuentes, cargadas desde Resources/Fonts.
public static class GameFonts
{
    private static Font regular, bold;

    public static Font Regular
    {
        get
        {
            if (regular == null)
                regular = Resources.Load<Font>("Fonts/Merriweather-Regular");

            return regular != null ? regular : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    public static Font Bold
    {
        get
        {
            if (bold == null)
                bold = Resources.Load<Font>("Fonts/Merriweather-Bold");

            return bold != null ? bold : Regular;
        }
    }

    // Aplica la fuente a un Text clasico: la negrita usa Merriweather Bold (sin negrita sintetica encima).
    public static void Apply(UnityEngine.UI.Text text)
    {
        if (text == null)
            return;

        bool wantsBold = ((int)text.fontStyle & 1) != 0;
        text.font = wantsBold ? Bold : Regular;

        if (wantsBold)
            text.fontStyle = (FontStyle)((int)text.fontStyle & 2);
    }
}
