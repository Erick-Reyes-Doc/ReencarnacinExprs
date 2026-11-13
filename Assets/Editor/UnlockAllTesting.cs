using UnityEditor;

// Interruptor de pruebas: desbloquea todos los encuentros del mapa (solo en el editor).
public static class UnlockAllTesting
{
    private const string Key = "RE_DebugUnlockAll";
    private const string Menu = "Reencarnacion Express/Desbloquear todo (pruebas)";

    [MenuItem(Menu)]
    private static void Toggle()
    {
        EditorPrefs.SetBool(Key, !EditorPrefs.GetBool(Key, false));
    }

    [MenuItem(Menu, true)]
    private static bool ToggleValidate()
    {
        Menu_SetChecked();
        return true;
    }

    private static void Menu_SetChecked()
    {
        UnityEditor.Menu.SetChecked(Menu, EditorPrefs.GetBool(Key, false));
    }
}
