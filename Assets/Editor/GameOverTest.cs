#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Herramienta de DESARROLLO (solo editor). Comprueba en Unity el flujo de derrota total: aparece la pantalla de derrota,
// su boton vuelve al menu y termina la sesion, y una partida nueva empieza desde cero (nivel 1, vida completa, Coronas,
// consumibles y reliquias iniciales, sin progreso). Uso (Play Mode, desde el mapa): GameOverTest.Begin(rutaSalida).
public static class GameOverTest
{
    static readonly string[] HeroIds = { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" };
    static readonly StringBuilder log = new StringBuilder();
    static string outPath;
    static bool active;
    static int step;
    static int wait;
    static int fails, checks;

    public static bool Done => !active;

    public static void Begin(string path)
    {
        outPath = path;
        log.Length = 0;
        fails = 0;
        checks = 0;
        step = 0;
        wait = 0;
        active = true;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    static void Check(bool ok, string text)
    {
        checks++;

        if (!ok)
            fails++;

        log.AppendLine((ok ? "[OK]   " : "[FALLA] ") + text);
    }

    static void Finish(string status)
    {
        EditorApplication.update -= Update;
        active = false;
        log.AppendLine();
        log.AppendLine("Comprobaciones: " + checks + " | fallidas: " + fails + " | " + status);
        System.IO.File.WriteAllText(outPath, log.ToString() + "\nfin\n");
    }

    static void Update()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish("cancelado (salio de Play)");
            return;
        }

        try
        {
            Tick();
        }
        catch (System.Exception e)
        {
            log.AppendLine("ERROR: " + e);
            Finish("error");
        }
    }

    static BattleUnit Hero(string id)
    {
        foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
            if (u.Side == TeamSide.Player && u.gameObject.name == id)
                return u;

        return null;
    }

    static GameObject FindByName(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == name && go.scene.IsValid())
                return go;

        return null;
    }

    static void Tick()
    {
        if (wait-- > 0)
            return;

        switch (step)
        {
            case 0:
            {
                log.AppendLine("PREPARACION: partida con progreso (nivel 3, P1 completado, 2 reliquias, 90 Coronas)");
                GameSession.StartNewGame();
                RelicInventory.Clear();
                List<GameSession.PartyMemberState> party = new List<GameSession.PartyMemberState>();

                foreach (string id in HeroIds)
                    party.Add(new GameSession.PartyMemberState { id = id, level = 3, experience = 50, health = 9999, maxHealth = 9999 });

                GameSession.SavePartySnapshot(party);
                GameSession.MarkEncounterCompleted("P1");
                GameSession.AddCoins(40);
                RelicInventory.Add("anillo_hierro");
                RelicInventory.Add("amuleto_vital");
                EncounterLoader.DevEncounter = AssetDatabase.LoadAssetAtPath<EncounterData>("Assets/ScriptableObjects/Encounters/Pradera/P2.asset");
                Time.timeScale = 1f;
                Application.runInBackground = true;
                step = 1;
                wait = 8;
                SceneManager.LoadScene("CombateIntegracion");
                return;
            }

            case 1:
            {
                CombatIntroTransition intro = Object.FindAnyObjectByType<CombatIntroTransition>();

                if (intro != null)
                    Object.Destroy(intro);

                TurnManager tm = Object.FindAnyObjectByType<TurnManager>();

                if (tm == null || Hero("PlayerEvanora") == null || Hero("PlayerCaballero") == null)
                {
                    wait = 1;
                    return;
                }

                Check(GameSession.IsActive && GameSession.Coins == 90, "sesion activa con " + GameSession.Coins + " Coronas");
                GameObject panelBefore = FindByName("PanelDerrota");
                Check(panelBefore != null && !panelBefore.activeInHierarchy, "el panel de derrota existe y esta oculto durante el combate");

                foreach (string id in HeroIds)
                    Hero(id).TakeDamage(9999);

                step = 2;
                wait = 12;
                return;
            }

            case 2:
            {
                TurnManager tm = Object.FindAnyObjectByType<TurnManager>();
                Check(tm != null && tm.BattleFinished && tm.PartyDefeated, "el combate termino en derrota total");
                Check(GameSession.IsDefeated, "la sesion quedo marcada como derrotada");

                GameObject panel = FindByName("PanelDerrota");
                Check(panel != null && panel.activeInHierarchy, "aparece la pantalla de derrota (PanelDerrota activo)");

                if (panel != null)
                {
                    string texts = "";

                    foreach (Text t in panel.GetComponentsInChildren<Text>(true))
                        texts += "\"" + t.text + "\" ";

                    log.AppendLine("   textos del panel: " + texts);
                }

                Button button = null;
                GameObject buttonObject = FindByName("VolverMenu");

                if (buttonObject != null)
                    button = buttonObject.GetComponent<Button>();

                Check(button != null && button.interactable && button.gameObject.activeInHierarchy, "el boton 'Volver al menu' esta activo e interactuable");

                CombatHudController hud = Object.FindAnyObjectByType<CombatHudController>();
                Check(hud != null && hud.CanReturnToMenu(), "la escena de menu esta en Build Settings");

                if (button != null)
                    button.onClick.Invoke();

                step = 3;
                wait = 12;
                return;
            }

            case 3:
            {
                Check(SceneManager.GetActiveScene().name == "Menu", "el boton carga la escena Menu (actual: " + SceneManager.GetActiveScene().name + ")");
                Check(!GameSession.IsActive, "la sesion termino (GameSession.IsActive = false)");
                Check(GameSession.Coins == 0 && GameSession.Potions == 0 && GameSession.CompletedEncounterCount == 0 && RelicInventory.Items.Count == 0,
                    "progreso descartado: Coronas " + GameSession.Coins + ", pociones " + GameSession.Potions + ", encuentros " + GameSession.CompletedEncounterCount + ", reliquias " + RelicInventory.Items.Count);
                GameSession.PartyMemberState s;
                Check(!GameSession.TryGetMember("PlayerEvanora", out s), "ya no hay foto del equipo (niveles y vida descartados)");

                MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();
                Check(menu != null, "el menu principal esta cargado");

                if (menu != null)
                    Check(menu.PrepareNewGame(), "Nueva partida se prepara correctamente");

                Check(GameSession.IsActive && !GameSession.IsDefeated, "la sesion nueva esta activa y sin marca de derrota");
                Check(GameSession.Coins == GameSession.StartingCoins && GameSession.Potions == GameSession.StartingPotions && GameSession.Antidotes == GameSession.StartingAntidotes,
                    "recursos iniciales: " + GameSession.Coins + " Coronas, " + GameSession.Potions + " pocion, " + GameSession.Antidotes + " antidotos");
                Check(GameSession.CompletedEncounterCount == 0 && !GameSession.IsEncounterCompleted("P1"), "ningun encuentro completado");
                Check(RelicInventory.Items.Count == 0, "sin reliquias gratuitas (" + RelicInventory.Items.Count + ")");
                Check(!GameSession.TryGetMember("PlayerEvanora", out s), "sin foto del equipo: los heroes empezaran en nivel 1");

                EncounterLoader.DevEncounter = AssetDatabase.LoadAssetAtPath<EncounterData>("Assets/ScriptableObjects/Encounters/Pradera/P1.asset");
                step = 4;
                wait = 4;
                SceneManager.LoadScene("CombateIntegracion");
                return;
            }

            case 4:
            {
                CombatIntroTransition intro = Object.FindAnyObjectByType<CombatIntroTransition>();

                if (intro != null)
                    Object.Destroy(intro);

                if (Object.FindAnyObjectByType<TurnManager>() == null || Hero("PlayerEvanora") == null || Hero("PlayerCaballero") == null)
                {
                    wait = 1;
                    return;
                }

                foreach (string id in HeroIds)
                {
                    BattleUnit h = Hero(id);
                    CharacterProgression p = h.GetComponent<CharacterProgression>();
                    Check(p != null && p.Level == 1 && h.CurrentHealth == h.MaxHealth, id + ": nivel " + (p != null ? p.Level : -1) + ", vida " + h.CurrentHealth + "/" + h.MaxHealth + " (nivel 1 y vida completa)");
                }

                Finish("completado");
                return;
            }
        }
    }
}
#endif
