#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Herramienta de DESARROLLO (solo editor). Juega encuentros reales completos en la escena de combate con una politica
// sencilla de heroes (mejor ataque disponible, curacion a <=50 %) y los enemigos con su IA real (v0.2), y mide rondas,
// victorias, heroes caidos y vida restante. No forma parte del juego ni de las builds.
//
// Uso (en Play Mode, desde el mapa): CombatBalanceTest.Begin(lista, repeticiones); cuando Done sea true, leer Report().
// Cada combate usa la sesion con el nivel de heroes indicado y Random.InitState(semilla).
public static class CombatBalanceTest
{
    public struct Job
    {
        public string encounterPath;
        public int level;
        public bool noelleFree;
        public string label;
        // Reliquias equipadas, p. ej. "V=anillo_hierro|escudo_vigia;E=colmillo_lobo".
        // Claves: E Evanora, A Elaina (Arquera), N Noelle, V Valerian (Caballero).
        public string relics;
        // Campaña: no reinicia la sesion; conserva vida, XP y nivel del combate anterior (con la recuperacion poscombate real).
        public bool continueSession;
        // Composicion alternativa solo para la prueba (no toca el asset): nombres de prefab de enemigo separados por coma,
        // el primero en E1. Vacio = la del encuentro.
        public string comp;
        // Vida con la que empieza el grupo, en % de la maxima (0 = completa).
        public int hpPercent;
        // Refuerzo experimental del jefe (nombre de prefab de enemigo, p. ej. "AcechadorOscuro"); vacio = sin refuerzo.
        public string reinforce;
        // Semilla fija (0 = secuencial). Se usa la misma por repeticion para comparar versiones.
        public int seed;
    }

    public static string LastHeroStats = "";

    public class Run
    {
        public string label;
        public bool victory;
        public int rounds;
        public int dead;
        public float hpFraction;   // vida total restante de los heroes / vida maxima total
        public float minFraction;  // vida minima de un heroe
        public bool aborted;
        public string stats;
        public float startHp;
        public int startLevel;
    }

    static readonly string[] HeroIds = { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" };

    static readonly Queue<Job> queue = new Queue<Job>();
    static readonly List<Run> results = new List<Run>();
    static readonly List<EnemyArchetypeData> archetypes = new List<EnemyArchetypeData>();
    static readonly List<float> originalDelays = new List<float>();

    static Job current;
    static int seed = 1000;
    static int waitFrames;
    static bool loading;
    static bool active;
    static bool captured;
    static Run currentRun;
    static TurnManager turnManager;
    static PositionManager positions;
    static AbilitySystem abilities;
    static int lastKey = -1;
    static int safety;
    static float savedTimeScale = 1f;
    static bool savedRunInBackground;
    static int savedVSync;
    static int savedTarget;
    static float startTime;
    public static string Status = "";
    public static string OutputPath = "";
    // Si no es 0, la repeticion i de cada configuracion usa la semilla SeedBase + i (mismas semillas entre versiones).
    public static int SeedBase = 0;

    static Job WithSeed(Job job, int repetition)
    {
        if (SeedBase != 0)
            job.seed = SeedBase + repetition;

        return job;
    }
    // Si no esta vacia, guarda las lineas de registro relevantes del combate (para observar al jefe).
    public static string LogPath = "";
    static readonly List<string> logLines = new List<string>();

    public static int ErrorCount;

    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            ErrorCount++;
            logLines.Add("[ERROR] " + message.Split((char)10)[0]);
            return;
        }

        if (type == LogType.Log && (message.Contains("=== RONDA") || message.Contains("utiliza") || message.Contains("causa") || message.Contains("fase 2") ||
            message.Contains("recibe") || message.Contains("prepara") || message.Contains("lanza") || message.Contains("objetivo prioritario") ||
            message.Contains("esquiva") || message.Contains(" sufre ") || message.Contains(" cura a ") || message.Contains("se cancela") || message.Contains("Refuerzo") || message.Contains("VICTORIA") || message.Contains("DERROTA") || message.Contains("derrotado")))
            logLines.Add(message.Split((char)10)[0]);
    }

    public static bool Done => !active;

    public static void Begin(IEnumerable<Job> jobs, int repeats, bool sequence = false)
    {
        queue.Clear();
        results.Clear();

        if (sequence)
        {
            for (int i = 0; i < repeats; i++)
                foreach (Job job in jobs)
                    queue.Enqueue(WithSeed(job, i));
        }
        else
        {
            foreach (Job job in jobs)
                for (int i = 0; i < repeats; i++)
                    queue.Enqueue(WithSeed(job, i));
        }

        // Sin retardo de IA: cada turno enemigo se resuelve al instante (solo en memoria, se restaura al terminar).
        archetypes.Clear();
        originalDelays.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:EnemyArchetypeData"))
        {
            EnemyArchetypeData data = AssetDatabase.LoadAssetAtPath<EnemyArchetypeData>(AssetDatabase.GUIDToAssetPath(guid));
            archetypes.Add(data);
            originalDelays.Add(data.actionDelay);
            data.actionDelay = 0f;
        }

        // Velocidad: sin vsync, en segundo plano y sin limite de fotogramas (solo en memoria; se restaura al terminar).
        savedRunInBackground = Application.runInBackground;
        savedVSync = QualitySettings.vSyncCount;
        savedTarget = Application.targetFrameRate;
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        startTime = Time.realtimeSinceStartup;

        if (!string.IsNullOrEmpty(LogPath))
        {
            logLines.Clear();
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        savedTimeScale = Time.timeScale;
        active = true;
        loading = false;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        Status = "iniciado";
    }

    static void Finish()
    {
        EditorApplication.update -= Update;

        for (int i = 0; i < archetypes.Count; i++)
            archetypes[i].actionDelay = originalDelays[i];

        if (!string.IsNullOrEmpty(LogPath))
        {
            Application.logMessageReceived -= OnLog;
            System.IO.File.WriteAllText(LogPath, string.Join("\n", logLines.ToArray()));
        }

        Application.runInBackground = savedRunInBackground;
        QualitySettings.vSyncCount = savedVSync;
        Application.targetFrameRate = savedTarget;
        Time.timeScale = savedTimeScale;
        active = false;
        Status = "terminado";

        if (!string.IsNullOrEmpty(OutputPath))
            System.IO.File.WriteAllText(OutputPath, Status + "\n" + Report() + "\nfin\n");
    }

    static void Update()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish();
            Status = "cancelado (salio de Play)";
            return;
        }

        try
        {
            Step();
        }
        catch (System.Exception e)
        {
            Status = "ERROR: " + e;
            Finish();
        }
    }

    static void Step()
    {
        if (!loading)
        {
            if (queue.Count == 0)
            {
                Finish();
                return;
            }

            current = queue.Dequeue();
            StartRun();
            return;
        }

        if (waitFrames-- > 0)
            return;

        CombatIntroTransition intro = Object.FindAnyObjectByType<CombatIntroTransition>();

        if (intro != null)
            Object.Destroy(intro);   // la transicion es solo presentacion: se omite (OnDestroy restaura el tiempo)

        if (turnManager == null)
        {
            turnManager = Object.FindAnyObjectByType<TurnManager>();
            positions = Object.FindAnyObjectByType<PositionManager>();
            abilities = Object.FindAnyObjectByType<AbilitySystem>();

            if (turnManager == null || positions == null || abilities == null)
            {
                waitFrames = 2;
                return;
            }

            turnManager.OnBattleEnding += Capture;

            if (!string.IsNullOrEmpty(current.reinforce))
            {
                BattleUnit boss = null;

                foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
                    if (u.Side == TeamSide.Enemy && u.Data.isBoss)
                        boss = u;

                GameObject holder = new GameObject("RefuerzoExperimental");
                BossReinforcements reinforcements = holder.AddComponent<BossReinforcements>();
                reinforcements.boss = boss;
                reinforcements.reinforcementPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + current.reinforce + ".prefab");
            }

            // Vida inicial del grupo (0 = completa), para probar jefes con el grupo ya desgastado.
            if (current.hpPercent > 0 && current.hpPercent < 100)
                foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
                    if (u.Side == TeamSide.Player)
                        u.TakeDamage(u.CurrentHealth - Mathf.Max(1, Mathf.CeilToInt(u.MaxHealth * current.hpPercent / 100f)));

            StringBuilder stats = new StringBuilder();

            foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
                if (u.Side == TeamSide.Player)
                    stats.Append(u.gameObject.name.Replace("Player", "")).Append(' ').Append(u.MaxHealth).Append("PV ")
                        .Append(u.DamageMin).Append('-').Append(u.DamageMax).Append(" P").Append(Mathf.RoundToInt(u.ProtectionPercent))
                        .Append(" E").Append(Mathf.RoundToInt(u.EvasionPercent)).Append(" V").Append(u.Speed)
                        .Append(" C").Append(Mathf.RoundToInt(u.CriticalChance)).Append(" | ");

            LastHeroStats = stats.ToString();
            currentRun.stats = LastHeroStats;

            float sh = 0f, sm = 0f; int lv = 0;
            foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
                if (u.Side == TeamSide.Player)
                {
                    sh += u.CurrentHealth; sm += u.MaxHealth;
                    CharacterProgression cp = u.GetComponent<CharacterProgression>();
                    if (cp != null) lv = Mathf.Max(lv, cp.Level);
                }
            currentRun.startHp = sm > 0f ? sh / sm : 0f;
            currentRun.startLevel = lv;
            captured = false;
            safety = 0;
            lastKey = -1;
            return;
        }

        if (captured || turnManager.BattleFinished)
        {
            turnManager.OnBattleEnding -= Capture;
            results.Add(currentRun);
            if (!string.IsNullOrEmpty(OutputPath))
                System.IO.File.WriteAllText(OutputPath + ".parcial", "completados " + results.Count + "\n" + Report());
            turnManager = null;
            loading = false;
            Status = "completados " + results.Count + " en " + (Time.realtimeSinceStartup - startTime).ToString("0") + " s";
            return;
        }

        BattleUnit unit = turnManager.CurrentUnit;

        if (unit == null || unit.Side != TeamSide.Player || !unit.IsAlive)
            return;

        int key = System.Array.IndexOf(HeroIds, unit.gameObject.name) * 1000 + turnManager.CurrentRound;

        if (key == lastKey)
            return;

        lastKey = key;

        if (++safety > 400 || turnManager.CurrentRound > 60)
        {
            currentRun.aborted = true;
            currentRun.rounds = turnManager.CurrentRound;
            captured = true;
            return;
        }

        PlayHeroTurn(unit);

        if (!turnManager.BattleFinished)
            turnManager.EndTurn();
    }

    static void StartRun()
    {
        Random.InitState(current.seed != 0 ? current.seed : seed++);
        EncounterData encounter = AssetDatabase.LoadAssetAtPath<EncounterData>(current.encounterPath);

        if (!string.IsNullOrEmpty(current.comp))
        {
            encounter = Object.Instantiate(encounter);
            encounter.enemies = new List<EncounterData.EnemySlot>();
            string[] names = current.comp.Split(',');

            for (int i = 0; i < names.Length; i++)
            {
                string path = "Assets/Prefabs/Enemies/" + names[i] + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bosses/" + names[i] + ".prefab");

                encounter.enemies.Add(new EncounterData.EnemySlot { prefab = prefab, position = i + 1 });
            }
        }
        bool cont = current.continueSession && GameSession.IsActive && !GameSession.IsDefeated;

        if (!cont)
        {
            GameSession.StartNewGame();
            List<GameSession.PartyMemberState> party = new List<GameSession.PartyMemberState>();

            foreach (string id in HeroIds)
            {
                party.Add(new GameSession.PartyMemberState
                {
                    id = id,
                    level = current.level,
                    experience = 0,
                    health = 9999,   // se recorta a la vida maxima real al restaurar (empieza al 100 %)
                    maxHealth = 9999,
                    transformed = id == "PlayerNoelle" && current.noelleFree
                });
            }

            GameSession.SavePartySnapshot(party);
        }
        else if (current.noelleFree)
        {
            GameSession.TryLiberateNoelle();
        }

        EquipRelics(current.relics);
        EncounterLoader.DevEncounter = encounter;
        currentRun = new Run { label = current.label };
        loading = true;
        waitFrames = 3;
        turnManager = null;
        Time.timeScale = 1f;
        SceneManager.LoadScene("CombateIntegracion");
    }

    // Deja el inventario sin equipar y equipa solo lo indicado (cada unidad se crea si no existe).
    static void EquipRelics(string spec)
    {
        RelicInventory.Clear();

        if (string.IsNullOrEmpty(spec))
            return;

        string[] ids = { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" };
        string keys = "EANV";

        foreach (string part in spec.Split(';'))
        {
            string[] kv = part.Split('=');

            if (kv.Length != 2 || kv[0].Length != 1)
                continue;

            string heroId = ids[keys.IndexOf(kv[0][0])];
            string[] types = kv[1].Split('|');

            for (int slot = 0; slot < types.Length && slot < RelicInventory.SlotsPerHero; slot++)
            {
                int uid = RelicInventory.Add(types[slot]);

                if (uid == 0)
                {
                    Debug.LogWarning("Reliquia no agregada: " + types[slot]);
                    continue;
                }

                string reason;

                if (!RelicInventory.TryEquip(uid, heroId, slot, out reason))
                    Debug.LogWarning("No se pudo equipar " + types[slot] + " en " + heroId + ": " + reason);
            }
        }
    }

    static void Capture(bool victory)
    {
        if (captured)
            return;

        captured = true;
        currentRun.victory = victory;
        currentRun.rounds = turnManager.CurrentRound;

        float hp = 0f, max = 0f, min = 1f;
        int dead = 0;

        foreach (BattleUnit unit in Object.FindObjectsByType<BattleUnit>())
        {
            if (unit.Side != TeamSide.Player)
                continue;

            hp += unit.CurrentHealth;
            max += unit.MaxHealth;
            min = Mathf.Min(min, (float)unit.CurrentHealth / unit.MaxHealth);

            if (!unit.IsAlive)
                dead++;
        }

        currentRun.dead = dead;
        currentRun.hpFraction = max > 0f ? hp / max : 0f;
        currentRun.minFraction = min;
    }

    // ------------------------------------------------------------
    // Politica de heroes
    // ------------------------------------------------------------

    public static bool TacticalPolicy = false;

    static void PlayHeroTurn(BattleUnit hero)
    {
        if (TacticalPolicy)
        {
            CampaignTest.PlayExternal(hero, abilities, positions);
            return;
        }

        CharacterAbilityLoadout loadout = hero.GetComponent<CharacterAbilityLoadout>();
        AbilityCooldownController cooldowns = hero.GetComponent<AbilityCooldownController>();

        if (loadout == null)
            return;

        AbilityData bestAbility = null;
        BattleUnit bestTarget = null;
        float bestScore = float.MinValue;

        foreach (AbilityData ability in loadout.Abilities)
        {
            if (ability == null || ability.requiresPreparation || ability.targetType != AbilityTargetType.Single)
                continue;

            if (!ability.CanUseFromPosition(hero.Position) || (cooldowns != null && !cooldowns.IsReady(ability)))
                continue;

            bool attack = ability.abilityType == AbilityType.Attack;
            bool heal = ability.abilityType == AbilityType.Heal;

            if (!attack && !heal)
                continue;

            if (attack && !ability.dealsDamage)
                continue;

            TeamSide side = attack ? TeamSide.Enemy : TeamSide.Player;

            for (int position = 1; position <= 4; position++)
            {
                if (!ability.CanTargetPosition(position))
                    continue;

                BattleUnit target = positions.GetUnitAtPosition(side, position);

                if (target == null || !target.IsAlive)
                    continue;

                float score;

                if (attack)
                {
                    int damage = DamageCalculator.ExpectedDamage(hero, target, ability.basePower, ability.damagePercent, ability.defensePenetration);
                    score = damage + (damage >= target.CurrentHealth ? 4f : 0f) + (1f - (float)target.CurrentHealth / target.MaxHealth) * 2f;
                }
                else
                {
                    float fraction = (float)target.CurrentHealth / target.MaxHealth;

                    if (fraction > 0.5f)
                        continue;

                    score = 20f + (1f - fraction) * 10f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAbility = ability;
                    bestTarget = target;
                }
            }
        }

        if (bestAbility != null)
            abilities.ExecuteAbility(hero, bestTarget, bestAbility);
    }

    // ------------------------------------------------------------
    // Suites predefinidas (campana real: nivel esperado y reliquias comprables con las Coronas de ese momento)
    // ------------------------------------------------------------

    static readonly string[] SuiteIds = { "P1", "P2", "P3", "P4", "B1", "B2", "B3", "C1", "C2", "BOSS" };
    static readonly string[] SuitePaths =
    {
        "Pradera/P1", "Pradera/P2", "Pradera/P3", "Pradera/P4", "Bosque/B1", "Bosque/B2", "Bosque/B3",
        "Corrupcion/C1", "Corrupcion/C2", "Jefe/BOSS_Capitan"
    };
    static readonly int[] SuiteLevels = { 1, 1, 2, 2, 2, 3, 3, 4, 4, 5 };

    const string R2 = "V=anillo_hierro;N=amuleto_vital";
    const string R3 = R2 + ";E=colmillo_lobo";
    const string R4 = R3 + ";A=colmillo_lobo";
    const string R5 = "V=anillo_hierro|amuleto_vital;N=amuleto_vital;E=colmillo_lobo;A=colmillo_lobo";
    const string R6 = "V=anillo_hierro|amuleto_vital;N=amuleto_vital|anillo_hierro;E=colmillo_lobo;A=colmillo_lobo";
    const string R7 = "V=anillo_hierro|amuleto_vital;N=amuleto_vital|anillo_hierro;E=colmillo_lobo|amuleto_vital;A=colmillo_lobo";
    const string R8 = "V=anillo_hierro|amuleto_vital;N=amuleto_vital|anillo_hierro;E=colmillo_lobo|amuleto_vital;A=colmillo_lobo|amuleto_vital";
    static readonly string[] SuiteRelics = { "", "", R2, R2, R3, R4, R5, R6, R7, R8 };

    // Composiciones candidatas por version (vacio = la actual del asset).
    public static string[] Comp(string version)
    {
        switch (version)
        {
            case "v1":
                return new[]
                {
                    "", // P1
                    "GoblinComun,GoblinComun,GoblinArquero", // P2
                    "GoblinBruto,GoblinComun,GoblinArquero", // P3
                    "GoblinBruto,GoblinComun,GoblinComun,GoblinArquero", // P4
                    "LoboSalvaje,LoboSalvaje,Bandido", // B1
                    "Bandido,LoboSalvaje,LoboSalvaje,ChamanBosque", // B2
                    "GuardianForestal,Bandido,LoboSalvaje,ChamanBosque", // B3
                    "CorruptoErrante,AcechadorOscuro,CorruptoErrante,HechiceroCorrupto", // C1
                    "GuardianCorrupto,CorruptoErrante,AcechadorOscuro,HechiceroCorrupto", // C2
                    ""
                };
            default:
                return new string[10];
        }
    }

    // ids: lista separada por comas ("P3,B2") o "*" para todos. withRelics: usa las reliquias obtenibles del momento.
    public static List<Job> Suite(string ids, string compVersion, bool withRelics)
    {
        List<Job> list = new List<Job>();
        string[] comp = Comp(compVersion);

        for (int i = 0; i < SuiteIds.Length; i++)
        {
            if (ids != "*" && System.Array.IndexOf(ids.Split(','), SuiteIds[i]) < 0)
                continue;

            if (withRelics && string.IsNullOrEmpty(SuiteRelics[i]))
                continue;

            int n = string.IsNullOrEmpty(SuiteRelics[i]) ? 0 : SuiteRelics[i].Split('|', ';').Length;
            int enemies = string.IsNullOrEmpty(comp[i]) ? -1 : comp[i].Split(',').Length;

            list.Add(new Job
            {
                encounterPath = "Assets/ScriptableObjects/Encounters/" + SuitePaths[i] + ".asset",
                level = SuiteLevels[i],
                relics = withRelics ? SuiteRelics[i] : "",
                noelleFree = i >= 5 || SuiteIds[i] == "BOSS",
                comp = comp[i],
                label = SuiteIds[i] + " " + compVersion + " Nv" + SuiteLevels[i] + " " + (withRelics ? n : 0) + "rel" + (enemies > 0 ? " (" + enemies + "en)" : "")
            });
        }

        return list;
    }

    // ------------------------------------------------------------
    // Resultados
    // ------------------------------------------------------------

    public static string Report()
    {
        Dictionary<string, List<Run>> groups = new Dictionary<string, List<Run>>();
        List<string> order = new List<string>();

        foreach (Run run in results)
        {
            if (!groups.ContainsKey(run.label))
            {
                groups[run.label] = new List<Run>();
                order.Add(run.label);
            }

            groups[run.label].Add(run);
        }

        StringBuilder sb = new StringBuilder();

        foreach (string label in order)
        {
            List<Run> list = groups[label];
            int wins = 0, deadTotal = 0, aborted = 0, minRounds = int.MaxValue, maxRounds = 0;
            float rounds = 0f, hp = 0f, minHp = 0f, startHp = 0f, startLv = 0f;

            foreach (Run run in list)
            {
                if (run.victory) wins++;
                if (run.aborted) aborted++;
                deadTotal += run.dead;
                rounds += run.rounds;
                hp += run.hpFraction;
                minHp += run.minFraction;
                startHp += run.startHp;
                startLv += run.startLevel;
                minRounds = Mathf.Min(minRounds, run.rounds);
                maxRounds = Mathf.Max(maxRounds, run.rounds);
            }

            int n = list.Count;
            sb.AppendLine(label + ": " + wins + "/" + n + " victorias | rondas media " + (rounds / n).ToString("0.0") +
                " (min " + minRounds + ", max " + maxRounds + ") | heroes caidos por combate " + ((float)deadTotal / n).ToString("0.00") +
                " | vida final del grupo " + Mathf.RoundToInt(hp / n * 100f) + " % | vida minima media " + Mathf.RoundToInt(minHp / n * 100f) + " %" +
                " | inicio: Nv " + (startLv / n).ToString("0.0") + " vida " + Mathf.RoundToInt(startHp / n * 100f) + " %" +
                (aborted > 0 ? " | abortados " + aborted : ""));
            sb.AppendLine("    stats: " + list[0].stats);
        }

        return sb.ToString();
    }
}
#endif
