#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Herramienta de DESARROLLO (solo editor). Simula campanas completas encadenadas P1 -> ... -> Capitan con el combate
// REAL (escena de combate, IA v0.2, habilidades, estados, curaciones), conservando entre combates vida, XP, nivel,
// Coronas, consumibles y reliquias. Entre misiones el "jugador" compra y equipa con las Coronas realmente disponibles
// y usa pociones en el mapa. Las reliquias del conjunto de prueba se descartan al empezar cada campana.
//
// Estilos: A = sin compras (solo la pocion inicial) | B = equilibrada (pociones, antidotos y reliquias) |
//          C = centrada en reliquias (reliquias primero; consumibles solo con lo que sobra).
//
// Uso (Play Mode, desde el mapa): CampaignTest.Begin("ABC", campanasPorEstilo, rutaSalida).
public static class CampaignTest
{
    static readonly string[] HeroIds = { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" };
    static readonly string[] HeroShort = { "Evanora", "Elaina", "Noelle", "Valerian" };

    static readonly string[] MissionIds = { "P1", "P2", "P3", "P4", "B1", "B2", "B3", "C1", "C2", "BOSS" };
    static readonly string[] MissionPaths =
    {
        "Pradera/P1", "Pradera/P2", "Pradera/P3", "Pradera/P4", "Bosque/B1", "Bosque/B2", "Bosque/B3",
        "Corrupcion/C1", "Corrupcion/C2", "Jefe/BOSS_Capitan"
    };

    // Reliquias de la tienda (las unicas obtenibles) en orden de preferencia: (heroe, tipo).
    static readonly string[] WishHero = { "PlayerCaballero", "PlayerNoelle", "PlayerEvanora", "PlayerArquera", "PlayerCaballero", "PlayerEvanora", "PlayerNoelle", "PlayerArquera" };
    static readonly string[] WishType = { "anillo_hierro", "amuleto_vital", "colmillo_lobo", "colmillo_lobo", "amuleto_vital", "amuleto_vital", "anillo_hierro", "amuleto_vital" };

    class Mission
    {
        public char style;
        public int index;
        public int level;
        public string relics = "";
        public int[] startHp = new int[4], startMax = new int[4], endHp = new int[4];
        public int potionsBought;
        public int dead, revived, rounds, potionsCombat, potionsMap, antidotes, heals, healedHp, coinsAfter, regenCasts, buffCasts;
        public bool victory, aborted;
        public Dictionary<string, int> dmg = new Dictionary<string, int>();
    }

    class Campaign
    {
        public char style;
        public List<Mission> missions = new List<Mission>();
        public bool beatBoss;
        public int failedAt = -1;
    }

    enum Phase { New, Map, Loading, Fighting, Post }

    static readonly Queue<char> queue = new Queue<char>();
    static readonly List<Campaign> done = new List<Campaign>();
    static readonly List<EnemyArchetypeData> archetypes = new List<EnemyArchetypeData>();
    static readonly List<float> delays = new List<float>();

    static Campaign camp;
    static Mission mission;
    static Phase phase;
    static int missionIndex;
    static int wait;
    static int seed = 5000;
    static bool active;
    static bool ended;
    static bool endVictory;
    static TurnManager tm;
    static PositionManager positions;
    static AbilitySystem abilities;
    static int lastKey;
    static int safety;
    static int potionsAtStart, antidotesAtStart;
    static int mapPotions;
    static int boughtTotal;
    static string outPath;
    static bool savedRun;
    static int savedVSync, savedTarget;
    static float startTime;

    public static string Status = "";
    // ---- Estadisticas de habilidades (leidas del registro de combate) ----
    static readonly string[] HeroNames = { "Arquera", "Evanora", "Noelle", "Caballero" };
    static readonly Dictionary<string, int> abilityUses = new Dictionary<string, int>();
    static readonly Dictionary<string, int> abilityDamage = new Dictionary<string, int>();
    static readonly Dictionary<string, int> abilityDamageHits = new Dictionary<string, int>();
    static int periodicBurn, periodicBleed, singleHealPv, singleHeals, errorLogs;

    static void Bump(Dictionary<string, int> d, string key, int amount)
    {
        int v;
        d.TryGetValue(key, out v);
        d[key] = v + amount;
    }

    static bool IsHeroName(string n)
    {
        return System.Array.IndexOf(HeroNames, n) >= 0;
    }

    static void OnLog(string m, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errorLogs++;
            return;
        }

        if (type != LogType.Log)
            return;

        string first = m.Split((char)10)[0];

        if (mission != null && phase == Phase.Fighting)
        {
            int cz = first.IndexOf(" causa ");

            if (cz > 0)
            {
                string abName = first.Substring(0, cz);
                string rest = first.Substring(cz + 7);
                int spc = rest.IndexOf((char)32);
                int amount;
                int lastA = rest.LastIndexOf(" a ");
                string victim = lastA >= 0 ? rest.Substring(lastA + 3).Split((char)46)[0] : "";

                if (spc > 0 && int.TryParse(rest.Substring(0, spc), out amount) && IsHeroName(victim) && !abilityUses.ContainsKey(abName))
                    Bump(mission.dmg, abName, amount);
            }
            else if (first.Contains(" sufre ") && first.Contains(" de da"))
            {
                string unitName = first.Substring(0, first.IndexOf(" sufre "));
                string[] tk = first.Substring(first.IndexOf(" sufre ") + 7).Split((char)32);
                int amount;

                if (IsHeroName(unitName) && int.TryParse(tk[0], out amount))
                    Bump(mission.dmg, first.Contains("Burn") ? "[Quemadura]" : first.Contains("Bleeding") ? "[Sangrado]" : "[Veneno]", amount);
            }
        }

        // Usos: "{heroe} utiliza la habilidad: X sobre Y" | "{heroe} utiliza X sobre ..." | "{heroe} lanza X sobre N enemigos."
        foreach (string marker in new[] { " utiliza la habilidad: ", " utiliza ", " lanza " })
        {
            int i = first.IndexOf(marker);

            if (i <= 0)
                continue;

            string user = first.Substring(0, i);

            if (!IsHeroName(user))
                break;

            string rest = first.Substring(i + marker.Length);
            int j = rest.IndexOf(" sobre ");
            string ability = j > 0 ? rest.Substring(0, j) : rest;
            Bump(abilityUses, ability, 1);
            return;
        }

        // Daño directo: "X causa N de daño a Y."  (solo habilidades de heroes y objetivos no heroes)
        int c = first.IndexOf(" causa ");

        if (c > 0)
        {
            string ability = first.Substring(0, c);
            string after = first.Substring(c + 7);
            int sp = after.IndexOf(' ');
            int dmg;
            int toIndex = after.IndexOf(" de da");
            string target = after.IndexOf(" a ") >= 0 ? after.Substring(after.LastIndexOf(" a ") + 3).TrimEnd('.', ' ') : "";

            if (target.EndsWith("CRITICO!"))
                target = target.Substring(0, target.IndexOf('.'));

            if (sp > 0 && int.TryParse(after.Substring(0, sp), out dmg) && abilityUses.ContainsKey(ability) && !IsHeroName(target))
            {
                Bump(abilityDamage, ability, dmg);
                Bump(abilityDamageHits, ability, 1);
            }

            return;
        }

        // Daño periodico sobre no heroes.
        int p = first.IndexOf(" de daño por ");

        if (p > 0 && first.Contains(" sufre "))
        {
            string unit = first.Substring(0, first.IndexOf(" sufre "));

            if (!IsHeroName(unit))
            {
                int amount;
                string[] tokens = first.Substring(first.IndexOf(" sufre ") + 7).Split(' ');

                if (int.TryParse(tokens[0], out amount))
                {
                    if (first.Contains("Burn")) periodicBurn += amount;
                    else if (first.Contains("Bleeding")) periodicBleed += amount;
                }
            }

            return;
        }

        // Curacion unica de Noelle: "Noelle cura a X por N puntos de vida."
        int h = first.IndexOf(" cura a ");

        if (h > 0 && IsHeroName(first.Substring(0, h)))
        {
            int n;
            int por = first.LastIndexOf(" por ");

            if (por > 0 && int.TryParse(first.Substring(por + 5).Split(' ')[0], out n))
            {
                singleHealPv += n;
                singleHeals++;
            }
        }
    }

    public static bool Done => !active;

    public static void Begin(string styles, int perStyle, string path)
    {
        queue.Clear();
        done.Clear();
        outPath = path;

        for (int i = 0; i < perStyle; i++)
            foreach (char s in styles)
                queue.Enqueue(s);

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
        startTime = Time.realtimeSinceStartup;

        abilityUses.Clear();
        abilityDamage.Clear();
        abilityDamageHits.Clear();
        periodicBurn = 0;
        periodicBleed = 0;
        singleHealPv = 0;
        singleHeals = 0;
        errorLogs = 0;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;

        camp = null;
        phase = Phase.New;
        active = true;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        Status = "iniciado";
    }

    static void Finish(string status)
    {
        EditorApplication.update -= Update;
        Application.logMessageReceived -= OnLog;

        for (int i = 0; i < archetypes.Count; i++)
            archetypes[i].actionDelay = delays[i];

        Application.runInBackground = savedRun;
        QualitySettings.vSyncCount = savedVSync;
        Application.targetFrameRate = savedTarget;
        Time.timeScale = 1f;
        active = false;
        Status = status;
        System.IO.File.WriteAllText(outPath + ".corto", status + "\n" + ShortReport() + "\nfin\n");
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
            Step();
        }
        catch (System.Exception e)
        {
            Finish("ERROR: " + e);
        }
    }

    // ------------------------------------------------------------
    // Maquina de estados
    // ------------------------------------------------------------

    static void Step()
    {
        if (wait-- > 0)
            return;

        switch (phase)
        {
            case Phase.New:
                if (queue.Count == 0)
                {
                    Finish("terminado en " + (Time.realtimeSinceStartup - startTime).ToString("0") + " s");
                    return;
                }

                camp = new Campaign { style = queue.Dequeue() };
                boughtTotal = 0;
                GameSession.StartNewGame();
                RelicInventory.Clear();           // sin el conjunto de reliquias de prueba
                missionIndex = 0;
                phase = Phase.Map;
                return;

            case Phase.Map:
                MapPhase();
                return;

            case Phase.Loading:
                LoadingStep();
                return;

            case Phase.Fighting:
                FightStep();
                return;

            case Phase.Post:
                PostStep();
                return;
        }
    }

    static void MapPhase()
    {
        GameSession.SetInCombat(false);

        if (missionIndex > 0 && MissionIds[missionIndex - 1] == "B1")
            GameSession.TryLiberateNoelle();   // evento "La mascara rota"

        mapPotions = 0;
        int boughtBefore = boughtTotal;
        Shop(camp.style, missionIndex);
        UsePotionsAtMap();

        mission = new Mission { style = camp.style, index = missionIndex, potionsMap = mapPotions, potionsBought = boughtTotal - boughtBefore };
        mission.relics = RelicText();

        Random.InitState(7000 + done.Count * 100 + missionIndex);
        EncounterLoader.DevEncounter = AssetDatabase.LoadAssetAtPath<EncounterData>("Assets/ScriptableObjects/Encounters/" + MissionPaths[missionIndex] + ".asset");
        Time.timeScale = 1f;
        tm = null;
        ended = false;
        phase = Phase.Loading;
        wait = 6;
        SceneManager.LoadScene("CombateIntegracion");
    }

    static void LoadingStep()
    {
        CombatIntroTransition intro = Object.FindAnyObjectByType<CombatIntroTransition>();

        if (intro != null)
            Object.Destroy(intro);

        TurnManager found = Object.FindAnyObjectByType<TurnManager>();
        positions = Object.FindAnyObjectByType<PositionManager>();
        abilities = Object.FindAnyObjectByType<AbilitySystem>();

        if (found == null || positions == null || abilities == null || Hero(0) == null || Hero(3) == null)
        {
            wait = 1;
            return;
        }

        tm = found;
        tm.OnBattleEnding += OnEnding;

        for (int i = 0; i < 4; i++)
        {
            BattleUnit h = Hero(i);
            mission.startHp[i] = h.CurrentHealth;
            mission.startMax[i] = h.MaxHealth;
            CharacterProgression p = h.GetComponent<CharacterProgression>();

            if (p != null)
                mission.level = Mathf.Max(mission.level, p.Level);
        }

        potionsAtStart = GameSession.Potions;
        antidotesAtStart = GameSession.Antidotes;
        lastKey = -1;
        safety = 0;
        phase = Phase.Fighting;
    }

    static void OnEnding(bool victory)
    {
        if (ended)
            return;

        ended = true;
        endVictory = victory;
        mission.victory = victory;
        mission.rounds = tm.CurrentRound;

        for (int i = 0; i < 4; i++)
        {
            BattleUnit h = Hero(i);
            mission.endHp[i] = h.CurrentHealth;

            if (!h.IsAlive)
                mission.dead++;
        }
    }

    static void FightStep()
    {
        if (ended || tm.BattleFinished)
        {
            if (!ended)
                OnEnding(false);

            tm.OnBattleEnding -= OnEnding;
            phase = Phase.Post;
            wait = 2;   // deja pasar la XP, la revivida y el guardado de la sesion
            return;
        }

        BattleUnit unit = tm.CurrentUnit;

        if (unit == null || unit.Side != TeamSide.Player || !unit.IsAlive)
            return;

        int key = System.Array.IndexOf(HeroIds, unit.gameObject.name) * 1000 + tm.CurrentRound;

        if (key == lastKey)
            return;

        lastKey = key;

        if (++safety > 400 || tm.CurrentRound > 60)
        {
            mission.aborted = true;
            mission.rounds = tm.CurrentRound;
            ended = true;
            mission.victory = false;
            return;
        }

        PlayHeroTurn(unit);

        if (!tm.BattleFinished)
            tm.EndTurn();
    }

    static void PostStep()
    {
        int revived = 0;

        for (int i = 0; i < 4; i++)
        {
            BattleUnit h = Hero(i);

            if (h != null && h.IsAlive && mission.endHp[i] <= 0)
                revived++;
        }

        mission.revived = revived;
        mission.potionsCombat = Mathf.Max(0, potionsAtStart - GameSession.Potions);
        mission.antidotes = Mathf.Max(0, antidotesAtStart - GameSession.Antidotes);
        mission.coinsAfter = GameSession.Coins;
        camp.missions.Add(mission);

        if (!mission.victory)
        {
            camp.failedAt = missionIndex;
            FinishCampaign();
            return;
        }

        if (MissionIds[missionIndex] == "BOSS")
        {
            camp.beatBoss = true;
            FinishCampaign();
            return;
        }

        missionIndex++;
        phase = Phase.Map;
    }

    static void FinishCampaign()
    {
        done.Add(camp);
        Status = "campanas " + done.Count;
        System.IO.File.WriteAllText(outPath + ".parcial", "campanas completadas: " + done.Count + "\n" + Report());
        phase = Phase.New;
    }

    static BattleUnit Hero(int i)
    {
        foreach (BattleUnit unit in Object.FindObjectsByType<BattleUnit>())
            if (unit.Side == TeamSide.Player && unit.gameObject.name == HeroIds[i])
                return unit;

        return null;
    }

    // ------------------------------------------------------------
    // Compras y mapa
    // ------------------------------------------------------------

    static int RelicCount()
    {
        return RelicInventory.Items.Count;
    }

    static RelicData WishData(int i)
    {
        return RelicCatalog.Instance.Find(WishType[i]);
    }

    static bool BuyRelic()
    {
        int n = RelicCount();

        if (n >= 8)
            return false;

        RelicData data = WishData(n);

        if (data == null || RelicShop.Check(data) != RelicShop.Result.Success)
            return false;

        if (RelicShop.TryBuy(data) != RelicShop.Result.Success)
            return false;

        // La unidad nueva es la ultima del inventario; se equipa en la primera casilla libre de su heroe.
        RelicInventory.Item item = RelicInventory.Items[RelicInventory.Items.Count - 1];
        string heroId = WishHero[n];

        for (int slot = 0; slot < RelicInventory.SlotsPerHero; slot++)
        {
            if (RelicInventory.EquippedUid(heroId, slot) != 0)
                continue;

            string reason;
            RelicInventory.TryEquip(item.uid, heroId, slot, out reason);
            break;
        }

        return true;
    }

    static bool BuyConsumable(GameSession.Consumable type)
    {
        bool ok;
        ConsumableService.Buy(type, out ok);
        if (ok && type == GameSession.Consumable.Potion) boughtTotal++;
        return ok;
    }

    static int Count(GameSession.Consumable type)
    {
        return GameSession.GetCount(type);
    }

    static void Shop(char style, int index)
    {
        if (style == 'A')
            return;

        for (int guard = 0; guard < 40; guard++)
        {
            bool bought = false;

            if (style == 'B')
            {
                int potionsWanted = index >= 2 ? 2 : 1;
                int antidotesWanted = index >= 7 ? 2 : (index >= 4 ? 1 : 0);

                if (Count(GameSession.Consumable.Potion) < potionsWanted) bought = BuyConsumable(GameSession.Consumable.Potion);
                else if (Count(GameSession.Consumable.Antidote) < antidotesWanted) bought = BuyConsumable(GameSession.Consumable.Antidote);
                else if (RelicCount() < 8) bought = BuyRelic();
                else if (Count(GameSession.Consumable.Potion) < 5) bought = BuyConsumable(GameSession.Consumable.Potion);
            }
            else if (style == 'D')
            {
                int antidotesWanted = index >= 4 ? 1 : 0;

                if (Count(GameSession.Consumable.Potion) < 3) bought = BuyConsumable(GameSession.Consumable.Potion);
                else if (Count(GameSession.Consumable.Antidote) < antidotesWanted) bought = BuyConsumable(GameSession.Consumable.Antidote);
                else if (RelicCount() < 8) bought = BuyRelic();
            }
            else // 'C'
            {
                if (RelicCount() < 8)
                    bought = BuyRelic();
                else if (Count(GameSession.Consumable.Potion) < 3)
                    bought = BuyConsumable(GameSession.Consumable.Potion);
                else if (Count(GameSession.Consumable.Antidote) < 2)
                    bought = BuyConsumable(GameSession.Consumable.Antidote);
            }

            if (!bought)
                break;
        }
    }

    // El jugador usa pociones en el mapa con los heridos por debajo del 50 %.
    static void UsePotionsAtMap()
    {
        for (int guard = 0; guard < 12 && GameSession.Potions > 0; guard++)
        {
            string worst = null;
            float worstFraction = (Tactical && (missionIndex >= 7 || camp.style == 'D')) ? 0.7f : 0.5f;

            foreach (string id in HeroIds)
            {
                GameSession.PartyMemberState state;

                if (!GameSession.TryGetMember(id, out state) || state.health <= 0 || state.maxHealth <= 0)
                    continue;

                float f = (float)state.health / state.maxHealth;

                if (f < worstFraction)
                {
                    worstFraction = f;
                    worst = id;
                }
            }

            if (worst == null)
                break;

            string message;

            if (!ConsumableService.TryUseOnMember(GameSession.Consumable.Potion, worst, out message))
                break;

            mapPotions++;
        }
    }

    static string RelicText()
    {
        StringBuilder sb = new StringBuilder();

        for (int h = 0; h < 4; h++)
        {
            for (int slot = 0; slot < RelicInventory.SlotsPerHero; slot++)
            {
                int uid = RelicInventory.EquippedUid(HeroIds[h], slot);

                if (uid == 0)
                    continue;

                RelicData d = RelicInventory.DataOf(uid);
                sb.Append(HeroShort[h][0]).Append(':').Append(d != null ? d.typeId : "?").Append(' ');
            }
        }

        return sb.ToString().Trim();
    }

    // ------------------------------------------------------------
    // Politica de heroes en combate
    // ------------------------------------------------------------

    static BattleUnit LowestAlly(out float fraction)
    {
        BattleUnit worst = null;
        fraction = 2f;

        for (int i = 0; i < 4; i++)
        {
            BattleUnit h = Hero(i);

            if (h == null || !h.IsAlive)
                continue;

            float f = (float)h.CurrentHealth / h.MaxHealth;

            if (f < fraction)
            {
                fraction = f;
                worst = h;
            }
        }

        return worst;
    }

    // Politica de apoyo de Noelle (liberada o no): devuelve el nombre de lo que uso o null si no hizo nada.
    //   1. Curacion unica si algun aliado esta al 40 % o menos.  2. Regeneracion de grupo si hay 2+ heridos y faltan 8+ PV.
    //   3. Curacion unica si algun aliado esta al 60 % o menos.  4. Bendicion de defensa si hay 2+ enemigos.
    public static string NoelleSupport(BattleUnit hero, AbilitySystem abilitySystem, out int healedHp)
    {
        healedHp = 0;
        CharacterAbilityLoadout loadout = hero.GetComponent<CharacterAbilityLoadout>();
        AbilityCooldownController cooldowns = hero.GetComponent<AbilityCooldownController>();

        if (loadout == null)
            return null;

        AbilityData single = null, regen = null, buff = null;

        foreach (AbilityData a in loadout.Abilities)
        {
            if (a == null || a.requiresPreparation || !a.CanUseFromPosition(hero.Position) || (cooldowns != null && !cooldowns.IsReady(a)))
                continue;

            if (a.abilityType == AbilityType.Heal && a.targetType == AbilityTargetType.Single)
                single = a;
            else if (a.abilityType == AbilityType.Support && a.targetType == AbilityTargetType.AllAllies)
            {
                if (a.regenerationPerTurn > 0) regen = a;
                else buff = a;
            }
        }

        BattleUnit lowest = null;
        float lowestFraction = 2f;
        int injured = 0, missing = 0, enemies = 0;

        foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
        {
            if (!u.IsAlive)
                continue;

            if (u.Side == TeamSide.Enemy)
            {
                enemies++;
                continue;
            }

            float f = (float)u.CurrentHealth / u.MaxHealth;
            missing += u.MaxHealth - u.CurrentHealth;

            if (f < 0.85f)
                injured++;

            if (f < lowestFraction)
            {
                lowestFraction = f;
                lowest = u;
            }
        }

        if (single != null && lowest != null && lowestFraction <= 0.40f && single.CanTargetPosition(lowest.Position))
            return CastSingle(hero, lowest, single, abilitySystem, out healedHp);

        if (regen != null && injured >= 2 && missing >= 8 && abilitySystem.ExecuteAbility(hero, hero, regen))
            return "bendicion";

        if (single != null && lowest != null && lowestFraction <= 0.60f && single.CanTargetPosition(lowest.Position))
            return CastSingle(hero, lowest, single, abilitySystem, out healedHp);

        if (buff != null && enemies >= 2 && abilitySystem.ExecuteAbility(hero, hero, buff))
            return "amparo";

        return null;
    }

    static string CastSingle(BattleUnit hero, BattleUnit target, AbilityData ability, AbilitySystem abilitySystem, out int healedHp)
    {
        int before = target.CurrentHealth;
        healedHp = 0;

        if (!abilitySystem.ExecuteAbility(hero, target, ability))
            return null;

        healedHp = Mathf.Max(0, target.CurrentHealth - before);
        return "cura";
    }

    // ------------------------------------------------------------
    // Politica "inteligente": aprovecha las sinergias de las habilidades (Marca, Quemadura -> Ruptura, Meteorito,
    // Guardia, Embate, Sangrado...). Se activa con SmartPolicy = true; si no, se usa la politica simple de antes.
    // ------------------------------------------------------------
    public static bool SmartPolicy = false;

    static AbilityData ReadyAbility(BattleUnit hero, string assetName)
    {
        CharacterAbilityLoadout loadout = hero.GetComponent<CharacterAbilityLoadout>();
        AbilityCooldownController cooldowns = hero.GetComponent<AbilityCooldownController>();

        if (loadout == null)
            return null;

        foreach (AbilityData a in loadout.Abilities)
            if (a != null && a.name == assetName && a.CanUseFromPosition(hero.Position) && (cooldowns == null || cooldowns.IsReady(a)))
                return a;

        return null;
    }

    static List<BattleUnit> LiveEnemies(AbilityData ability)
    {
        List<BattleUnit> list = new List<BattleUnit>();

        for (int p = 1; p <= 4; p++)
        {
            BattleUnit u = positions.GetUnitAtPosition(TeamSide.Enemy, p);

            if (u != null && u.IsAlive && (ability == null || ability.CanTargetPosition(p)))
                list.Add(u);
        }

        return list;
    }

    static bool Has(BattleUnit u, StatusEffectType t)
    {
        StatusEffectController c = u.GetComponent<StatusEffectController>();
        return c != null && c.HasEffect(t);
    }

    static BattleUnit Best(List<BattleUnit> list, System.Func<BattleUnit, float> score)
    {
        BattleUnit best = null;
        float bestScore = float.MinValue;

        foreach (BattleUnit u in list)
        {
            float s = score(u);

            if (s > bestScore)
            {
                bestScore = s;
                best = u;
            }
        }

        return best;
    }

    // Devuelve true si uso una habilidad de sinergia.
    static bool SmartTurn(BattleUnit hero)
    {
        string id = hero.gameObject.name;

        if (id == "PlayerArquera")
        {
            AbilityData marca = ReadyAbility(hero, "Elaina_MarcaDelCazador");

            if (marca != null)
            {
                BattleUnit t = Best(LiveEnemies(marca).FindAll(u => !Has(u, StatusEffectType.Mark) && u.CurrentHealth >= 10), u => u.CurrentHealth);

                if (t != null && abilities.ExecuteAbility(hero, t, marca))
                    return true;
            }

            // Con un enemigo marcado: el mejor golpe disponible sobre el.
            foreach (string name in new[] { "Elaina_FlechaPerforante", "Elaina_DisparoCertero" })
            {
                AbilityData a = ReadyAbility(hero, name);

                if (a == null)
                    continue;

                BattleUnit marked = Best(LiveEnemies(a).FindAll(u => Has(u, StatusEffectType.Mark)), u => u.CurrentHealth);

                if (marked != null && abilities.ExecuteAbility(hero, marked, a))
                    return true;
            }

            AbilityData evasivo = ReadyAbility(hero, "Elaina_TiroEvasivo");

            if (evasivo != null && hero.CurrentHealth < hero.MaxHealth * 0.5f)
            {
                BattleUnit t = Best(LiveEnemies(evasivo), u => -u.CurrentHealth);

                if (t != null && abilities.ExecuteAbility(hero, t, evasivo))
                    return true;
            }
        }
        else if (id == "PlayerEvanora")
        {
            AbilityData meteoro = ReadyAbility(hero, "Evanora_GolpeDeMeteorito");
            PreparedAbilityController prep = hero.GetComponent<PreparedAbilityController>();

            if (meteoro != null && (prep == null || !prep.HasPreparation) && LiveEnemies(meteoro).Count >= 2 && abilities.ExecuteAreaAbility(hero, meteoro))
                return true;

            AbilityData ruptura = ReadyAbility(hero, "Evanora_SelloDeRuptura");

            if (ruptura != null)
            {
                BattleUnit t = Best(LiveEnemies(ruptura).FindAll(u => Has(u, StatusEffectType.Burn)), u => u.CurrentHealth);

                if (t != null && abilities.ExecuteAbility(hero, t, ruptura))
                    return true;
            }

            AbilityData llamarada = ReadyAbility(hero, "Evanora_Llamarada");

            if (llamarada != null)
            {
                BattleUnit t = Best(LiveEnemies(llamarada).FindAll(u => !Has(u, StatusEffectType.Burn) && u.CurrentHealth >= 8), u => u.CurrentHealth);

                if (t != null && abilities.ExecuteAbility(hero, t, llamarada))
                    return true;
            }
        }
        else if (id == "PlayerCaballero")
        {
            AbilityData guardia = ReadyAbility(hero, "Valerian_GuardiaDeTrinchera");

            if (guardia != null && LiveEnemies(null).Count >= 2)
            {
                BattleUnit weak = null;
                float f = 0.6f;

                for (int i = 0; i < 4; i++)
                {
                    BattleUnit ally = Hero(i);

                    if (ally == null || !ally.IsAlive || ally == hero)
                        continue;

                    float ratio = (float)ally.CurrentHealth / ally.MaxHealth;

                    if (ratio < f)
                    {
                        f = ratio;
                        weak = ally;
                    }
                }

                if (weak != null && abilities.ExecuteAbility(hero, weak, guardia))
                    return true;
            }

            AbilityData embate = ReadyAbility(hero, "Valerian_GolpeBajo");

            if (embate != null)
            {
                BattleUnit t = Best(LiveEnemies(embate).FindAll(u => !u.Data.isBoss && !Has(u, StatusEffectType.Stun)), u => u.AverageDamage);

                if (t != null && abilities.ExecuteAbility(hero, t, embate))
                    return true;
            }

            AbilityData herida = ReadyAbility(hero, "Valerian_HeridaProfunda");

            if (herida != null)
            {
                BattleUnit t = Best(LiveEnemies(herida).FindAll(u => !Has(u, StatusEffectType.Bleeding) && u.CurrentHealth >= 10), u => u.CurrentHealth);

                if (t != null && abilities.ExecuteAbility(hero, t, herida))
                    return true;
            }
        }
        else if (id == "PlayerNoelle")
        {
            AbilityData resistencia = ReadyAbility(hero, "NoelleMasked_ResistenciaObstinada");

            if (resistencia != null && LiveEnemies(null).Count >= 2 && abilities.ExecuteAbility(hero, hero, resistencia))
                return true;

            AbilityData quebrantador = ReadyAbility(hero, "NoelleMasked_ImpactoQuebrantador");

            if (quebrantador != null)
            {
                BattleUnit t = Best(LiveEnemies(quebrantador).FindAll(u => !u.Data.isBoss && !Has(u, StatusEffectType.Stun)), u => u.AverageDamage);

                if (t != null && abilities.ExecuteAbility(hero, t, quebrantador))
                    return true;
            }
        }

        return false;
    }

    // ------------------------------------------------------------
    // Politica "de valor": cada habilidad disponible se puntua con su dano esperado MAS el valor de sus efectos (Quemadura,
    // Sangrado, Aturdimiento, Debilitamiento, Marca, evasion propia, ataque de area, Guardia, Resistencia) y se usa la mejor.
    // Asi las habilidades especiales compiten con el ataque basico segun lo que realmente aportan.
    // ------------------------------------------------------------
    static float EffectsValue(AbilityData a, BattleUnit target, float direct)
    {
        float v = 0f;

        if (a.statusEffects == null)
            return v;

        foreach (AbilityStatusEffect s in a.statusEffects)
        {
            if (s == null)
                continue;

            float chance = s.chance / 100f;

            if (s.applyToUser)
            {
                if (s.type == StatusEffectType.EvasionBoost)
                    v += 1.2f;

                continue;
            }

            switch (s.type)
            {
                case StatusEffectType.Burn:
                case StatusEffectType.Bleeding:
                case StatusEffectType.Poison:
                    if (!Has(target, s.type))
                    {
                        float live = target.CurrentHealth - direct;
                        float survive = live > s.potency * s.duration + 2f ? 1f : (live > 2f ? 0.4f : 0f);
                        v += chance * s.potency * s.duration * survive * 0.85f;
                    }
                    break;

                case StatusEffectType.Stun:
                    if (!target.Data.isBoss && !Has(target, StatusEffectType.Stun))
                        v += chance * target.AverageDamage * 0.9f;
                    break;

                case StatusEffectType.Weaken:
                    if (!Has(target, StatusEffectType.Weaken))
                        v += chance * target.AverageDamage * (s.potency / 100f) * s.duration * 0.8f;
                    break;

                case StatusEffectType.Mark:
                    if (!Has(target, StatusEffectType.Mark) && target.CurrentHealth >= 10)
                        v += 5.5f * 0.85f;
                    break;
            }
        }

        return v;
    }

    static float DirectValue(BattleUnit hero, AbilityData a, BattleUnit target)
    {
        if (!a.dealsDamage)
            return 0f;

        float d = DamageCalculator.ExpectedDamage(hero, target, a.basePower, a.damagePercent, a.defensePenetration) * a.DamageMultiplierAgainst(target);
        d *= 1f - EvasionController.ChanceFor(target, a.attackWeight, a.evasionPenetration) / 100f;

        if (Has(target, StatusEffectType.Mark))
            d *= 2f;

        d = Mathf.Min(d, target.CurrentHealth);
        return d + (d >= target.CurrentHealth ? 4f : 0f);
    }

    static bool ValueTurn(BattleUnit hero)
    {
        CharacterAbilityLoadout loadout = hero.GetComponent<CharacterAbilityLoadout>();
        AbilityCooldownController cooldowns = hero.GetComponent<AbilityCooldownController>();

        if (loadout == null)
            return false;

        List<KeyValuePair<float, System.Func<bool>>> options = new List<KeyValuePair<float, System.Func<bool>>>();
        int enemyCount = LiveEnemies(null).Count;
        PreparedAbilityController prep = hero.GetComponent<PreparedAbilityController>();

        foreach (AbilityData a in loadout.Abilities)
        {
            if (a == null || !a.CanUseFromPosition(hero.Position) || (cooldowns != null && !cooldowns.IsReady(a)))
                continue;

            AbilityData ability = a;

            if (a.abilityType == AbilityType.Attack && a.targetType == AbilityTargetType.AllEnemies && a.requiresPreparation)
            {
                if (prep != null && prep.HasPreparation)
                    continue;

                float sum = 0f;

                foreach (BattleUnit e in LiveEnemies(a))
                    sum += DirectValue(hero, a, e) + EffectsValue(a, e, 0f) * 0.5f;

                // La preparacion consume este turno y puede perderse si el enemigo cae antes: pesa la mitad.
                options.Add(new KeyValuePair<float, System.Func<bool>>(sum * 0.5f * 0.85f, () => abilities.ExecuteAreaAbility(hero, ability)));
                continue;
            }

            if (a.abilityType == AbilityType.Attack && a.targetType == AbilityTargetType.Single)
            {
                foreach (BattleUnit target in LiveEnemies(a))
                {
                    BattleUnit t = target;
                    float direct = DirectValue(hero, a, t);
                    options.Add(new KeyValuePair<float, System.Func<bool>>(direct + EffectsValue(a, t, direct), () => abilities.ExecuteAbility(hero, t, ability)));
                }

                continue;
            }

            if (a.abilityType == AbilityType.Support && a.name.Contains("Guardia"))
            {
                BattleUnit weak = null;
                float f = 0.5f;

                for (int i = 0; i < 4; i++)
                {
                    BattleUnit ally = Hero(i);

                    if (ally == null || !ally.IsAlive || ally == hero)
                        continue;

                    float ratio = (float)ally.CurrentHealth / ally.MaxHealth;

                    if (ratio < f)
                    {
                        f = ratio;
                        weak = ally;
                    }
                }

                if (weak != null && enemyCount >= 2)
                {
                    BattleUnit w = weak;
                    options.Add(new KeyValuePair<float, System.Func<bool>>(4f, () => abilities.ExecuteAbility(hero, w, ability)));
                }

                continue;
            }

            if (a.abilityType == AbilityType.Support && a.targetType == AbilityTargetType.Self && a.name.Contains("Resistencia") && enemyCount >= 2)
                options.Add(new KeyValuePair<float, System.Func<bool>>(hero.CurrentHealth < hero.MaxHealth * 0.8f ? 3f : 1.5f, () => abilities.ExecuteAbility(hero, hero, ability)));
        }

        options.Sort((x, y) => y.Key.CompareTo(x.Key));

        foreach (KeyValuePair<float, System.Func<bool>> option in options)
            if (option.Key > 0f && option.Value())
                return true;

        return false;
    }

    // ------------------------------------------------------------
    // Politica TACTICA: usa solo informacion visible para un jugador (vida, estadisticas, estados, recargas, posiciones) y las
    // mismas reglas del juego. Prioriza amenazas y bajas, aprovecha Marca y Quemadura -> Ruptura, no desperdicia efectos
    // periodicos en enemigos que van a morir, usa Guardia con aliados en riesgo y cura/administra pociones solo si hace falta.
    // ------------------------------------------------------------
    public static bool Tactical = false;

    // Entrada para otras herramientas (CombatBalanceTest): juega el turno de un heroe con la politica tactica.
    public static void PlayExternal(BattleUnit hero, AbilitySystem abilitySystem, PositionManager positionManager)
    {
        abilities = abilitySystem;
        positions = positionManager;

        if (mission == null)
            mission = new Mission();

        bool previous = Tactical;
        Tactical = true;
        PlayHeroTurn(hero);
        Tactical = previous;
    }

    static float Threat(BattleUnit e)
    {
        return e.AverageDamage * (1f + Mathf.Max(0, e.Speed - 5) * 0.08f);
    }

    static float PartyDamagePerRound()
    {
        float sum = 0f;

        for (int i = 0; i < 4; i++)
        {
            BattleUnit h = Hero(i);

            if (h != null && h.IsAlive)
                sum += h.AverageDamage * 0.85f;
        }

        return sum;
    }

    static int EnemyHealthTotal()
    {
        int sum = 0;

        foreach (BattleUnit e in LiveEnemies(null))
            sum += e.CurrentHealth;

        return sum;
    }

    static float EnemyDamagePerRound()
    {
        float sum = 0f;

        foreach (BattleUnit e in LiveEnemies(null))
            sum += e.AverageDamage;

        return sum;
    }

    static bool FightEndingSoon()
    {
        return EnemyHealthTotal() <= PartyDamagePerRound() * 1.1f;
    }

    // Efectos de una habilidad sobre un objetivo, ajustando el valor de los efectos periodicos al tiempo que se espera que
    // el objetivo siga vivo (si se le va a derrotar enseguida, no se desperdicia).
    static float TacticalEffects(AbilityData a, BattleUnit target, float direct, BattleUnit hero)
    {
        float v = 0f;

        if (a.statusEffects == null)
            return v;

        float roundsToKill = Mathf.Max(0f, (target.CurrentHealth - direct) / Mathf.Max(1f, PartyDamagePerRound() * 0.6f));

        foreach (AbilityStatusEffect s in a.statusEffects)
        {
            if (s == null)
                continue;

            float chance = s.chance / 100f;

            if (s.applyToUser)
            {
                if (s.type == StatusEffectType.EvasionBoost)
                    v += hero.CurrentHealth < hero.MaxHealth * 0.6f ? 1.6f : 0.8f;

                continue;
            }

            bool lethal = direct >= target.CurrentHealth;

            switch (s.type)
            {
                case StatusEffectType.Burn:
                case StatusEffectType.Bleeding:
                case StatusEffectType.Poison:
                    if (!lethal && !Has(target, s.type))
                    {
                        float ticks = Mathf.Min(s.duration, Mathf.Max(0f, roundsToKill - 0.3f));
                        v += chance * s.potency * ticks;
                    }
                    break;

                case StatusEffectType.Stun:
                    if (!lethal && !target.Data.isBoss && !Has(target, StatusEffectType.Stun))
                        v += chance * Threat(target) * 0.9f;
                    break;

                case StatusEffectType.Weaken:
                    if (!lethal && !Has(target, StatusEffectType.Weaken))
                        v += chance * Threat(target) * (s.potency / 100f) * s.duration * 0.8f;
                    break;

                case StatusEffectType.Mark:
                    // La marca solo tiene sentido si alguien mas va a golpear al objetivo y este es una amenaza importante.
                    if (!Has(target, StatusEffectType.Mark) && target.CurrentHealth >= 12 && LiveEnemies(null).Count >= 1)
                        v += Mathf.Min(5f, target.CurrentHealth * 0.3f) * 0.9f + Threat(target) * 0.15f;
                    break;
            }
        }

        return v;
    }

    static float TacticalDirect(BattleUnit hero, AbilityData a, BattleUnit target, out float killBonus)
    {
        killBonus = 0f;

        if (!a.dealsDamage)
            return 0f;

        float d = DamageCalculator.ExpectedDamage(hero, target, a.basePower, a.damagePercent, a.defensePenetration) * a.DamageMultiplierAgainst(target);
        d *= 1f - EvasionController.ChanceFor(target, a.attackWeight, a.evasionPenetration) / 100f;

        if (Has(target, StatusEffectType.Mark))
            d *= 2f;

        if (d >= target.CurrentHealth)
        {
            d = target.CurrentHealth;
            killBonus = 3f + Threat(target);
        }

        return d + Threat(target) * 0.3f;
    }

    static bool NoelleTactical(BattleUnit hero)
    {
        CharacterAbilityLoadout loadout = hero.GetComponent<CharacterAbilityLoadout>();
        AbilityCooldownController cooldowns = hero.GetComponent<AbilityCooldownController>();

        if (loadout == null)
            return false;

        AbilityData single = null, regen = null, buff = null;

        foreach (AbilityData a in loadout.Abilities)
        {
            if (a == null || a.requiresPreparation || !a.CanUseFromPosition(hero.Position) || (cooldowns != null && !cooldowns.IsReady(a)))
                continue;

            if (a.abilityType == AbilityType.Heal && a.targetType == AbilityTargetType.Single)
                single = a;
            else if (a.abilityType == AbilityType.Support && a.targetType == AbilityTargetType.AllAllies)
            {
                if (a.regenerationPerTurn > 0) regen = a;
                else buff = a;
            }
        }

        BattleUnit lowest = null;
        float lowestRatio = 2f;
        int injured = 0, missing = 0, alive = 0;

        for (int i = 0; i < 4; i++)
        {
            BattleUnit u = Hero(i);

            if (u == null || !u.IsAlive)
                continue;

            alive++;
            float r = (float)u.CurrentHealth / u.MaxHealth;
            missing += u.MaxHealth - u.CurrentHealth;

            if (r < 0.8f)
                injured++;

            if (r < lowestRatio)
            {
                lowestRatio = r;
                lowest = u;
            }
        }

        float incomingPerAlly = EnemyDamagePerRound() * 0.9f / Mathf.Max(1, alive) * 1.5f;
        bool ending = FightEndingSoon();
        int enemies = LiveEnemies(null).Count;

        // Curacion unica: solo con necesidad real (a punto de caer, o bajo y con combate para rato).
        if (single != null && lowest != null && single.CanTargetPosition(lowest.Position) && lowest.MaxHealth - lowest.CurrentHealth >= single.AverageHeal(lowest) * 0.7f)
        {
            bool danger = lowest.CurrentHealth <= Mathf.Max(lowest.MaxHealth * 0.35f, incomingPerAlly);
            bool low = lowestRatio <= 0.55f && !ending;

            if (danger || low)
            {
                int before = lowest.CurrentHealth;

                if (abilities.ExecuteAbility(hero, lowest, single))
                {
                    mission.heals++;
                    mission.healedHp += Mathf.Max(0, lowest.CurrentHealth - before);
                    return true;
                }
            }
        }

        if (regen != null && !ending && injured >= 2 && missing >= 10 && abilities.ExecuteAbility(hero, hero, regen))
        {
            mission.regenCasts++;
            return true;
        }

        if (buff != null && !ending && enemies >= 2 && (EnemyDamagePerRound() >= 14f || injured >= 2) && abilities.ExecuteAbility(hero, hero, buff))
        {
            mission.buffCasts++;
            return true;
        }

        return false;
    }

    static bool PotionTactical(BattleUnit hero)
    {
        if (GameSession.Potions <= 0)
            return false;

        float incoming = EnemyDamagePerRound() * 0.9f / 3f * 1.5f;

        for (int i = 0; i < 4; i++)
        {
            BattleUnit ally = Hero(i);

            if (ally == null || !ally.IsAlive)
                continue;

            float r = (float)ally.CurrentHealth / ally.MaxHealth;

            if (r <= 0.30f && ally.CurrentHealth <= incoming * 1.2f + 2f)
            {
                string msg;

                if (ConsumableService.TryUse(GameSession.Consumable.Potion, ally, out msg))
                    return true;
            }
        }

        return false;
    }

    static bool TacticalTurn(BattleUnit hero)
    {
        CharacterAbilityLoadout loadout = hero.GetComponent<CharacterAbilityLoadout>();
        AbilityCooldownController cooldowns = hero.GetComponent<AbilityCooldownController>();

        if (loadout == null)
            return false;

        List<KeyValuePair<float, System.Func<bool>>> options = new List<KeyValuePair<float, System.Func<bool>>>();
        List<BattleUnit> all = LiveEnemies(null);
        PreparedAbilityController prep = hero.GetComponent<PreparedAbilityController>();
        bool ending = FightEndingSoon();

        foreach (AbilityData a in loadout.Abilities)
        {
            if (a == null || !a.CanUseFromPosition(hero.Position) || (cooldowns != null && !cooldowns.IsReady(a)))
                continue;

            AbilityData ability = a;

            if (a.abilityType == AbilityType.Attack && a.targetType == AbilityTargetType.AllEnemies && a.requiresPreparation)
            {
                if ((prep != null && prep.HasPreparation) || ending || all.Count < 2)
                    continue;

                float sum = 0f;

                foreach (BattleUnit e in LiveEnemies(a))
                {
                    float kill;
                    sum += TacticalDirect(hero, a, e, out kill) + kill * 0.5f + TacticalEffects(a, e, 0f, hero) * 0.4f;
                }

                // La preparacion consume este turno y puede perder objetivos antes de impactar.
                options.Add(new KeyValuePair<float, System.Func<bool>>(sum * 0.5f * 0.85f, () => abilities.ExecuteAreaAbility(hero, ability)));
                continue;
            }

            if (a.abilityType == AbilityType.Attack && a.targetType == AbilityTargetType.Single)
            {
                foreach (BattleUnit target in LiveEnemies(a))
                {
                    BattleUnit t = target;
                    float kill;
                    float direct = TacticalDirect(hero, a, t, out kill);
                    options.Add(new KeyValuePair<float, System.Func<bool>>(direct + kill + TacticalEffects(a, t, direct - Threat(t) * 0.3f, hero), () => abilities.ExecuteAbility(hero, t, ability)));
                }

                continue;
            }

            if (a.abilityType == AbilityType.Support && a.name.Contains("Guardia"))
            {
                BattleUnit risky = null;
                float riskBest = 0f;
                float enemyDmg = EnemyDamagePerRound();

                for (int i = 0; i < 4; i++)
                {
                    BattleUnit ally = Hero(i);

                    if (ally == null || !ally.IsAlive || ally == hero)
                        continue;

                    float ratio = (float)ally.CurrentHealth / ally.MaxHealth;
                    float risk = enemyDmg * 0.35f / Mathf.Max(1f, ally.CurrentHealth);

                    if ((ratio <= 0.6f || risk >= 0.45f) && risk > riskBest)
                    {
                        riskBest = risk;
                        risky = ally;
                    }
                }

                if (risky != null && all.Count >= 2 && !ending)
                {
                    BattleUnit w = risky;
                    options.Add(new KeyValuePair<float, System.Func<bool>>(Mathf.Min(6.5f, 3f + 4f * riskBest), () => abilities.ExecuteAbility(hero, w, ability)));
                }

                continue;
            }

            if (a.abilityType == AbilityType.Support && a.targetType == AbilityTargetType.Self && a.name.Contains("Resistencia") && all.Count >= 2 && !ending)
                options.Add(new KeyValuePair<float, System.Func<bool>>(hero.CurrentHealth < hero.MaxHealth * 0.7f ? 3f : 1.8f, () => abilities.ExecuteAbility(hero, hero, ability)));
        }

        options.Sort((x, y) => y.Key.CompareTo(x.Key));

        foreach (KeyValuePair<float, System.Func<bool>> option in options)
            if (option.Key > 0f && option.Value())
                return true;

        return false;
    }

    static void PlayHeroTurn(BattleUnit hero)
    {
        // 0. Politica tactica: antidoto/pocion solo si hace falta, curas de Noelle con necesidad real, y habilidades por valor.
        if (Tactical)
        {
            if (GameSession.Antidotes > 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    BattleUnit ally = Hero(i);

                    if (ally != null && ally.IsAlive && ConsumableService.HasCurableStatus(ally) && ally.CurrentHealth <= ally.MaxHealth * 0.7f)
                    {
                        string msg;

                        if (ConsumableService.TryUse(GameSession.Consumable.Antidote, ally, out msg))
                            return;
                    }
                }
            }

            if (hero.gameObject.name == "PlayerNoelle" && NoelleTactical(hero))
                return;

            if (PotionTactical(hero))
                return;

            if (TacticalTurn(hero))
                return;
        }

        // 1. Antidoto: si un aliado sufre un estado curable y quedan antidotos.
        if (GameSession.Antidotes > 0)
        {
            for (int i = 0; i < 4; i++)
            {
                BattleUnit ally = Hero(i);

                if (ally != null && ally.IsAlive && ConsumableService.HasCurableStatus(ally))
                {
                    string msg;

                    if (ConsumableService.TryUse(GameSession.Consumable.Antidote, ally, out msg))
                        return;
                }
            }
        }

        if (hero.gameObject.name == "PlayerNoelle")
        {
            int healed;
            string kind = NoelleSupport(hero, abilities, out healed);

            if (kind == "cura") { mission.heals++; mission.healedHp += healed; return; }
            if (kind == "bendicion") { mission.regenCasts++; return; }
            if (kind == "amparo") { mission.buffCasts++; return; }
        }

        // Politica inteligente (sinergias): despues de las curaciones de Noelle y antes del ataque generico.
        if (SmartPolicy && ValueTurn(hero))
            return;

        CharacterAbilityLoadout loadout = hero.GetComponent<CharacterAbilityLoadout>();
        AbilityCooldownController cooldowns = hero.GetComponent<AbilityCooldownController>();

        AbilityData bestAbility = null;
        BattleUnit bestTarget = null;
        float bestScore = float.MinValue;

        if (loadout != null)
        {
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
        }

        // 2. Curacion de habilidad (Noelle): tiene prioridad.
        if (bestAbility != null && bestAbility.abilityType == AbilityType.Heal)
        {
            int before = bestTarget.CurrentHealth;
            abilities.ExecuteAbility(hero, bestTarget, bestAbility);
            mission.heals++;
            mission.healedHp += Mathf.Max(0, bestTarget.CurrentHealth - before);
            return;
        }

        // 3. Pocion: si un aliado esta por debajo del 30 % y no hay curacion de habilidad disponible.
        if (GameSession.Potions > 0)
        {
            float fraction;
            BattleUnit low = LowestAlly(out fraction);

            if (low != null && fraction <= 0.30f)
            {
                string msg;

                if (ConsumableService.TryUse(GameSession.Consumable.Potion, low, out msg))
                    return;
            }
        }

        // 4. Ataque.
        if (bestAbility != null)
            abilities.ExecuteAbility(hero, bestTarget, bestAbility);
    }

    // ------------------------------------------------------------
    // Informe
    // ------------------------------------------------------------

    static string Pct(int hp, int max)
    {
        return max > 0 ? Mathf.RoundToInt(100f * hp / max) + "%" : "-";
    }

    public static string ShortReport()
    {
        StringBuilder sb = new StringBuilder();
        int n = 0;

        foreach (Campaign c in done)
        {
            n++;
            int bought = 0, used = 0, won = 0;

            foreach (Mission m in c.missions)
            {
                bought += m.potionsBought;
                used += m.potionsMap + m.potionsCombat;

                if (m.victory)
                    won++;
            }

            string last = c.missions.Count > 0 ? c.missions[c.missions.Count - 1].relics : "";
            sb.AppendLine("CAMPANA " + n + ": misiones superadas " + won + "/" + MissionIds.Length + " | " + (c.beatBoss ? "derrota al Capitan" : "derrota en " + (c.failedAt >= 0 ? MissionIds[c.failedAt] : "?")));

            foreach (Mission m in c.missions)
            {
                string id = MissionIds[m.index];

                if (id != "C1" && id != "C2" && id != "BOSS")
                    continue;

                int hp = 0, max = 0;

                for (int i = 0; i < 4; i++)
                {
                    hp += m.startHp[i];
                    max += m.startMax[i];
                }

                sb.AppendLine("   entrada a " + id + ": vida " + hp + "/" + max + " (" + (max > 0 ? hp * 100 / max : 0) + " %), Coronas tras la mision " + m.coinsAfter + ", pociones compradas en esa visita " + m.potionsBought);
            }

            sb.AppendLine("   pociones compradas " + bought + ", usadas " + used + " | reliquias equipadas: " + (last == "" ? "ninguna" : last));
        }

        return sb.ToString();
    }

    public static string Report()
    {
        StringBuilder sb = new StringBuilder();

        foreach (char style in "ABCD")
        {
            List<Campaign> list = done.FindAll(c => c.style == style);

            if (list.Count == 0)
                continue;

            int reachedBoss = 0, beat = 0;
            int[] failures = new int[MissionIds.Length];

            foreach (Campaign c in list)
            {
                if (c.missions.Count == MissionIds.Length)
                    reachedBoss++;

                if (c.beatBoss)
                    beat++;

                if (c.failedAt >= 0)
                    failures[c.failedAt]++;
            }

            string name = style == 'A' ? "A: sin compras" : style == 'B' ? "B: equilibrada" : style == 'D' ? "D: pociones primero" : "C: centrada en reliquias";
            sb.AppendLine("== ESTILO " + name + " | campanas: " + list.Count + " | llegan al Capitan: " + reachedBoss + " | lo derrotan: " + beat);

            string fails = "";

            for (int i = 0; i < failures.Length; i++)
                if (failures[i] > 0)
                    fails += MissionIds[i] + "=" + failures[i] + " ";

            sb.AppendLine("   derrotas por mision: " + (fails == "" ? "ninguna" : fails));

            for (int m = 0; m < MissionIds.Length; m++)
            {
                List<Mission> ms = new List<Mission>();

                foreach (Campaign c in list)
                    if (c.missions.Count > m)
                        ms.Add(c.missions[m]);

                if (ms.Count == 0)
                    continue;

                int wins = 0, deadSum = 0, revSum = 0, roundSum = 0, potC = 0, potM = 0, ant = 0, heals = 0, healed = 0, coins = 0, regen = 0, buff = 0;
                float level = 0f, startPct = 0f, endPct = 0f, minEnd = 2f;

                foreach (Mission x in ms)
                {
                    if (x.victory) wins++;
                    deadSum += x.dead;
                    revSum += x.revived;
                    roundSum += x.rounds;
                    potC += x.potionsCombat;
                    potM += x.potionsMap;
                    ant += x.antidotes;
                    heals += x.heals;
                    regen += x.regenCasts;
                    buff += x.buffCasts;
                    healed += x.healedHp;
                    coins += x.coinsAfter;
                    level += x.level;

                    int sh = 0, sm = 0, eh = 0;

                    for (int i = 0; i < 4; i++)
                    {
                        sh += x.startHp[i];
                        sm += x.startMax[i];
                        eh += x.endHp[i];
                    }

                    startPct += sm > 0 ? (float)sh / sm : 0f;
                    float e = sm > 0 ? (float)eh / sm : 0f;
                    endPct += e;
                    minEnd = Mathf.Min(minEnd, e);
                }

                int n = ms.Count;
                sb.AppendLine(string.Format(
                    "   {0,-4} n={1,-2} Nv {2:0.0} | vida grupo inicio {3}% -> fin {4}% (peor {5}%) | resultado {6}/{1} | caidos {7:0.00} revividos {8:0.00} | rondas {9:0.0} | pociones mapa {10:0.00} combate {11:0.00} | antidotos {12:0.00} | curas Noelle {13:0.0} ({14:0.0} PV) + Bendicion {16:0.0} + Amparo {17:0.0} | Coronas {15:0}",
                    MissionIds[m], n, level / n, Mathf.RoundToInt(startPct / n * 100f), Mathf.RoundToInt(endPct / n * 100f),
                    Mathf.RoundToInt(minEnd * 100f), wins, (float)deadSum / n, (float)revSum / n, (float)roundSum / n,
                    (float)potM / n, (float)potC / n, (float)ant / n, (float)heals / n, (float)healed / n, (float)coins / n, (float)regen / n, (float)buff / n));

                sb.AppendLine("        reliquias (1.a campana): " + ms[0].relics);

                Dictionary<string, int> srcs = new Dictionary<string, int>();
                int srcTotal = 0;

                foreach (Mission x in ms)
                    foreach (KeyValuePair<string, int> kv in x.dmg)
                    {
                        Bump(srcs, kv.Key, kv.Value);
                        srcTotal += kv.Value;
                    }

                List<string> srcNames = new List<string>(srcs.Keys);
                srcNames.Sort((p, q) => srcs[q].CompareTo(srcs[p]));
                string srcText = "";

                for (int k = 0; k < srcNames.Count && k < 4; k++)
                    srcText += srcNames[k] + " " + ((float)srcs[srcNames[k]] / ms.Count).ToString("0.0") + " PV (" + Mathf.RoundToInt(100f * srcs[srcNames[k]] / Mathf.Max(1, srcTotal)) + "%)  ";

                sb.AppendLine("        dano recibido por combate: " + ((float)srcTotal / ms.Count).ToString("0.0") + " PV | principales fuentes: " + srcText);
            }

            sb.AppendLine();
        }

        sb.AppendLine("== HABILIDADES DE LOS HEROES (todas las campanas)");
        List<string> names = new List<string>(abilityUses.Keys);
        names.Sort((a, b) => abilityUses[b].CompareTo(abilityUses[a]));

        foreach (string name in names)
        {
            int uses = abilityUses[name];
            int dmg = abilityDamage.ContainsKey(name) ? abilityDamage[name] : 0;
            int hits = abilityDamageHits.ContainsKey(name) ? abilityDamageHits[name] : 0;
            sb.AppendLine("   " + name.PadRight(26) + " usos " + uses.ToString().PadLeft(5) +
                (hits > 0 ? " | daño directo total " + dmg.ToString().PadLeft(6) + " | medio por golpe " + ((float)dmg / hits).ToString("0.0") + " (" + hits + " golpes)" : " | sin daño directo"));
        }

        sb.AppendLine("   Quemadura (daño periodico a enemigos): " + periodicBurn + " PV | Sangrado: " + periodicBleed + " PV");
        sb.AppendLine("   Curas unicas de Noelle: " + singleHeals + " (" + singleHealPv + " PV)");
        sb.AppendLine("   Errores de consola durante la prueba: " + errorLogs);

        return sb.ToString();
    }
}
#endif
