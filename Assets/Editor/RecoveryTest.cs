#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Herramienta de DESARROLLO (solo editor). Comprueba la recuperacion poscombate (ganar: los supervivientes conservan su
// vida, los derrotados reviven con el 20 % redondeado hacia arriba; la vida pasa de una mision a otra; las pociones no
// reviven; una derrota total sigue siendo Game Over) con combates reales de la escena de combate.
// Uso (Play Mode, desde el mapa): RecoveryTest.Begin(rutaDeSalida); cuando Done sea true el informe esta en el archivo.
public static class RecoveryTest
{
    static readonly string[] HeroIds = { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" };

    static readonly StringBuilder log = new StringBuilder();
    static string outputPath;
    static bool active;
    static int scenario;
    static int phase;
    static int wait;
    static int failures;
    static int checks;

    // Vida de cada heroe antes de ganar y al empezar el siguiente combate.
    static readonly Dictionary<string, int> beforeWin = new Dictionary<string, int>();
    static readonly Dictionary<string, int> maxAtWin = new Dictionary<string, int>();
    static readonly Dictionary<string, bool> wasDead = new Dictionary<string, bool>();

    public static bool Done => !active;

    public static void Begin(string path)
    {
        outputPath = path;
        log.Length = 0;
        failures = 0;
        checks = 0;
        scenario = 0;
        phase = 0;
        wait = 0;
        active = true;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    static void Check(bool ok, string text)
    {
        checks++;

        if (!ok)
            failures++;

        log.AppendLine((ok ? "[OK]   " : "[FALLA] ") + text);
    }

    static void Finish(string status)
    {
        EditorApplication.update -= Update;
        active = false;
        log.AppendLine();
        log.AppendLine("Comprobaciones: " + checks + " | fallidas: " + failures + " | " + status);
        System.IO.File.WriteAllText(outputPath, log.ToString() + "\nfin\n");
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
            Step();
        }
        catch (System.Exception e)
        {
            log.AppendLine("ERROR: " + e);
            Finish("error");
        }
    }

    static BattleUnit Hero(string id)
    {
        foreach (BattleUnit unit in Object.FindObjectsByType<BattleUnit>())
            if (unit.Side == TeamSide.Player && unit.gameObject.name == id)
                return unit;

        return null;
    }

    static void Load(string encounterPath)
    {
        EncounterLoader.DevEncounter = AssetDatabase.LoadAssetAtPath<EncounterData>(encounterPath);
        Time.timeScale = 1f;
        Application.runInBackground = true;
        phase = 1;
        wait = 8;
        SceneManager.LoadScene("CombateIntegracion");
    }

    static void StartSession(int level, string relicSpec)
    {
        GameSession.StartNewGame();
        List<GameSession.PartyMemberState> party = new List<GameSession.PartyMemberState>();

        foreach (string id in HeroIds)
            party.Add(new GameSession.PartyMemberState
            {
                id = id, level = level, experience = 0, health = 9999, maxHealth = 9999,
                transformed = id == "PlayerNoelle"
            });

        GameSession.SavePartySnapshot(party);
        RelicInventory.Clear();

        if (!string.IsNullOrEmpty(relicSpec))
        {
            string keys = "EANV";

            foreach (string part in relicSpec.Split(';'))
            {
                string[] kv = part.Split('=');
                string heroId = HeroIds[keys.IndexOf(kv[0][0])];
                string[] types = kv[1].Split('|');

                for (int slot = 0; slot < types.Length; slot++)
                {
                    int uid = RelicInventory.Add(types[slot]);
                    string reason;
                    RelicInventory.TryEquip(uid, heroId, slot, out reason);
                }
            }
        }
    }

    static void KillEnemies()
    {
        foreach (BattleUnit unit in Object.FindObjectsByType<BattleUnit>())
            if (unit.Side == TeamSide.Enemy && unit.IsAlive)
                unit.TakeDamage(9999);
    }

    static void Snapshot(string title)
    {
        beforeWin.Clear();
        maxAtWin.Clear();
        wasDead.Clear();
        StringBuilder sb = new StringBuilder(title + ": ");

        foreach (string id in HeroIds)
        {
            BattleUnit h = Hero(id);
            beforeWin[id] = h.CurrentHealth;
            maxAtWin[id] = h.MaxHealth;
            wasDead[id] = !h.IsAlive;
            sb.Append(id.Replace("Player", "")).Append(' ').Append(h.CurrentHealth).Append('/').Append(h.MaxHealth).Append(" | ");
        }

        log.AppendLine("   " + sb);
    }

    static void Step()
    {
        if (wait-- > 0)
            return;

        CombatIntroTransition intro = Object.FindAnyObjectByType<CombatIntroTransition>();

        if (intro != null)
            Object.Destroy(intro);

        TurnManager tm = Object.FindAnyObjectByType<TurnManager>();

        if (phase != 0 && (tm == null || Hero("PlayerEvanora") == null))
        {
            wait = 2;
            return;
        }

        switch (scenario)
        {
            // ---- Escenario 1: victoria con los cuatro vivos, heridos distinto ----
            case 0:
                if (phase == 0)
                {
                    log.AppendLine("ESCENARIO 1: victoria con los cuatro vivos (nivel 2, sin reliquias)");
                    StartSession(2, "");
                    Load("Assets/ScriptableObjects/Encounters/Pradera/P1.asset");
                    return;
                }

                if (phase == 1)
                {
                    int[] loss = { 3, 8, 0, 1 };

                    for (int i = 0; i < HeroIds.Length; i++)
                        if (loss[i] > 0)
                            Hero(HeroIds[i]).TakeDamage(loss[i]);

                    Snapshot("antes de ganar");
                    KillEnemies();
                    phase = 2;
                    wait = 3;
                    return;
                }

                if (phase == 2)
                {
                    Check(tm.BattleFinished, "el combate termino en victoria");

                    foreach (string id in HeroIds)
                    {
                        BattleUnit h = Hero(id);
                        Check(h.IsAlive && h.CurrentHealth == beforeWin[id],
                            id + ": vivo conserva la vida (" + beforeWin[id] + " -> " + h.CurrentHealth + "), sin curacion");
                    }

                    GameSession.PartyMemberState state;

                    foreach (string id in HeroIds)
                    {
                        GameSession.TryGetMember(id, out state);
                        Check(state.health == beforeWin[id], id + ": la foto de sesion guarda " + state.health + "/" + state.maxHealth);
                    }

                    scenario = 1;
                    phase = 0;
                    wait = 2;
                    return;
                }

                break;

            // ---- Escenario 2: la vida pasa a la siguiente mision y se gana con derrotados ----
            case 1:
                if (phase == 0)
                {
                    log.AppendLine("ESCENARIO 2: la vida se conserva en la mision siguiente (P2) y se gana con 2 derrotados");
                    Load("Assets/ScriptableObjects/Encounters/Pradera/P2.asset");
                    return;
                }

                if (phase == 1)
                {
                    foreach (string id in HeroIds)
                    {
                        BattleUnit h = Hero(id);
                        Check(h.CurrentHealth == beforeWin[id], id + ": empieza P2 con la vida de P1 (" + beforeWin[id] + " -> " + h.CurrentHealth + ")");
                    }

                    Hero("PlayerEvanora").TakeDamage(9999);      // derrotada
                    Hero("PlayerCaballero").TakeDamage(9999);    // derrotado
                    BattleUnit n = Hero("PlayerNoelle");
                    n.TakeDamage(n.CurrentHealth - 2);           // superviviente con 2 HP
                    Snapshot("antes de ganar");
                    KillEnemies();
                    phase = 2;
                    wait = 3;
                    return;
                }

                if (phase == 2)
                {
                    Check(tm.BattleFinished, "el combate termino en victoria");

                    foreach (string id in HeroIds)
                    {
                        BattleUnit h = Hero(id);

                        if (wasDead[id])
                        {
                            int expected = Mathf.Max(1, Mathf.CeilToInt(h.MaxHealth * 0.2f));
                            Check(h.IsAlive && h.CurrentHealth == expected,
                                id + ": derrotado revive con 20 % de " + h.MaxHealth + " = " + expected + " (real " + h.CurrentHealth + ")");
                        }
                        else
                        {
                            Check(h.CurrentHealth == beforeWin[id],
                                id + ": superviviente conserva " + beforeWin[id] + " (real " + h.CurrentHealth + ")");
                        }
                    }

                    GameSession.PartyMemberState state;

                    foreach (string id in HeroIds)
                    {
                        GameSession.TryGetMember(id, out state);
                        Check(state.health == Hero(id).CurrentHealth && state.health >= 1, id + ": foto de sesion " + state.health + "/" + state.maxHealth);
                    }

                    // Se guardan los valores para la mision siguiente.
                    foreach (string id in HeroIds)
                        beforeWin[id] = Hero(id).CurrentHealth;

                    scenario = 2;
                    phase = 0;
                    wait = 2;
                    return;
                }

                break;

            // ---- Escenario 3: continuidad tras revivir (P3) y pociones ----
            case 2:
                if (phase == 0)
                {
                    log.AppendLine("ESCENARIO 3: los revividos conservan su 20 % en la mision siguiente (P3) y las pociones no reviven");
                    Load("Assets/ScriptableObjects/Encounters/Pradera/P3.asset");
                    return;
                }

                if (phase == 1)
                {
                    foreach (string id in HeroIds)
                    {
                        BattleUnit h = Hero(id);
                        Check(h.IsAlive && h.CurrentHealth == beforeWin[id], id + ": empieza P3 con " + beforeWin[id] + " (real " + h.CurrentHealth + ")");
                    }

                    // Pociones: no reviven a un derrotado (en combate).
                    BattleUnit target = Hero("PlayerArquera");
                    target.TakeDamage(9999);
                    string reason;
                    bool can = ConsumableService.CanUse(GameSession.Consumable.Potion, target, out reason);
                    Check(!can && !target.IsAlive, "pocion sobre derrotado rechazada: \"" + reason + "\"");
                    string message;
                    bool used = ConsumableService.TryUse(GameSession.Consumable.Potion, target, out message);
                    Check(!used && !target.IsAlive && target.CurrentHealth == 0, "TryUse sobre derrotado no revive (HP " + target.CurrentHealth + ")");

                    // Pocion sobre un vivo herido: sigue curando (efecto actual sin cambios).
                    BattleUnit alive = Hero("PlayerNoelle");
                    alive.TakeDamage(Mathf.Max(0, alive.CurrentHealth - 3));
                    int hp = alive.CurrentHealth;
                    bool usedAlive = ConsumableService.TryUse(GameSession.Consumable.Potion, alive, out message);
                    Check(usedAlive && alive.CurrentHealth > hp, "pocion sobre vivo herido cura (" + hp + " -> " + alive.CurrentHealth + ")");

                    Snapshot("antes de ganar");
                    KillEnemies();
                    phase = 2;
                    wait = 3;
                    return;
                }

                if (phase == 2)
                {
                    BattleUnit a = Hero("PlayerArquera");
                    int expected = Mathf.Max(1, Mathf.CeilToInt(a.MaxHealth * 0.2f));
                    Check(a.IsAlive && a.CurrentHealth == expected, "Arquera (derrotada) revive con " + expected + " de " + a.MaxHealth + " (real " + a.CurrentHealth + ")");

                    foreach (string id in HeroIds)
                        if (id != "PlayerArquera")
                            Check(Hero(id).CurrentHealth == beforeWin[id], id + ": conserva " + beforeWin[id] + " (real " + Hero(id).CurrentHealth + ")");

                    scenario = 3;
                    phase = 0;
                    wait = 2;
                    return;
                }

                break;

            // ---- Escenario 4: reliquias de vida ----
            case 3:
                if (phase == 0)
                {
                    log.AppendLine("ESCENARIO 4: el 20 % usa la vida maxima con reliquias (nivel 5, Amuleto Vital en Evanora y Sello Sagrado en Noelle)");
                    StartSession(5, "E=amuleto_vital|colmillo_lobo;N=sello_sagrado");
                    Load("Assets/ScriptableObjects/Encounters/Pradera/P1.asset");
                    return;
                }

                if (phase == 1)
                {
                    BattleUnit e = Hero("PlayerEvanora");
                    BattleUnit n = Hero("PlayerNoelle");
                    log.AppendLine("   vida maxima: Evanora " + e.MaxHealth + " (nivel 5 base 26 + 4 reliquia = 30), Noelle " + n.MaxHealth + " (36 + 4 = 40)");
                    Check(e.MaxHealth == 30, "Evanora tiene 30 de vida maxima (26 + 4)");
                    Check(n.MaxHealth == 40, "Noelle tiene 40 de vida maxima (36 + 4)");
                    e.TakeDamage(9999);
                    n.TakeDamage(9999);
                    Snapshot("antes de ganar");
                    KillEnemies();
                    phase = 2;
                    wait = 3;
                    return;
                }

                if (phase == 2)
                {
                    BattleUnit e = Hero("PlayerEvanora");
                    BattleUnit n = Hero("PlayerNoelle");
                    Check(e.IsAlive && e.CurrentHealth == 6, "Evanora revive con ceil(30 x 0,2) = 6 (real " + e.CurrentHealth + ")");
                    Check(n.IsAlive && n.CurrentHealth == 8, "Noelle revive con ceil(40 x 0,2) = 8 (real " + n.CurrentHealth + ")");
                    scenario = 4;
                    phase = 0;
                    wait = 2;
                    return;
                }

                break;

            // ---- Escenario 5: derrota total = Game Over ----
            case 4:
                if (phase == 0)
                {
                    log.AppendLine("ESCENARIO 5: los cuatro derrotados -> derrota y Game Over");
                    StartSession(2, "");
                    Load("Assets/ScriptableObjects/Encounters/Pradera/P1.asset");
                    return;
                }

                if (phase == 1)
                {
                    foreach (string id in HeroIds)
                        Hero(id).TakeDamage(9999);

                    phase = 2;
                    wait = 3;
                    return;
                }

                if (phase == 2)
                {
                    Check(tm.BattleFinished, "el combate termino");
                    Check(GameSession.IsDefeated, "GameSession.IsDefeated = true (Game Over)");

                    foreach (string id in HeroIds)
                        Check(!Hero(id).IsAlive && Hero(id).CurrentHealth == 0, id + ": sigue derrotado, sin revivir");

                    Finish("completado");
                    return;
                }

                break;
        }
    }
}
#endif
