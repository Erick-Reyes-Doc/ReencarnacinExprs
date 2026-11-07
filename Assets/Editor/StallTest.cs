#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Herramienta de DESARROLLO (solo editor). Prueba de "combate alargado": el grupo empieza al 30 % de vida, solo queda
// vivo UN enemigo de cada tipo y los heroes (salvo Noelle, que usa su politica de apoyo) pasan el turno. Mide cuanta vida
// puede recuperar el grupo con las curaciones de Noelle durante los ultimos turnos de un combate y si llega a
// "curarse del todo" mientras el enemigo sigue atacando.
// Uso (Play Mode, desde el mapa): StallTest.Begin(rutaSalida, repeticiones).
public static class StallTest
{
    static readonly string[] HeroIds = { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" };

    struct Variant
    {
        public string label, path, keep;
        public int level;
    }

    static readonly Variant[] Variants =
    {
        new Variant { label = "B2 Chaman solo (Nv3)", path = "Bosque/B2", keep = "cham", level = 3 },
        new Variant { label = "B3 Guardian Forestal solo (Nv3)", path = "Bosque/B3", keep = "guardi", level = 3 },
        new Variant { label = "C2 Hechicero solo (Nv4)", path = "Corrupcion/C2", keep = "hechic", level = 4 },
        new Variant { label = "BOSS Capitan solo (Nv5)", path = "Jefe/BOSS_Capitan", keep = "capit", level = 5 },
    };

    class Row
    {
        public string label;
        public List<int> pct = new List<int>();   // % de vida del grupo al inicio de cada ronda
        public int deadAtEnd;
        public int roundTo60 = -1, roundTo80 = -1, roundTo95 = -1;
        public int heals, regens, buffs;
    }

    static readonly List<Row> rows = new List<Row>();
    static readonly List<(int variant, int seed)> queue = new List<(int, int)>();
    static readonly List<EnemyArchetypeData> archetypes = new List<EnemyArchetypeData>();
    static readonly List<float> delays = new List<float>();

    static string outPath;
    static bool active;
    static int wait;
    static int step;          // 0 = cargar, 1 = esperar escena, 2 = en curso
    static int current;
    static Row row;
    static TurnManager tm;
    static AbilitySystem abilities;
    static int lastRound;
    static int lastKey;
    static int guard;
    static bool savedRun;
    static int savedVSync, savedTarget;

    public static bool Done => !active;
    public static string Status = "";

    public static void Begin(string path, int repeats)
    {
        outPath = path;
        rows.Clear();
        queue.Clear();

        for (int v = 0; v < Variants.Length; v++)
            for (int i = 0; i < repeats; i++)
                queue.Add((v, 9000 + v * 100 + i));

        archetypes.Clear();
        delays.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:EnemyArchetypeData"))
        {
            EnemyArchetypeData data = AssetDatabase.LoadAssetAtPath<EnemyArchetypeData>(AssetDatabase.GUIDToAssetPath(guid));
            archetypes.Add(data);
            delays.Add(data.actionDelay);
            data.actionDelay = 0f;
        }

        savedRun = Application.runInBackground;
        savedVSync = QualitySettings.vSyncCount;
        savedTarget = Application.targetFrameRate;
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        current = 0;
        step = 0;
        active = true;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        Status = "iniciado";
    }

    static void Finish(string status)
    {
        EditorApplication.update -= Update;

        for (int i = 0; i < archetypes.Count; i++)
            archetypes[i].actionDelay = delays[i];

        Application.runInBackground = savedRun;
        QualitySettings.vSyncCount = savedVSync;
        Application.targetFrameRate = savedTarget;
        Time.timeScale = 1f;
        active = false;
        Status = status;
        System.IO.File.WriteAllText(outPath, status + "\n" + Report() + "\nfin\n");
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
            Finish("ERROR: " + e);
        }
    }

    static BattleUnit Hero(int i)
    {
        foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
            if (u.Side == TeamSide.Player && u.gameObject.name == HeroIds[i])
                return u;

        return null;
    }

    static int GroupPercent()
    {
        int hp = 0, max = 0;

        for (int i = 0; i < 4; i++)
        {
            BattleUnit h = Hero(i);

            if (h == null)
                continue;

            hp += h.CurrentHealth;
            max += h.MaxHealth;
        }

        return max > 0 ? Mathf.RoundToInt(100f * hp / max) : 0;
    }

    static void Tick()
    {
        if (wait-- > 0)
            return;

        if (step == 0)
        {
            if (current >= queue.Count)
            {
                Finish("terminado");
                return;
            }

            Variant v = Variants[queue[current].variant];
            Random.InitState(queue[current].seed);
            GameSession.StartNewGame();
            RelicInventory.Clear();

            List<GameSession.PartyMemberState> party = new List<GameSession.PartyMemberState>();

            foreach (string id in HeroIds)
                party.Add(new GameSession.PartyMemberState
                {
                    id = id, level = v.level, experience = 0, health = 9999, maxHealth = 9999,
                    transformed = id == "PlayerNoelle"
                });

            GameSession.SavePartySnapshot(party);
            EncounterLoader.DevEncounter = AssetDatabase.LoadAssetAtPath<EncounterData>("Assets/ScriptableObjects/Encounters/" + v.path + ".asset");
            Time.timeScale = 1f;
            row = new Row { label = v.label };
            step = 1;
            wait = 6;
            SceneManager.LoadScene("CombateIntegracion");
            return;
        }

        CombatIntroTransition intro = Object.FindAnyObjectByType<CombatIntroTransition>();

        if (intro != null)
            Object.Destroy(intro);

        if (step == 1)
        {
            tm = Object.FindAnyObjectByType<TurnManager>();
            abilities = Object.FindAnyObjectByType<AbilitySystem>();

            if (tm == null || abilities == null || Hero(0) == null || Hero(3) == null)
            {
                wait = 1;
                return;
            }

            Variant v = Variants[queue[current].variant];

            // Grupo al 30 %.
            for (int i = 0; i < 4; i++)
            {
                BattleUnit h = Hero(i);
                int target = Mathf.Max(1, Mathf.CeilToInt(h.MaxHealth * 0.30f));
                h.TakeDamage(h.CurrentHealth - target);
            }

            // Solo queda un enemigo del tipo indicado.
            bool kept = false;

            foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
            {
                if (u.Side != TeamSide.Enemy || !u.IsAlive)
                    continue;

                if (!kept && u.Data.unitName.ToLowerInvariant().Contains(v.keep))
                    kept = true;
                else
                    u.TakeDamage(9999);
            }

            lastRound = -1;
            lastKey = -1;
            guard = 0;
            step = 2;
            return;
        }

        // Combate en curso.
        if (tm.BattleFinished || tm.CurrentRound > 12 || ++guard > 20000)
        {
            EndRow();
            return;
        }

        if (tm.CurrentRound != lastRound)
        {
            lastRound = tm.CurrentRound;
            int p = GroupPercent();
            row.pct.Add(p);

            int r = row.pct.Count - 1;

            if (p >= 60 && row.roundTo60 < 0) row.roundTo60 = r;
            if (p >= 80 && row.roundTo80 < 0) row.roundTo80 = r;
            if (p >= 95 && row.roundTo95 < 0) row.roundTo95 = r;

            if (p >= 98)
            {
                EndRow();
                return;
            }
        }

        BattleUnit unit = tm.CurrentUnit;

        if (unit == null || unit.Side != TeamSide.Player || !unit.IsAlive)
            return;

        int key = System.Array.IndexOf(HeroIds, unit.gameObject.name) * 1000 + tm.CurrentRound;

        if (key == lastKey)
            return;

        lastKey = key;

        if (unit.gameObject.name == "PlayerNoelle")
        {
            int healed;
            string kind = CampaignTest.NoelleSupport(unit, abilities, out healed);

            if (kind == "cura") row.heals++;
            else if (kind == "bendicion") row.regens++;
            else if (kind == "amparo") row.buffs++;
        }

        if (!tm.BattleFinished)
            tm.EndTurn();
    }

    static void EndRow()
    {
        int dead = 0;

        for (int i = 0; i < 4; i++)
        {
            BattleUnit h = Hero(i);

            if (h != null && !h.IsAlive)
                dead++;
        }

        row.deadAtEnd = dead;
        row.pct.Add(GroupPercent());
        rows.Add(row);
        current++;
        step = 0;
        wait = 1;
    }

    static string Report()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Grupo al 30 % de vida; un solo enemigo; los heroes pasan el turno salvo Noelle (curas unicas, Bendicion Continua, Amparo).");
        sb.AppendLine("Columnas: % de vida del grupo al empezar cada ronda (la primera es la ronda 1).");

        string last = null;

        foreach (Row r in rows)
        {
            if (r.label != last)
            {
                sb.AppendLine();
                sb.AppendLine("== " + r.label);
                last = r.label;
            }

            sb.AppendLine("   " + string.Join(" > ", r.pct.ConvertAll(x => x + "%").ToArray()) +
                " | caidos " + r.deadAtEnd + " | llega a 60%: " + (r.roundTo60 >= 0 ? "ronda " + (r.roundTo60 + 1) : "nunca") +
                ", 80%: " + (r.roundTo80 >= 0 ? "ronda " + (r.roundTo80 + 1) : "nunca") +
                ", 95%: " + (r.roundTo95 >= 0 ? "ronda " + (r.roundTo95 + 1) : "nunca") +
                " | curas " + r.heals + ", Bendicion " + r.regens + ", Amparo " + r.buffs);
        }

        return sb.ToString();
    }
}
#endif
