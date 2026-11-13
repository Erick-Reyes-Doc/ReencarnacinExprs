#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Herramienta de DESARROLLO (solo editor). Comprueba la regla del encuentro del Capitan: si el Capitan cae con el Errante
// vivo, el Errante huye (sin actuar, sin contar como baja ni dar recompensas) y el combate termina en victoria.
// 4 escenarios: S1 cae primero el Capitan, S2 cae primero el Errante, S3 el Errante tiene una accion pendiente y el
// Capitan cae, S4 derrota total. Uso (Play Mode, desde el mapa): CaptainFleeTest.Begin(rutaSalida).
public static class CaptainFleeTest
{
    static readonly string[] HeroIds = { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" };
    static readonly StringBuilder log = new StringBuilder();
    static readonly List<EnemyArchetypeData> archetypes = new List<EnemyArchetypeData>();
    static readonly List<float> delays = new List<float>();

    static string outPath;
    static bool active;
    static int scenario;
    static int step;
    static int wait;
    static int fails, checks;
    static TurnManager tm;
    static BattleUnit boss, errante;
    static CombatHudController hud;
    static int coinsBefore;
    static int heroHpBefore;
    static float timer;

    // Contadores del registro.
    static int msgFled, msgErranteDefeated, expLines, coinLines, victoryLines;

    public static bool Done => !active;

    public static void Begin(string path)
    {
        outPath = path;
        log.Length = 0;
        fails = 0;
        checks = 0;
        scenario = 1;
        step = 0;
        wait = 0;
        archetypes.Clear();
        delays.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:EnemyArchetypeData"))
        {
            EnemyArchetypeData data = AssetDatabase.LoadAssetAtPath<EnemyArchetypeData>(AssetDatabase.GUIDToAssetPath(guid));
            archetypes.Add(data);
            delays.Add(data.actionDelay);
            data.actionDelay = 0f;
        }

        Application.runInBackground = true;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        active = true;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    static void OnLog(string m, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            log.AppendLine("   [ERROR DE CONSOLA] " + m.Split((char)10)[0]);
            fails++;
            return;
        }

        if (m.Contains("El Capitán ha caído") && m.Contains("huye"))
            msgFled++;

        if (m.Contains("Corrupto Errante ha sido derrotado"))
            msgErranteDefeated++;

        if (m.StartsWith("Experiencia de victoria: 200"))
            expLines++;

        if (m.StartsWith("Recompensa: +100 Coronas"))
            coinLines++;

        if (m.StartsWith("¡VICTORIA!"))
            victoriaLinesInc();
    }

    static void victoriaLinesInc()
    {
        victoryLines++;
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
        Application.logMessageReceived -= OnLog;

        for (int i = 0; i < archetypes.Count; i++)
            archetypes[i].actionDelay = delays[i];

        active = false;
        log.AppendLine();
        log.AppendLine("Comprobaciones: " + checks + " | fallidas (incluye errores de consola): " + fails + " | " + status);
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

    static int AliveEnemies()
    {
        int n = 0;

        foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
            if (u.Side == TeamSide.Enemy && u.IsAlive)
                n++;

        return n;
    }

    static int PartyHealth()
    {
        int sum = 0;

        foreach (string id in HeroIds)
            sum += Hero(id).CurrentHealth;

        return sum;
    }

    static bool VictoryBannerVisible()
    {
        foreach (Text t in Resources.FindObjectsOfTypeAll<Text>())
            if (t.gameObject.scene.IsValid() && t.gameObject.activeInHierarchy && t.text != null && t.text.Contains("VICTORIA"))
                return true;

        return false;
    }

    static void LoadCaptain()
    {
        GameSession.StartNewGame();
        RelicInventory.Clear();
        List<GameSession.PartyMemberState> party = new List<GameSession.PartyMemberState>();

        foreach (string id in HeroIds)
            party.Add(new GameSession.PartyMemberState { id = id, level = 5, health = 9999, maxHealth = 9999, transformed = id == "PlayerNoelle" });

        GameSession.SavePartySnapshot(party);
        EncounterLoader.DevEncounter = AssetDatabase.LoadAssetAtPath<EncounterData>("Assets/ScriptableObjects/Encounters/Jefe/BOSS_Capitan.asset");
        msgFled = msgErranteDefeated = expLines = coinLines = victoryLines = 0;
        Time.timeScale = 1f;
        wait = 8;
        SceneManager.LoadScene("CombateIntegracion");
    }

    // Espera a que el combate este en curso y localiza a las unidades.
    static bool BattleReady()
    {
        CombatIntroTransition intro = Object.FindAnyObjectByType<CombatIntroTransition>();

        if (intro != null)
            Object.Destroy(intro);

        tm = Object.FindAnyObjectByType<TurnManager>();
        hud = Object.FindAnyObjectByType<CombatHudController>();

        if (tm == null || hud == null || !tm.BattleActive || Hero("PlayerCaballero") == null)
            return false;

        boss = null;
        errante = null;

        foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
        {
            if (u.Side != TeamSide.Enemy)
                continue;

            if (u.Data.isBoss) boss = u;
            else errante = u;
        }

        return boss != null && errante != null;
    }

    static void Tick()
    {
        if (wait-- > 0)
            return;

        switch (scenario)
        {
            case 1: Scenario1(); break;
            case 2: Scenario2(); break;
            case 3: Scenario3(); break;
            case 4: Scenario4(); break;
            default: Finish("completado"); break;
        }
    }

    static void Next()
    {
        scenario++;
        step = 0;
        wait = 2;
    }

    // S1: el Capitan cae primero con el Errante vivo.
    static void Scenario1()
    {
        if (step == 0)
        {
            log.AppendLine("S1: el Capitan cae primero (Errante vivo, un heroe caido)");
            LoadCaptain();
            step = 1;
            return;
        }

        if (step == 1)
        {
            if (!BattleReady())
            {
                wait = 1;
                return;
            }

            Check(hud.TileUnit(4) == boss && hud.TileUnit(5) == errante, "HUD antes: tarjeta del jefe en E1 y del Errante en E2");
            coinsBefore = GameSession.Coins;
            Hero("PlayerEvanora").TakeDamage(9999);                    // un heroe caido: debe revivir al 20 % tras la victoria
            Hero("PlayerArquera").TakeDamage(Mathf.Max(0, Hero("PlayerArquera").CurrentHealth - 11));   // un superviviente herido (11 PV)
            heroHpBefore = Hero("PlayerArquera").CurrentHealth;
            boss.TakeDamage(9999);
            step = 2;
            wait = 12;
            return;
        }

        if (step == 2)
        {
            Check(tm.BattleFinished && !tm.PartyDefeated, "el combate termina en victoria al caer el Capitan");
            Check(errante.HasFled && !errante.IsAlive && errante.CurrentHealth > 0, "el Errante huyo (no murio: vida " + errante.CurrentHealth + "/" + errante.MaxHealth + ")");
            Check(msgFled == 1, "mensaje de huida en el registro exactamente una vez (" + msgFled + ")");
            Check(msgErranteDefeated == 0, "la huida no cuenta como derrota del Errante (mensajes de derrota: " + msgErranteDefeated + ")");
            Check(expLines == 1 && tm.LastExperienceAwarded == 200, "XP entregada una sola vez: " + tm.LastExperienceAwarded + " (lineas " + expLines + ")");
            Check(coinLines == 1 && GameSession.Coins - coinsBefore == 100, "Coronas entregadas una sola vez: +" + (GameSession.Coins - coinsBefore) + " (lineas " + coinLines + ")");
            Check(victoryLines == 1, "victoria anunciada una sola vez (" + victoryLines + ")");
            BattleUnit evanora = Hero("PlayerEvanora");
            int expectedRevive = Mathf.Max(1, Mathf.CeilToInt(evanora.MaxHealth * 0.2f));
            Check(evanora.IsAlive && evanora.CurrentHealth == expectedRevive, "recuperacion: la heroina caida revive con el 20 % (" + evanora.CurrentHealth + "/" + evanora.MaxHealth + ", esperado " + expectedRevive + ")");
            Check(Hero("PlayerArquera").CurrentHealth == heroHpBefore, "recuperacion: el superviviente conserva su vida (" + Hero("PlayerArquera").CurrentHealth + ")");
            Check(hud.TileUnit(4) == null && hud.TileUnit(5) == null, "HUD despues: ya no hay tarjeta del jefe ni del enemigo que huyo");
            SpriteRenderer sprite = errante.GetComponent<SpriteRenderer>();
            Check(sprite == null || !sprite.enabled, "el sprite del Errante se oculta al huir");
            Check(AliveEnemies() == 0, "no queda ningun enemigo activo");
            step = 3;
            wait = 6;
            return;
        }

        if (step == 3)
        {
            Check(VictoryBannerVisible(), "el banner/panel de VICTORIA se muestra");
            Next();
        }
    }

    // S2: el Errante cae primero; despues el Capitan.
    static void Scenario2()
    {
        if (step == 0)
        {
            log.AppendLine("S2: el Errante cae primero, luego el Capitan");
            LoadCaptain();
            step = 1;
            return;
        }

        if (step == 1)
        {
            if (!BattleReady())
            {
                wait = 1;
                return;
            }

            coinsBefore = GameSession.Coins;
            errante.TakeDamage(9999);
            step = 2;
            wait = 8;
            return;
        }

        if (step == 2)
        {
            Check(!tm.BattleFinished && boss.IsAlive, "con el Errante muerto el combate continua hasta derrotar al Capitan");
            Check(msgFled == 0 && !errante.HasFled, "no hay huida: el Errante murio (mensajes de huida: " + msgFled + ")");
            Check(hud.TileUnit(5) == null && hud.TileUnit(4) == boss, "HUD: la tarjeta del Errante desaparece y la del jefe sigue");
            boss.TakeDamage(9999);
            step = 3;
            wait = 12;
            return;
        }

        if (step == 3)
        {
            Check(tm.BattleFinished && !tm.PartyDefeated, "al caer el Capitan termina la victoria");
            Check(msgFled == 0, "sin mensaje de huida al caer el ultimo enemigo (" + msgFled + ")");
            Check(expLines == 1 && coinLines == 1 && GameSession.Coins - coinsBefore == 100, "recompensas una sola vez: XP lineas " + expLines + ", Coronas +" + (GameSession.Coins - coinsBefore));
            Check(hud.TileUnit(4) == null && hud.TileUnit(5) == null, "HUD: sin tarjetas enemigas al terminar");
            Next();
        }
    }

    // S3: el Errante tiene una accion pendiente (retardo) cuando cae el Capitan.
    static void Scenario3()
    {
        if (step == 0)
        {
            log.AppendLine("S3: el Errante tiene una accion pendiente cuando cae el Capitan");

            foreach (EnemyArchetypeData a in archetypes)
                if (a.name.Contains("CorruptoErrante"))
                    a.actionDelay = 1.5f;

            LoadCaptain();
            step = 1;
            return;
        }

        if (step == 1)
        {
            if (!BattleReady())
            {
                wait = 1;
                return;
            }

            step = 2;
            return;
        }

        if (step == 2)
        {
            foreach (string id in HeroIds)
                Hero(id).Heal(9999);

            if (tm.BattleFinished)
            {
                Check(false, "el combate termino antes de poder probar la accion pendiente");
                Next();
                return;
            }

            BattleUnit current = tm.CurrentUnit;

            if (current != null && current == errante)
            {
                // El Errante ha empezado su turno y su accion esta en espera (retardo 1,5 s).
                heroHpBefore = PartyHealth();
                boss.TakeDamage(9999);
                timer = Time.realtimeSinceStartup;
                step = 3;
                return;
            }

            if (current != null && current.Side == TeamSide.Player && current.IsAlive)
                tm.EndTurn();

            return;
        }

        if (step == 3)
        {
            if (Time.realtimeSinceStartup - timer < 2.5f)
                return;

            Check(tm.BattleFinished && !tm.PartyDefeated && errante.HasFled, "el combate termino en victoria y el Errante huyo");
            Check(PartyHealth() == heroHpBefore, "la accion pendiente del Errante se cancelo: la vida del grupo no cambio (" + heroHpBefore + " -> " + PartyHealth() + ")");
            Check(expLines == 1 && coinLines == 1, "recompensas una sola vez (XP " + expLines + ", Coronas " + coinLines + ")");

            foreach (EnemyArchetypeData a in archetypes)
                if (a.name.Contains("CorruptoErrante"))
                    a.actionDelay = 0f;

            Next();
        }
    }

    // S4: derrota total: la regla no la convierte en victoria.
    static void Scenario4()
    {
        if (step == 0)
        {
            log.AppendLine("S4: derrota total");
            LoadCaptain();
            step = 1;
            return;
        }

        if (step == 1)
        {
            if (!BattleReady())
            {
                wait = 1;
                return;
            }

            foreach (string id in HeroIds)
                Hero(id).TakeDamage(9999);

            step = 2;
            wait = 12;
            return;
        }

        if (step == 2)
        {
            Check(tm.BattleFinished && tm.PartyDefeated && GameSession.IsDefeated, "derrota total y sesion perdida");
            Check(!errante.HasFled && errante.IsAlive && boss.IsAlive, "en la derrota los enemigos no huyen");
            Check(victoryLines == 0 && msgFled == 0 && expLines == 0 && coinLines == 0, "sin victoria ni recompensas en la derrota");
            Next();
        }
    }
}
#endif
