#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Herramienta de DESARROLLO (solo editor) para medir el balance del Capitan
// (Pasos 72-73). No forma parte del juego ni se incluye en las builds.
//
//  - Reconstruye el estado de llegada al jefe jugando una partida nueva por
//    los caminos reales del juego (victorias reales: XP, Coronas, recuperacion,
//    foto del equipo, evento de Noelle). Los combates previos se ganan matando
//    a los enemigos al instante. El estado se guarda en Library/ y cada prueba
//    parte de una copia nueva (sesion nueva + foto + inventario fijo).
//  - Variantes temporales del jefe (HP, ATK, poder de Ultima Orden) en memoria.
//  - Politicas de los heroes por PERFIL, con clics simulados sobre el HUD:
//
//    PERFIL A (competente): ataca con la mejor habilidad (Marca primero),
//      Guardia de Valerian sobre el aliado amenazado/marcado, salida del area
//      de Fuego de Supresion cuando un fragil esta dentro, curaciones de Noelle
//      a <=50 %, pocion a <=35 %, apoyo de grupo, Meteorito.
//    PERFIL B (promedio): 70 % mejor ataque / 30 % otro ataque disponible al
//      azar; cura a <=35 % (pocion a <=25 %); Guardia ocasional (35 %) sobre el
//      mas herido; solo reacciona al Fuego si un aliado amenazado esta a
//      <=45 % (Guardia o mover); apoyo de grupo 50 % cuando hay 2+ heridos.
//    PERFIL C (aprendiendo): 50 % mejor ataque / 50 % ataque al azar (puede
//      elegir los menos convenientes, incluso Marca repetida); 25 % de ignorar
//      una habilidad con recarga; cura tarde (<=20 %, pocion <=15 %); Guardia
//      15 % sobre un aliado aleatorio; solo atiende el aviso de Fuego el 50 %
//      de las veces (y entonces reacciona como B con umbral 60 %); apoyo 25 %.
//    Ningun perfil pasa turno a proposito: si una accion falla, prueba otro
//    ataque y, solo si no hay ninguno posible, termina el turno.
//
//  Reproducible: cada combate usa System.Random(seed) para las decisiones del
//  jugador y Random.InitState(seed) para dados del juego.
public static class BossBalanceHarness
{
    // ------------------------------------------------------------
    // Variantes y resultados
    // ------------------------------------------------------------

    public class Variant
    {
        public string name;
        public int hp = 450;
        public int atk = 28;
        public int ult = -4;
        public int potions = 3;
        public int antidotes = 2;
        public char profile = 'A';
        public int seed = 1;
    }

    public class Result
    {
        public string variant;
        public char profile;
        public int seed;
        public bool victory;
        public int rounds;
        public int dead;
        public int revived;
        public string deadNames = "";
        public string hpEnd = "";
        public string minHp = "";
        public int potionsUsed, antidotesUsed;
        public int bossDamage, dmgP1, dmgP2;
        public Dictionary<string, int> dmgBy = new Dictionary<string, int>();
        public Dictionary<string, int> usesBy = new Dictionary<string, int>();
        public int phase2Round = -1;
        public int actionsP1, actionsP2;
        public int ultCasts, ultTargetsHit, ultWeakened, ultDamageSum;
        public int weakActions, weakAttackLoss;
        public int guardCasts, heals, potionUses, moves, buffs;
        public float minHpFrac = 1f;
        public int below25Events;
        // Objetivo Prioritario
        public int markApplied, bossSingleAttacks, attacksWhileMarkActive, markedHits, markExtraDamage;
        // Objetivo Prioritario medido en el registro de BattleUnit (cualquier golpe directo)
        public int markAllHits, markAllExtra;
        // Disciplina de Hierro: turnos del Capitan con DEF reforzada e impactos recibidos con ella
        public int bossTurns, boostBossTurns, boostHits, normalHits, normalHitDamage, boostSaved;
        // Oportunidades: por habilidad, turnos del Capitan en que estaba disponible / fue elegida
        public Dictionary<string, int> readyBy = new Dictionary<string, int>();
        // Fuego de Supresion
        public int fireNotices, fireResponded, fireIgnored;
        public Dictionary<string, int> fireResp = new Dictionary<string, int>();
        public string log = "";
    }

    static readonly List<Result> results = new List<Result>();
    public static IReadOnlyList<Result> Results => results;

    static UnitData bossData;
    static AbilityData ultAbility;
    static bool originalsCaptured;
    static int origHp, origAtk, origUlt;

    // ------------------------------------------------------------
    // Estado de llegada
    // ------------------------------------------------------------

    static List<GameSession.PartyMemberState> arrival;
    static int arrivalCoins, arrivalPotions, arrivalAntidotes;

    public static bool HasArrival { get { EnsureArrival(); return arrival != null; } }

    [System.Serializable]
    class Member
    {
        public string id;
        public int level, experience, health, maxHealth;
        public bool transformed, hasCurableStatus;
    }

    [System.Serializable]
    class ArrivalFile
    {
        public List<Member> party = new List<Member>();
        public int coins, potions, antidotes;
    }

    static string ArrivalPath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "BossBalanceArrival.json");

    static void SaveArrival()
    {
        ArrivalFile f = new ArrivalFile { coins = arrivalCoins, potions = arrivalPotions, antidotes = arrivalAntidotes };

        foreach (GameSession.PartyMemberState m in arrival)
            f.party.Add(new Member { id = m.id, level = m.level, experience = m.experience, health = m.health, maxHealth = m.maxHealth, transformed = m.transformed, hasCurableStatus = m.hasCurableStatus });

        File.WriteAllText(ArrivalPath, JsonUtility.ToJson(f, true));
    }

    static void EnsureArrival()
    {
        if (arrival != null || !File.Exists(ArrivalPath))
            return;

        ArrivalFile f = JsonUtility.FromJson<ArrivalFile>(File.ReadAllText(ArrivalPath));
        arrival = new List<GameSession.PartyMemberState>();

        foreach (Member m in f.party)
            arrival.Add(new GameSession.PartyMemberState { id = m.id, level = m.level, experience = m.experience, health = m.health, maxHealth = m.maxHealth, transformed = m.transformed, hasCurableStatus = m.hasCurableStatus });

        arrivalCoins = f.coins;
        arrivalPotions = f.potions;
        arrivalAntidotes = f.antidotes;
    }

    static readonly string[] CampaignIds = { "P1", "P2", "P3", "P4", "B1", "B2", "B3", "C1", "C2" };
    static int campaignIndex;
    static string campaignPending;
    static readonly StringBuilder campaignLog = new StringBuilder();
    static readonly string EncounterDir = "Assets/ScriptableObjects/Encounters/";

    static string PathFor(string id)
    {
        if (id == "BOSS") return EncounterDir + "Jefe/BOSS_Capitan.asset";
        string zone = id[0] == 'P' ? "Pradera" : id[0] == 'B' ? "Bosque" : "Corrupcion";
        return EncounterDir + zone + "/" + id + ".asset";
    }

    public static string CampaignStep()
    {
        if (campaignIndex == 0 && campaignPending == null)
        {
            GameSession.StartNewGame();
            campaignLog.Clear();
            campaignLog.AppendLine("Partida nueva: Coronas " + GameSession.Coins + ", pociones " + GameSession.Potions + ", antidotos " + GameSession.Antidotes);
        }

        if (campaignPending != null)
        {
            TurnManager t = Object.FindAnyObjectByType<TurnManager>();
            EncounterLoader el = Object.FindAnyObjectByType<EncounterLoader>();

            if (t == null || el == null || el.Current == null || el.Current.encounterId != campaignPending)
                return "ERROR: el encuentro " + campaignPending + " no esta cargado.";

            int coinsBefore = GameSession.Coins;

            foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
                if (u.gameObject.activeInHierarchy && u.Side == TeamSide.Enemy && u.IsAlive)
                    u.TakeDamage(99999);

            CombatHudController h = Object.FindAnyObjectByType<CombatHudController>();
            if (h != null) h.SendMessage("Update", SendMessageOptions.DontRequireReceiver);

            campaignLog.AppendLine(campaignPending + ": victoria=" + t.BattleFinished + " XP=" + t.LastExperienceAwarded + " Coronas " + coinsBefore + " -> " + GameSession.Coins + " | " + PartyText());

            if (campaignPending == "B1")
                campaignLog.AppendLine("Evento 'La mascara rota' (TryLiberateNoelle) = " + GameSession.TryLiberateNoelle());

            campaignPending = null;
            campaignIndex++;
        }

        if (campaignIndex >= CampaignIds.Length)
        {
            arrival = new List<GameSession.PartyMemberState>(GameSession.Party);
            arrivalCoins = GameSession.Coins;
            arrivalPotions = GameSession.Potions;
            arrivalAntidotes = GameSession.Antidotes;
            SaveArrival();
            campaignLog.AppendLine("LLEGADA AL JEFE: Coronas " + arrivalCoins + ", pociones " + arrivalPotions + ", antidotos " + arrivalAntidotes + " | " + PartyText());
            return campaignLog.ToString() + "CAMPANA COMPLETA";
        }

        string next = CampaignIds[campaignIndex];
        EncounterLoader.DevEncounter = AssetDatabase.LoadAssetAtPath<EncounterData>(PathFor(next));
        campaignPending = next;
        SceneManager.LoadScene("CombateIntegracion");
        return "cargando " + next;
    }

    public static string CampaignLog => campaignLog.ToString();

    static string PartyText()
    {
        StringBuilder sb = new StringBuilder();

        foreach (GameSession.PartyMemberState m in GameSession.Party)
            sb.Append(m.id.Replace("Player", "") + " Nv" + m.level + " XP" + m.experience + " HP" + m.health + "/" + m.maxHealth + (m.transformed ? " [liberada]" : "") + "; ");

        return sb.ToString();
    }

    // Paso 76: sesion con el equipo de llegada al jefe (nivel 5, Noelle
    // liberada), sin marcar encuentros, para probar el flujo desde el mapa.
    public static bool ApplyArrivalSnapshot()
    {
        EnsureArrival();

        if (arrival == null)
            return false;

        GameSession.StartNewGame();
        GameSession.AddCoins(arrivalCoins - GameSession.StartingCoins);
        GameSession.SavePartySnapshot(arrival);
        return true;
    }

    static void ResetSessionToArrival(int potions, int antidotes)
    {
        EnsureArrival();
        GameSession.StartNewGame();
        GameSession.AddCoins(arrivalCoins - GameSession.StartingCoins);

        int guardCount = 0;

        while (GameSession.Potions < potions && guardCount++ < 20)
            GameSession.TryBuy(GameSession.Consumable.Potion, ConsumableService.PotionPrice);

        while (GameSession.Potions > potions)
            GameSession.TryConsume(GameSession.Consumable.Potion);

        while (GameSession.Antidotes < antidotes && guardCount++ < 40)
            GameSession.TryBuy(GameSession.Consumable.Antidote, ConsumableService.AntidotePrice);

        GameSession.SavePartySnapshot(arrival);

        foreach (string id in CampaignIds)
            GameSession.MarkEncounterCompleted(id);

        GameSession.MarkEncounterCompleted(GameSession.NoelleEventId);
    }

    // ------------------------------------------------------------
    // Variantes del jefe (temporales, en memoria)
    // ------------------------------------------------------------

    static void CaptureOriginals()
    {
        if (originalsCaptured)
            return;

        bossData = AssetDatabase.LoadAssetAtPath<UnitData>("Assets/ScriptableObjects/Enemies/Jefe/CapitanExpedicionario.asset");
        ultAbility = AssetDatabase.LoadAssetAtPath<AbilityData>("Assets/ScriptableObjects/Abilities/Enemies/Jefe/Capitan_UltimaOrden.asset");
        origHp = bossData.maxHealth;
        origAtk = bossData.attack;
        origUlt = ultAbility.basePower;
        originalsCaptured = true;
    }

    static void ApplyVariant(Variant v)
    {
        CaptureOriginals();
        bossData.maxHealth = v.hp;
        bossData.attack = v.atk;
        ultAbility.basePower = v.ult;
    }

    public static string RestoreOriginals()
    {
        if (!originalsCaptured)
            return "sin cambios";

        bossData.maxHealth = origHp;
        bossData.attack = origAtk;
        ultAbility.basePower = origUlt;
        return "restaurado: HP " + bossData.maxHealth + ", ATK " + bossData.attack + ", poder Ultima Orden " + ultAbility.basePower;
    }

    public static string CurrentBossValues()
    {
        CaptureOriginals();
        return "HP " + bossData.maxHealth + ", ATK " + bossData.attack + ", DEF " + bossData.defense + ", SPD " + bossData.speed + ", crit " + bossData.criticalChance + ", Ultima Orden " + ultAbility.basePower;
    }

    // ------------------------------------------------------------
    // Cola de pruebas
    // ------------------------------------------------------------

    static readonly Queue<Variant> queue = new Queue<Variant>();
    static Variant loadedVariant;

    public static void Enqueue(Variant v, int repeats)
    {
        for (int i = 0; i < repeats; i++)
            queue.Enqueue(new Variant { name = v.name, hp = v.hp, atk = v.atk, ult = v.ult, potions = v.potions, antidotes = v.antidotes, profile = v.profile, seed = v.seed + i });
    }

    public static int Pending => queue.Count + (loadedVariant != null ? 1 : 0);

    static void PrepareNext()
    {
        if (queue.Count == 0)
        {
            loadedVariant = null;
            return;
        }

        Variant v = queue.Dequeue();
        loadedVariant = v;
        ResetSessionToArrival(v.potions, v.antidotes);
        ApplyVariant(v);
        EncounterLoader.DevEncounter = AssetDatabase.LoadAssetAtPath<EncounterData>(PathFor("BOSS"));
        SceneManager.LoadScene("CombateIntegracion");
    }

    public static string Pump()
    {
        string text = "";

        if (loadedVariant != null)
        {
            Result r = Fight(loadedVariant);
            results.Add(r);
            text = Line(r);
        }

        PrepareNext();

        if (loadedVariant == null)
            text += "\n(cola vacia) " + RestoreOriginals();

        return text;
    }

    public static void ClearResults()
    {
        results.Clear();
    }

    // ------------------------------------------------------------
    // Combate
    // ------------------------------------------------------------

    static CombatHudController hud;
    static TurnManager tm;
    static Transform cv;
    static BattleUnit boss;
    static List<BattleUnit> heroes;
    static PositionManager pm;
    static System.Random rng;
    static char profile;

    static AbilityData lastAbility;
    static BattleUnit lastTarget;
    static bool bossActed;
    static int phaseRound;
    static int damageBucket;
    static readonly Dictionary<BattleUnit, int> perUnitBucket = new Dictionary<BattleUnit, int>();
    static string actionType;
    static bool noticeAware;

    static void OnAct(BattleUnit u, AbilityData a, BattleUnit t, bool sp)
    {
        if (u == boss) { lastAbility = a; lastTarget = t; bossActed = true; }
    }

    static void OnPhase(BossPhaseController c, BossPhaseData.Phase a, BossPhaseData.Phase b)
    {
        phaseRound = tm != null ? tm.CurrentRound : -1;
    }

    static int logMarkHits, logMarkExtra, bossHitsBoost, bossHitsNormal, bossHitDamageNormal, bossBoostSaved;

    // Registro de BattleUnit: "X es objetivo prioritario de Y: daño A -> B".
    static void OnLog(string message, string stack, LogType type)
    {
        if (message == null || !message.Contains("es objetivo prioritario de"))
            return;

        System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(message, @"da.o (\d+) -> (\d+)");

        if (m.Success)
        {
            logMarkHits++;
            logMarkExtra += int.Parse(m.Groups[2].Value) - int.Parse(m.Groups[1].Value);
        }
    }

    static void OnDamage(BattleUnit unit, int amount, bool periodic)
    {
        // Impactos recibidos por el Capitan (con o sin DEF reforzada).
        if (unit != null && unit == boss && !periodic && amount > 0)
        {
            StatusEffectController fx = boss.GetComponent<StatusEffectController>();

            if (fx != null && fx.HasEffect(StatusEffectType.DefenseBoost)) { bossHitsBoost++; bossBoostSaved += boss.Defense - boss.Data.defense; }
            else { bossHitsNormal++; bossHitDamageNormal += amount; }

            return;
        }

        if (unit == null || unit.Side != TeamSide.Player || periodic || amount <= 0)
            return;

        damageBucket += amount;
        perUnitBucket.TryGetValue(unit, out int n);
        perUnitBucket[unit] = n + amount;
    }

    static void Tick()
    {
        hud.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
    }

    static Button Btn(string path)
    {
        Transform t = cv.Find(path);
        return t != null && t.gameObject.activeInHierarchy ? t.GetComponent<Button>() : null;
    }

    static int CellOf(BattleUnit u)
    {
        return u.Side == TeamSide.Enemy ? 4 : 4 - u.Position;
    }

    static float Frac(BattleUnit u)
    {
        return u.MaxHealth <= 0 ? 1f : (float)u.CurrentHealth / u.MaxHealth;
    }

    public static string Describe()
    {
        StringBuilder sb = new StringBuilder();

        foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
        {
            if (!u.gameObject.activeInHierarchy || u.Side != TeamSide.Player)
                continue;

            CharacterProgression p = u.GetComponent<CharacterProgression>();
            sb.AppendLine(u.Data.unitName + " P" + u.Position + ": Nv" + p.Level + " XP " + p.CurrentExperience + "/" + p.ExperienceRequired +
                " | HP " + u.CurrentHealth + "/" + u.MaxHealth + " ATK " + u.Attack + " DEF " + u.Defense + " SPD " + u.Speed + " crit " + u.CriticalChance + "%");
        }

        return sb.ToString();
    }

    static Result Fight(Variant v)
    {
        hud = Object.FindAnyObjectByType<CombatHudController>();
        tm = Object.FindAnyObjectByType<TurnManager>();
        pm = Object.FindAnyObjectByType<PositionManager>();
        cv = hud.transform.Find("Canvas/Marco");
        profile = v.profile;
        rng = new System.Random(v.seed * 7919 + v.profile);
        Random.InitState(v.seed * 104729 + v.profile);

        heroes = new List<BattleUnit>();
        boss = null;

        foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
        {
            if (!u.gameObject.activeInHierarchy)
                continue;

            if (u.Side == TeamSide.Enemy) boss = u; else heroes.Add(u);
        }

        Result r = new Result { variant = v.name, profile = v.profile, seed = v.seed };
        r.log = "inicio " + (boss != null ? boss.MaxHealth + " HP, ATK " + boss.Attack : "SIN JEFE") + " | " + Describe().Replace("\n", " / ");

        if (boss == null || tm == null || tm.BattleFinished)
        {
            r.log += " | ERROR: escena no preparada";
            return r;
        }

        EnemyAIController.ActionPerformed -= OnAct;
        EnemyAIController.ActionPerformed += OnAct;
        BossPhaseController.PhaseChanged -= OnPhase;
        BossPhaseController.PhaseChanged += OnPhase;
        BattleUnit.DamageApplied -= OnDamage;
        BattleUnit.DamageApplied += OnDamage;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        logMarkHits = 0; logMarkExtra = 0; bossHitsBoost = 0; bossHitsNormal = 0; bossHitDamageNormal = 0; bossBoostSaved = 0;
        phaseRound = -1;

        BossPhaseController phases = boss.GetComponent<BossPhaseController>();
        PreparedAbilityController prepared = boss.GetComponent<PreparedAbilityController>();
        AbilityData fire = boss.GetComponent<EnemyAIController>().Archetype.specialAbilities.Find(a => a.requiresPreparation);
        int potions0 = GameSession.Potions;
        int antidotes0 = GameSession.Antidotes;
        int guard = 0;
        var dead = new HashSet<BattleUnit>();
        var minHp = new Dictionary<BattleUnit, int>();
        foreach (BattleUnit h in heroes) minHp[h] = h.CurrentHealth;
        string lastSnapshot = "";
        bool noticeResponded = false;

        while (!tm.BattleFinished && guard++ < 1500)
        {
            BattleUnit cur = tm.CurrentUnit;
            Tick();

            prepared = boss.GetComponent<PreparedAbilityController>();
            lastSnapshot = string.Join(", ", heroes.ConvertAll(h => h.Data.unitName + " " + h.CurrentHealth + "/" + h.MaxHealth));

            foreach (BattleUnit h in heroes)
            {
                if (h.IsAlive)
                {
                    if (h.CurrentHealth < minHp[h]) minHp[h] = h.CurrentHealth;
                    float f = Frac(h);
                    if (f < r.minHpFrac) r.minHpFrac = f;
                }
                else
                {
                    minHp[h] = 0;
                    if (dead.Add(h)) r.deadNames += h.Data.unitName + "(R" + tm.CurrentRound + ",F" + (phases.CurrentIndex + 1) + ") ";
                }
            }

            if (cur.Side == TeamSide.Enemy)
            {
                int phIdx = phases.CurrentIndex;
                lastAbility = null;
                lastTarget = null;
                bossActed = false;
                damageBucket = 0;
                perUnitBucket.Clear();

                // Oportunidades: que habilidades estaban disponibles en este turno.
                r.bossTurns++;
                StatusEffectController bfx = boss.GetComponent<StatusEffectController>();
                if (bfx.HasEffect(StatusEffectType.DefenseBoost)) r.boostBossTurns++;
                AbilityCooldownController bcd = boss.GetComponent<AbilityCooldownController>();
                bool pendingFire = prepared != null && prepared.HasPreparation;
                var archetype = boss.GetComponent<EnemyAIController>().Archetype;
                var allAbilities = new List<AbilityData> { archetype.basicAttack };
                allAbilities.AddRange(archetype.specialAbilities);

                if (!pendingFire)
                {
                    foreach (AbilityData ab in allAbilities)
                    {
                        if (ab == null || !bcd.IsReady(ab) || !phases.IsAbilityAvailable(ab)) continue;
                        r.readyBy[ab.abilityName] = (r.readyBy.ContainsKey(ab.abilityName) ? r.readyBy[ab.abilityName] : 0) + 1;
                    }
                }

                var markedBefore = new HashSet<BattleUnit>();
                foreach (BattleUnit h in heroes)
                {
                    StatusEffectInstance m = h.IsAlive ? h.GetComponent<StatusEffectController>().GetEffect(StatusEffectType.PriorityTarget) : null;
                    if (m != null && m.Source == boss) markedBefore.Add(h);
                }

                cur.GetComponent<EnemyAIController>().PerformTurn();

                if (lastAbility != null)
                {
                    string n = lastAbility.abilityName;
                    bool isFire = lastAbility == fire;
                    r.usesBy[n] = (r.usesBy.ContainsKey(n) ? r.usesBy[n] : 0) + 1;
                    string key = isFire ? n + " (anuncio)" : n;
                    r.dmgBy[key] = (r.dmgBy.ContainsKey(key) ? r.dmgBy[key] : 0) + damageBucket;
                    r.bossDamage += damageBucket;
                    if (phIdx == 0) r.dmgP1 += damageBucket; else r.dmgP2 += damageBucket;

                    if (isFire)
                    {
                        r.fireNotices++;
                        noticeResponded = false;
                        noticeAware = profile == 'A' || (profile == 'B') || rng.NextDouble() < 0.5;
                    }

                    if (n.StartsWith("Objetivo")) r.markApplied++;

                    if (lastAbility.dealsDamage && lastAbility.targetType == AbilityTargetType.Single && lastTarget != null && lastTarget.Side == TeamSide.Player)
                    {
                        r.bossSingleAttacks++;

                        if (markedBefore.Count > 0)
                        {
                            r.attacksWhileMarkActive++;

                            if (markedBefore.Contains(lastTarget) && damageBucket > 0)
                            {
                                r.markedHits++;
                                r.markExtraDamage += Mathf.RoundToInt(damageBucket * 0.2f / 1.2f);
                            }
                        }
                    }

                    if (n.StartsWith("Última"))
                    {
                        r.ultCasts++;
                        r.ultDamageSum += damageBucket;
                        r.ultTargetsHit += perUnitBucket.Count;

                        foreach (BattleUnit h in heroes)
                        {
                            StatusEffectInstance w = h.IsAlive ? h.GetComponent<StatusEffectController>().GetEffect(StatusEffectType.Weaken) : null;
                            if (w != null && w.Source == boss) r.ultWeakened++;
                        }
                    }
                }

                if (phIdx == 0) r.actionsP1++; else r.actionsP2++;
                tm.EndTurn();
            }
            else
            {
                bool wasPending = prepared != null && prepared.HasPreparation;
                int phIdx = phases.CurrentIndex;
                damageBucket = 0;
                perUnitBucket.Clear();

                StatusEffectInstance wk = cur.GetComponent<StatusEffectController>().GetEffect(StatusEffectType.Weaken);
                if (wk != null && wk.Source == boss)
                {
                    r.weakActions++;
                    r.weakAttackLoss += Mathf.RoundToInt(cur.Attack / (1f - wk.Potency / 100f)) - cur.Attack;
                }

                actionType = "ninguna";
                HeroTurn(cur, r, prepared, phases);
                prepared = boss.GetComponent<PreparedAbilityController>();

                if (wasPending)
                {
                    r.fireResp[actionType] = (r.fireResp.ContainsKey(actionType) ? r.fireResp[actionType] : 0) + 1;
                    if (actionType == "guardia" || actionType == "movimiento" || actionType == "curacion" || actionType == "pocion" || actionType == "apoyo") noticeResponded = true;
                }

                if (wasPending && prepared != null && !prepared.HasPreparation)
                {
                    string key = "Fuego de Supresión (impacto)";
                    r.dmgBy[key] = (r.dmgBy.ContainsKey(key) ? r.dmgBy[key] : 0) + damageBucket;
                    r.bossDamage += damageBucket;
                    if (phases.CurrentIndex == 0) r.dmgP1 += damageBucket; else r.dmgP2 += damageBucket;
                    r.usesBy[key] = (r.usesBy.ContainsKey(key) ? r.usesBy[key] : 0) + 1;

                    if (phases.CurrentIndex == 0) r.actionsP1++; else r.actionsP2++;
                    if (noticeResponded) r.fireResponded++; else r.fireIgnored++;
                }

                foreach (BattleUnit h in heroes)
                {
                    if (!h.IsAlive)
                    {
                        minHp[h] = 0;
                        if (dead.Add(h)) r.deadNames += h.Data.unitName + "(R" + tm.CurrentRound + ",F" + (phases.CurrentIndex + 1) + ") ";
                    }
                }
            }
        }

        foreach (BattleUnit h in heroes)
        {
            if (!h.IsAlive && dead.Add(h))
            {
                minHp[h] = 0;
                r.deadNames += h.Data.unitName + "(R" + tm.CurrentRound + ",F" + (phases.CurrentIndex + 1) + ") ";
            }
        }

        r.markAllHits = logMarkHits;
        r.markAllExtra = logMarkExtra;
        r.boostHits = bossHitsBoost; r.boostSaved = bossBoostSaved;
        r.normalHits = bossHitsNormal;
        r.normalHitDamage = bossHitDamageNormal;
        Application.logMessageReceived -= OnLog;
        r.victory = !boss.IsAlive;
        r.rounds = tm.CurrentRound;
        r.dead = dead.Count;
        foreach (BattleUnit h in dead) if (h.IsAlive) r.revived++;   // la victoria revive con el 40 %
        r.phase2Round = phaseRound;
        r.potionsUsed = potions0 - GameSession.Potions;
        r.antidotesUsed = antidotes0 - GameSession.Antidotes;
        r.hpEnd = lastSnapshot;
        r.minHp = string.Join(", ", heroes.ConvertAll(h => h.Data.unitName + " " + minHp[h]));
        return r;
    }

    // ------------------------------------------------------------
    // Clics sobre el HUD
    // ------------------------------------------------------------

    static bool Acted(BattleUnit cur)
    {
        return tm.BattleFinished || tm.CurrentUnit != cur;
    }

    static void CancelPending()
    {
        Button cancel = Btn("PanelInformacion/Volver");
        if (cancel != null) { cancel.onClick.Invoke(); Tick(); }
    }

    static bool TryAbility(BattleUnit cur, int index, int cell)
    {
        Button btn = Btn("PanelPersonaje/Habilidad_" + index);
        if (btn == null || !btn.interactable) return false;

        AbilityData ab = cur.GetComponent<CharacterAbilityLoadout>().Abilities[index];

        btn.onClick.Invoke(); Tick();
        if (Acted(cur)) return true;

        if (ab != null && ab.requiresPreparation)
        {
            btn.onClick.Invoke(); Tick();
            if (Acted(cur)) return true;
        }

        if (cell >= 0)
        {
            Button c = Btn("Unidades/Casilla_" + cell);
            if (c != null && c.interactable) { c.onClick.Invoke(); Tick(); }
        }

        if (Acted(cur)) return true;

        CancelPending();
        return false;
    }

    static bool TryItem(BattleUnit cur, string button, BattleUnit target)
    {
        Button b = Btn("PanelConsumibles/" + button);
        if (b == null || !b.interactable) return false;

        b.onClick.Invoke(); Tick();
        Button c = Btn("Unidades/Casilla_" + CellOf(target));
        if (c != null && c.interactable) { c.onClick.Invoke(); Tick(); }
        if (Acted(cur)) return true;

        CancelPending();
        return false;
    }

    static bool TryMove(BattleUnit cur, int dir)
    {
        if (!pm.CanMoveUnit(cur, dir)) return false;

        Button move = Btn("PanelInformacion/Mover");
        if (move == null || !move.interactable) return false;

        move.onClick.Invoke(); Tick();
        Button option = Btn("PanelPersonaje/OpcionesMovimiento/" + (dir < 0 ? "Avanzar" : "Retroceder"));

        if (option != null && option.interactable)
        {
            option.onClick.Invoke(); Tick();
            if (!Acted(cur)) { option.onClick.Invoke(); Tick(); }
        }

        if (Acted(cur)) return true;

        CancelPending();
        return false;
    }

    static int Find(CharacterAbilityLoadout lo, System.Func<AbilityData, bool> pred)
    {
        for (int i = 0; i < lo.Abilities.Count; i++)
            if (lo.Abilities[i] != null && pred(lo.Abilities[i])) return i;

        return -1;
    }

    static bool Ready(int index)
    {
        if (index < 0) return false;
        Button b = Btn("PanelPersonaje/Habilidad_" + index);
        return b != null && b.interactable;
    }

    static bool Roll(double p)
    {
        return rng.NextDouble() < p;
    }

    // ------------------------------------------------------------
    // Politicas por perfil
    // ------------------------------------------------------------

    static void HeroTurn(BattleUnit cur, Result r, PreparedAbilityController bossPrep, BossPhaseController phases)
    {
        CharacterAbilityLoadout lo = cur.GetComponent<CharacterAbilityLoadout>();
        List<BattleUnit> alive = heroes.FindAll(h => h.IsAlive);
        BattleUnit low = null;

        foreach (BattleUnit h in alive)
            if (low == null || Frac(h) < Frac(low)) low = h;

        foreach (BattleUnit h in alive)
            if (Frac(h) < 0.25f) { r.below25Events++; break; }

        bool firePending = bossPrep != null && bossPrep.HasPreparation && bossPrep.PreparedArea != null;
        AbilityPositionArea area = firePending ? bossPrep.PreparedArea : null;

        int guardIdx = Find(lo, a => a.allyProtectionPercent > 0);
        int healIdx = Find(lo, a => a.abilityType == AbilityType.Heal);
        int groupBuffIdx = Find(lo, a => a.targetType == AbilityTargetType.AllAllies && a.regenerationPerTurn > 0);
        int groupDefIdx = Find(lo, a => a.targetType == AbilityTargetType.AllAllies && a.regenerationPerTurn <= 0 && a.abilityType == AbilityType.Support);

        // Umbrales por perfil
        float healAt = profile == 'A' ? 0.5f : profile == 'B' ? 0.35f : 0.20f;
        float potionAt = profile == 'A' ? 0.35f : profile == 'B' ? 0.25f : 0.15f;
        float dangerAt = profile == 'A' ? 0.90f : profile == 'B' ? 0.45f : 0.60f;

        // 1) Fuego de Supresion
        bool reacts = firePending && (profile == 'A' || profile == 'B' || noticeAware);

        if (reacts)
        {
            List<BattleUnit> threatened = alive.FindAll(h => area.Contains(h.Position));
            BattleUnit danger = null;

            foreach (BattleUnit h in threatened)
            {
                bool fragile = profile == 'A' ? Frac(h) * h.MaxHealth < 100f : Frac(h) <= dangerAt;
                if (fragile && (danger == null || Frac(h) < Frac(danger))) danger = h;
            }

            if (danger != null)
            {
                if (guardIdx >= 0 && Ready(guardIdx) && danger != cur)
                {
                    BattleUnit pick = danger;

                    if (profile == 'A')
                    {
                        pick = null;
                        foreach (BattleUnit h in threatened)
                            if (h != cur && (pick == null || Frac(h) * h.MaxHealth < Frac(pick) * pick.MaxHealth)) pick = h;
                    }

                    if (pick != null && TryAbility(cur, guardIdx, CellOf(pick))) { r.guardCasts++; actionType = "guardia"; return; }
                }

                if (cur == danger && (profile == 'A' ? cur.MaxHealth < 110 && Frac(cur) < 0.9f : true))
                {
                    foreach (int dir in new[] { 1, -1 })
                    {
                        int targetPos = cur.Position + dir;
                        if (targetPos < 1 || targetPos > 4 || area.Contains(targetPos)) continue;
                        BattleUnit partner = pm.GetUnitAtPosition(TeamSide.Player, targetPos);
                        if (partner != null && partner.CurrentHealth <= cur.CurrentHealth) continue;
                        if (TryMove(cur, dir)) { r.moves++; actionType = "movimiento"; return; }
                    }
                }
            }
        }

        // 2) Curacion
        if (low != null && Frac(low) <= healAt && (profile != 'C' || Roll(0.8)))
        {
            if (healIdx >= 0 && Ready(healIdx) && TryAbility(cur, healIdx, CellOf(low))) { r.heals++; actionType = "curacion"; return; }

            if (Frac(low) <= potionAt && GameSession.Potions > 0 && TryItem(cur, "Pocion", low)) { r.heals++; r.potionUses++; actionType = "pocion"; return; }
        }

        // 3) Antidoto
        if (GameSession.Antidotes > 0)
        {
            foreach (BattleUnit h in alive)
            {
                StatusEffectController fx = h.GetComponent<StatusEffectController>();
                bool bad = fx.HasEffect(StatusEffectType.Bleeding) || fx.HasEffect(StatusEffectType.Poison) || fx.HasEffect(StatusEffectType.Burn);
                if (bad && Frac(h) <= (profile == 'A' ? 0.6f : 0.4f) && TryItem(cur, "Antidoto", h)) { actionType = "antidoto"; return; }
            }
        }

        // 4) Guardia segun perfil (A: sobre el marcado; B/C: ocasional)
        if (guardIdx >= 0 && Ready(guardIdx))
        {
            BattleUnit target = null;

            if (profile == 'A')
            {
                foreach (BattleUnit h in alive)
                    if (h != cur && h.GetComponent<StatusEffectController>().HasEffect(StatusEffectType.PriorityTarget)) target = h;
            }
            else if (profile == 'B' && Roll(0.35))
            {
                foreach (BattleUnit h in alive)
                    if (h != cur && Frac(h) < 0.8f && (target == null || Frac(h) < Frac(target))) target = h;
            }
            else if (profile == 'C' && Roll(0.15))
            {
                List<BattleUnit> others = alive.FindAll(h => h != cur);
                if (others.Count > 0) target = others[rng.Next(others.Count)];
            }

            if (target != null && TryAbility(cur, guardIdx, CellOf(target))) { r.guardCasts++; actionType = "guardia"; return; }
        }

        // 5) Apoyo de grupo / curacion preventiva
        int hurt = alive.FindAll(h => Frac(h) < (profile == 'A' ? 0.8f : 0.7f)).Count;
        double supportChance = profile == 'A' ? 1.0 : profile == 'B' ? 0.5 : 0.25;

        if (groupBuffIdx >= 0 && hurt >= 2 && Ready(groupBuffIdx) && Roll(supportChance) && TryAbility(cur, groupBuffIdx, -1)) { r.buffs++; r.heals++; actionType = "apoyo"; return; }
        if (profile == 'A' && healIdx >= 0 && low != null && Frac(low) <= 0.65f && Ready(healIdx) && TryAbility(cur, healIdx, CellOf(low))) { r.heals++; actionType = "curacion"; return; }
        if (groupDefIdx >= 0 && (profile == 'A' ? (firePending || phases.CurrentIndex == 1) : (hurt >= 2)) && Ready(groupDefIdx) && Roll(supportChance) && TryAbility(cur, groupDefIdx, -1)) { r.buffs++; actionType = "apoyo"; return; }

        // 6) Ataque
        bool bossMarked = boss.GetComponent<StatusEffectController>().HasEffect(StatusEffectType.Mark);
        List<int> order = new List<int>();
        for (int i = 0; i < lo.Abilities.Count; i++) order.Add(i);
        order.Sort((a, b) => Score(lo.Abilities[b], bossMarked).CompareTo(Score(lo.Abilities[a], bossMarked)));

        List<int> candidates = new List<int>();

        foreach (int i in order)
        {
            AbilityData ab = lo.Abilities[i];
            if (ab == null || ab.abilityType != AbilityType.Attack) continue;
            if (ab.requiresPreparation && (boss.CurrentHealth < 60 || cur.GetComponent<PreparedAbilityController>() != null && cur.GetComponent<PreparedAbilityController>().HasPreparation)) continue;
            // El perfil C puede repetir la Marca o lanzar Meteorito con el jefe casi muerto: errores "razonables".
            if (profile != 'C' && ab.statusEffects != null && ab.statusEffects.Exists(s => s.type == StatusEffectType.Mark) && bossMarked) continue;
            candidates.Add(i);
        }

        // Orden de intentos segun el perfil
        List<int> attempts = new List<int>(candidates);

        if (profile != 'A' && attempts.Count > 1)
        {
            double randomChance = profile == 'B' ? 0.30 : 0.50;

            if (Roll(randomChance))
            {
                int pick = attempts[rng.Next(attempts.Count)];
                attempts.Remove(pick);
                attempts.Insert(0, pick);
            }

            // Perfil C: a veces no aprovecha una habilidad con recarga (usa las de recarga 0 primero).
            if (profile == 'C' && Roll(0.25))
            {
                attempts.Sort((a, b) => lo.Abilities[a].cooldown.CompareTo(lo.Abilities[b].cooldown));
            }
        }

        foreach (int i in attempts)
        {
            AbilityData ab = lo.Abilities[i];
            int cell = ab.targetType == AbilityTargetType.Single ? 4 : -1;
            if (TryAbility(cur, i, cell)) { actionType = "ataque"; return; }
        }

        Button end = Btn("PanelInformacion/TerminarTurno");
        if (end != null && !Acted(cur)) { end.onClick.Invoke(); Tick(); actionType = "fin de turno (sin accion posible)"; }
    }

    static float Score(AbilityData a, bool bossMarked)
    {
        if (a == null) return -100f;

        float s = a.basePower;

        if (a.statusEffects != null && a.statusEffects.Exists(x => x.type == StatusEffectType.Mark))
            s += bossMarked ? -50f : 10f;

        if (a.requiresPreparation) s += 6f;
        if (a.dealsDamage == false) s -= 20f;

        return s;
    }

    // ------------------------------------------------------------
    // Resumen
    // ------------------------------------------------------------

    static string Short(string k)
    {
        return k.Replace(" de Campaña", "").Replace(" Implacable", "").Replace(" de Hierro", "").Replace("Fuego de Supresión", "Fuego").Replace(" Prioritario", "");
    }

    static string Line(Result r)
    {
        StringBuilder dm = new StringBuilder();
        foreach (var kv in r.dmgBy) dm.Append(Short(kv.Key) + ":" + kv.Value + " ");

        return "[" + r.variant + " s" + r.seed + "] " + (r.victory ? "VICTORIA" : "DERROTA") + " R" + r.rounds + " (F2 desde R" + r.phase2Round + ", acc F1/F2 " + r.actionsP1 + "/" + r.actionsP2 + ")" +
            " dano " + r.bossDamage + " [F1 " + r.dmgP1 + "/F2 " + r.dmgP2 + "] {" + dm.ToString().Trim() + "} bajas " + r.dead + (r.deadNames != "" ? " (" + r.deadNames.Trim() + ")" : "") + " revividos " + r.revived +
            " pociones " + r.potionsUsed + " antidotos " + r.antidotesUsed + " curas " + r.heals + " guardias " + r.guardCasts + " mov " + r.moves +
            " minHP% " + Mathf.RoundToInt(r.minHpFrac * 100f) + " | Obj: marcas " + r.markApplied + " golpes al marcado " + r.markedHits + "/" + r.attacksWhileMarkActive + " (+" + r.markExtraDamage + ") | Fuego: avisos " + r.fireNotices + " con respuesta " + r.fireResponded + " ignorados " + r.fireIgnored +
            " | Ult " + r.ultCasts + (r.ultCasts > 0 ? " (dano " + r.ultDamageSum + ", debil " + r.ultWeakened + ")" : "") + " | Disciplina " + (r.usesBy.ContainsKey("Disciplina de Hierro") ? r.usesBy["Disciplina de Hierro"] : 0);
    }

    public static string Summary()
    {
        StringBuilder sb = new StringBuilder();
        List<string> names = new List<string>();
        foreach (Result r in results) if (!names.Contains(r.variant)) names.Add(r.variant);

        foreach (string name in names)
        {
            int n = 0, wins = 0, deadSum = 0, rSum = 0, rMin = 999, rMax = 0, potions = 0, antidotes = 0, dmg = 0, d1 = 0, d2 = 0, p2 = 0, p2n = 0, a1 = 0, a2 = 0, ult = 0, ultN = 0, ultDmg = 0, ultWeak = 0, ultHit = 0;
            int guards = 0, moves = 0, heals = 0, below = 0, revived = 0, deadF1 = 0, deadF2 = 0, weakActs = 0, weakLoss = 0, marks = 0, mHits = 0, mAtt = 0, mExtra = 0, notices = 0, responded = 0, ignored = 0, disc = 0, fights25 = 0;
            float minHp = 0f;
            Dictionary<string, int> dmgBy = new Dictionary<string, int>();
            Dictionary<string, int> useBy = new Dictionary<string, int>();
            Dictionary<string, int> resp = new Dictionary<string, int>();

            foreach (Result r in results)
            {
                if (r.variant != name) continue;
                n++; if (r.victory) wins++; deadSum += r.dead; revived += r.revived; rSum += r.rounds; rMin = Mathf.Min(rMin, r.rounds); rMax = Mathf.Max(rMax, r.rounds);
                potions += r.potionsUsed; antidotes += r.antidotesUsed; dmg += r.bossDamage; d1 += r.dmgP1; d2 += r.dmgP2; if (r.phase2Round > 0) { p2 += r.phase2Round; p2n++; }
                a1 += r.actionsP1; a2 += r.actionsP2; ult += r.ultCasts; if (r.ultCasts > 0) ultN++; ultDmg += r.ultDamageSum; ultHit += r.ultTargetsHit; ultWeak += r.ultWeakened;
                guards += r.guardCasts; moves += r.moves; heals += r.heals; minHp += r.minHpFrac; if (r.below25Events > 0) fights25++;
                weakActs += r.weakActions; weakLoss += r.weakAttackLoss; marks += r.markApplied; mHits += r.markedHits; mAtt += r.attacksWhileMarkActive; mExtra += r.markExtraDamage;
                notices += r.fireNotices; responded += r.fireResponded; ignored += r.fireIgnored;
                if (r.usesBy.ContainsKey("Disciplina de Hierro")) disc += r.usesBy["Disciplina de Hierro"];
                foreach (string part in r.deadNames.Split(' ')) { if (part.Contains(",F1)")) deadF1++; else if (part.Contains(",F2)")) deadF2++; }
                foreach (var kv in r.dmgBy) dmgBy[kv.Key] = (dmgBy.ContainsKey(kv.Key) ? dmgBy[kv.Key] : 0) + kv.Value;
                foreach (var kv in r.usesBy) useBy[kv.Key] = (useBy.ContainsKey(kv.Key) ? useBy[kv.Key] : 0) + kv.Value;
                foreach (var kv in r.fireResp) resp[kv.Key] = (resp.ContainsKey(kv.Key) ? resp[kv.Key] : 0) + kv.Value;
            }

            if (n == 0) continue;

            StringBuilder dm = new StringBuilder();
            foreach (var kv in dmgBy) dm.Append(Short(kv.Key) + " " + (kv.Value / (float)n).ToString("0.0") + "; ");
            StringBuilder us = new StringBuilder();
            foreach (var kv in useBy) us.Append(Short(kv.Key) + " " + (kv.Value / (float)n).ToString("0.0") + "; ");
            StringBuilder rs = new StringBuilder();
            foreach (var kv in resp) rs.Append(kv.Key + " " + kv.Value + "; ");

            sb.AppendLine("== " + name + " | n=" + n + " victorias " + wins + "/" + n + " derrotas " + (n - wins) + " | rondas " + (rSum / (float)n).ToString("0.0") + " (min " + rMin + ", max " + rMax + ") | F2 desde R " + (p2n > 0 ? (p2 / (float)p2n).ToString("0.0") : "-") +
                " | acciones F1/F2 " + (a1 / (float)n).ToString("0.0") + "/" + (a2 / (float)n).ToString("0.0"));
            sb.AppendLine("   bajas/combate " + (deadSum / (float)n).ToString("0.00") + " (en F1 " + deadF1 + ", en F2 " + deadF2 + "; revividos por la victoria " + revived + ") | pociones " + (potions / (float)n).ToString("0.00") + " antidotos " + (antidotes / (float)n).ToString("0.00") +
                " | curas " + (heals / (float)n).ToString("0.0") + " guardias " + (guards / (float)n).ToString("0.0") + " mov " + (moves / (float)n).ToString("0.0") + " | HP minimo medio " + Mathf.RoundToInt(minHp / n * 100f) + "% | combates con heroe <25%: " + fights25 + "/" + n);
            sb.AppendLine("   dano jefe " + (dmg / (float)n).ToString("0") + " (F1 " + (d1 / (float)n).ToString("0") + " / F2 " + (d2 / (float)n).ToString("0") + ") por habilidad [" + dm.ToString().Trim() + "] usos [" + us.ToString().Trim() + "]");
            sb.AppendLine("   Obj.Prioritario: marcas " + (marks / (float)n).ToString("0.0") + ", golpes al marcado " + mHits + " de " + mAtt + " ataques con marca activa, dano extra total " + mExtra +
                " | Fuego: avisos " + notices + ", con respuesta " + responded + ", ignorados " + ignored + " | acciones en la ventana: " + rs.ToString().Trim() +
                " | Ult: usos " + (ult / (float)n).ToString("0.0") + " en " + ultN + "/" + n + " combates, dano medio " + (ult > 0 ? (ultDmg / (float)ult).ToString("0") : "-") + ", debilitados/uso " + (ult > 0 ? (ultWeak / (float)ult).ToString("0.0") : "-") +
                ", acciones de heroes debilitados " + weakActs + " (ATK perdido total " + weakLoss + ") | Disciplina " + disc + " usos en " + n + " combates");
            sb.Append(Extra(name));
        }

        return sb.ToString();
    }

    // Metricas del Paso 74: aprovechamiento de Objetivo Prioritario y Disciplina de Hierro.
    static string Extra(string name)
    {
        int n = 0, mh = 0, mx = 0, bt = 0, bbt = 0, bh = 0, nh = 0, nd = 0, ultAll = 0, bsv = 0;
        Dictionary<string, int> ready = new Dictionary<string, int>();
        Dictionary<string, int> used = new Dictionary<string, int>();

        foreach (Result r in results)
        {
            if (r.variant != name) continue;
            n++; mh += r.markAllHits; mx += r.markAllExtra; bt += r.bossTurns; bbt += r.boostBossTurns; bh += r.boostHits; bsv += r.boostSaved; nh += r.normalHits; nd += r.normalHitDamage;
            foreach (var kv in r.readyBy) ready[kv.Key] = (ready.ContainsKey(kv.Key) ? ready[kv.Key] : 0) + kv.Value;
            foreach (var kv in r.usesBy) if (!kv.Key.Contains("(impacto)")) used[kv.Key] = (used.ContainsKey(kv.Key) ? used[kv.Key] : 0) + kv.Value;
        }

        if (n == 0) return "";

        float avgHit = nh > 0 ? nd / (float)nh : 0f;
        StringBuilder sb = new StringBuilder();
        sb.Append("   [P74] marca (todos los golpes directos del Capitan): golpes al marcado " + mh + ", dano extra total " + mx + " (" + (mx / (float)n).ToString("0.0") + "/combate)");
        sb.Append(" | Disciplina: turnos con DEF reforzada " + bbt + " de " + bt + " turnos del Capitan, impactos recibidos con ella " + bh + " (ahorro estimado por DEF = " + bsv + " HP; impacto medio sin ella " + avgHit.ToString("0.0") + ")\n");
        sb.Append("   [P74] oportunidades (disponible -> elegida): ");
        foreach (var kv in ready) sb.Append(Short(kv.Key) + " " + kv.Value + "->" + (used.ContainsKey(kv.Key) ? used[kv.Key] : 0) + "; ");
        return sb.ToString().TrimEnd() + "\n";
    }

    // Muestreo de decisiones del Capitan en la escena cargada (no juega el combate).
    // hp: vida del Capitan (solo baja); fireCd/ultCd: dejar en recarga esas habilidades.
    public static string SampleDecisions(string label, int n, int hp, bool fireCd, bool ultCd)
    {
        TurnManager t = Object.FindAnyObjectByType<TurnManager>();
        CombatHudController h = Object.FindAnyObjectByType<CombatHudController>();
        BattleUnit b = null;
        List<BattleUnit> hs = new List<BattleUnit>();

        foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
        {
            if (!u.gameObject.activeInHierarchy) continue;
            if (u.Side == TeamSide.Enemy) b = u; else hs.Add(u);
        }

        boss = b;
        EnemyAIController.ActionPerformed -= OnAct;
        EnemyAIController.ActionPerformed += OnAct;
        int g = 0;
        while (t.CurrentUnit != b && g++ < 20) t.EndTurn();
        if (b.CurrentHealth > hp) b.TakeDamage(b.CurrentHealth - hp);

        AbilityCooldownController cd = b.GetComponent<AbilityCooldownController>();
        EnemyAIController ai = b.GetComponent<EnemyAIController>();
        AbilityData fire = ai.Archetype.specialAbilities.Find(a => a.requiresPreparation);
        AbilityData ult = ai.Archetype.specialAbilities.Find(a => a.abilityName.StartsWith("Última"));
        Dictionary<string, int> counts = new Dictionary<string, int>();

        for (int i = 0; i < n; i++)
        {
            PreparedAbilityController pc = b.GetComponent<PreparedAbilityController>();
            if (pc != null) pc.Clear();
            cd.ResetAll();
            if (fireCd) cd.StartCooldown(fire);
            if (ultCd) cd.StartCooldown(ult);

            foreach (BattleUnit u in hs)
            {
                u.Heal(9999);
                u.GetComponent<StatusEffectController>().ClearAllEffects();
            }

            b.GetComponent<StatusEffectController>().ClearAllEffects();
            lastAbility = null;
            ai.PerformTurn();
            pc = b.GetComponent<PreparedAbilityController>();
            string key = pc != null && pc.HasPreparation ? "Fuego" : (lastAbility != null ? Short(lastAbility.abilityName) : "pasa");
            counts[key] = (counts.ContainsKey(key) ? counts[key] : 0) + 1;
        }

        StringBuilder sb = new StringBuilder(label + " (HP " + b.CurrentHealth + "/" + b.MaxHealth + ", n=" + n + "): ");
        foreach (var kv in counts) sb.Append(kv.Key + " " + kv.Value + " | ");
        return sb.ToString();
    }

    public static void Save(string path)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(Summary());
        sb.AppendLine("--- combates ---");
        foreach (Result r in results) sb.AppendLine(Line(r) + " | HP final " + r.hpEnd + " | HP minimo " + r.minHp);
        File.WriteAllText(path, sb.ToString());
    }
}
#endif
