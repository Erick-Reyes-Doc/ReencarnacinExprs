#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Herramienta de DESARROLLO (solo editor). Prueba individual de las habilidades de los protagonistas con unidades y
// turnos REALES de la escena de combate (los enemigos se aturden para que no interfieran):
//  - Parte 1: tabla de daño/curacion efectivos por nivel y reliquias (el % usa el daño base del nivel).
//  - Parte 2: escenario de Elaina/Evanora/Noelle liberada/Valerian: Marca, Quemadura, Ruptura, Meteorito, Guardia,
//    Amparo, Bendicion, Plegaria, Tiro Evasivo, cooldowns y duraciones.
//  - Parte 3: escenario de Noelle enmascarada (Auxilio, Resistencia, Quebrantador).
// Uso (Play Mode, desde el mapa): AbilityRulesTest.Begin(rutaSalida).
public static class AbilityRulesTest
{
    static readonly string[] HeroIds = { "PlayerArquera", "PlayerEvanora", "PlayerNoelle", "PlayerCaballero" };

    class Step
    {
        public string hero;
        public Func<string> fn;     // devuelve null si todo bien, o el motivo del fallo; null = pasar turno
        public string label;
    }

    static readonly StringBuilder log = new StringBuilder();
    static readonly List<Step> steps = new List<Step>();
    static readonly List<EnemyArchetypeData> archetypes = new List<EnemyArchetypeData>();
    static readonly List<float> delays = new List<float>();

    static string outPath;
    static bool active;
    static int phase;        // 0 inicio, 1 esperando escena, 2 ejecutando script, 3 siguiente
    static int part;
    static int wait;
    static int stepIndex;
    static int lastKey;
    static int guard;
    static int fails, checks;
    static int errorLogs;

    static int meteorHits, meteorDodges, meteorTargets;

    static void OnLog(string m, string st, LogType t)
    {
        if (t == LogType.Log)
        {
            if (m.StartsWith("Golpe de Meteorito causa "))
                meteorHits++;
            else if (m.Contains("esquiva Golpe de Meteorito"))
                meteorDodges++;
            else if (m.Contains(" lanza Golpe de Meteorito sobre "))
            {
                string[] parts = m.Split((char)32);
                int idx = System.Array.IndexOf(parts, "sobre");
                int n;

                if (idx >= 0 && idx + 1 < parts.Length && int.TryParse(parts[idx + 1], out n))
                    meteorTargets = n;
            }
        }

        if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert)
        {
            errorLogs++;
            log.AppendLine("   [ERROR DE CONSOLA] " + m.Split((char)10)[0]);
        }
    }
    static TurnManager tm;
    static AbilitySystem abilities;
    static PositionManager positions;

    public static bool Done => !active;

    public static void Begin(string path)
    {
        outPath = path;
        log.Length = 0;
        fails = 0;
        checks = 0;
        part = 0;
        phase = 0;
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

        errorLogs = 0;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        Application.runInBackground = true;
        active = true;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
    }

    static void Finish(string status)
    {
        EditorApplication.update -= Update;
        Application.logMessageReceived -= OnLog;

        for (int i = 0; i < archetypes.Count; i++)
            archetypes[i].actionDelay = delays[i];

        active = false;
        log.AppendLine();
        log.AppendLine("Errores de consola durante la prueba: " + errorLogs);
        log.AppendLine("Comprobaciones: " + checks + " | fallidas: " + fails + " | " + status);
        System.IO.File.WriteAllText(outPath, log.ToString() + "\nfin\n");
    }

    static void Check(bool ok, string text)
    {
        checks++;

        if (!ok)
            fails++;

        log.AppendLine((ok ? "[OK]   " : "[FALLA] ") + text);
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
        catch (Exception e)
        {
            log.AppendLine("ERROR: " + e);
            Finish("error");
        }
    }

    // ------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------

    static BattleUnit Hero(string id)
    {
        foreach (BattleUnit u in UnityEngine.Object.FindObjectsByType<BattleUnit>())
            if (u.Side == TeamSide.Player && u.gameObject.name == id)
                return u;

        return null;
    }

    static BattleUnit Enemy(string nameContains)
    {
        foreach (BattleUnit u in UnityEngine.Object.FindObjectsByType<BattleUnit>())
            if (u.Side == TeamSide.Enemy && u.IsAlive && u.Data.unitName.ToLowerInvariant().Contains(nameContains))
                return u;

        return null;
    }

    // Primer enemigo vivo en P1-P2 (alcance de Valerian).
    static BattleUnit FirstInReach()
    {
        for (int p = 1; p <= 2; p++)
        {
            BattleUnit u = positions.GetUnitAtPosition(TeamSide.Enemy, p);

            if (u != null && u.IsAlive)
                return u;
        }

        return null;
    }

    static AbilityData Ability(BattleUnit hero, string assetName)
    {
        foreach (AbilityData a in hero.GetComponent<CharacterAbilityLoadout>().Abilities)
            if (a != null && a.name == assetName)
                return a;

        return null;
    }

    static StatusEffectInstance Fx(BattleUnit u, StatusEffectType t)
    {
        StatusEffectController c = u.GetComponent<StatusEffectController>();
        return c != null ? c.GetEffect(t) : null;
    }

    static void SetUp(int level, bool noelleFree, string encounter, string relicSpec)
    {
        GameSession.StartNewGame();
        RelicInventory.Clear();

        List<GameSession.PartyMemberState> party = new List<GameSession.PartyMemberState>();

        foreach (string id in HeroIds)
            party.Add(new GameSession.PartyMemberState
            {
                id = id, level = level, experience = 0, health = 9999, maxHealth = 9999,
                transformed = id == "PlayerNoelle" && noelleFree
            });

        GameSession.SavePartySnapshot(party);

        if (!string.IsNullOrEmpty(relicSpec))
        {
            string keys = "EANV";
            string[] ids = { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" };

            foreach (string part in relicSpec.Split(';'))
            {
                string[] kv = part.Split('=');
                string[] types = kv[1].Split('|');

                for (int s = 0; s < types.Length; s++)
                {
                    int uid = RelicInventory.Add(types[s]);
                    string reason;
                    RelicInventory.TryEquip(uid, ids[keys.IndexOf(kv[0][0])], s, out reason);
                }
            }
        }

        EncounterLoader.DevEncounter = AssetDatabase.LoadAssetAtPath<EncounterData>("Assets/ScriptableObjects/Encounters/" + encounter + ".asset");
        Time.timeScale = 1f;
        phase = 1;
        wait = 8;
        SceneManager.LoadScene("CombateIntegracion");
    }

    // ------------------------------------------------------------
    // Parte 1: tabla de daño y curacion efectivos
    // ------------------------------------------------------------

    static string Row(BattleUnit hero, AbilityData a)
    {
        if (a.abilityType == AbilityType.Heal)
        {
            string heal = a.healMax > 0 ? a.healMin + "-" + a.healMax + " PV" : a.healPercentMaxHealth + " % de la vida maxima";
            return "      " + a.abilityName.PadRight(22) + " cd " + a.cooldown + " | cura " + heal;
        }

        if (a.abilityType != AbilityType.Attack || !a.dealsDamage)
            return "      " + a.abilityName.PadRight(22) + " cd " + a.cooldown + " | apoyo (" + a.description + ")";

        int lo = Mathf.Max(1, Mathf.RoundToInt(hero.DamageMin * a.damagePercent / 100f) + a.basePower);
        int hi = Mathf.Max(1, Mathf.RoundToInt(hero.DamageMax * a.damagePercent / 100f) + a.basePower);
        float mean = hero.AverageDamage * a.damagePercent / 100f + a.basePower;
        return "      " + a.abilityName.PadRight(22) + " cd " + a.cooldown + " | " + a.damagePercent + " % -> " + lo + "-" + hi + " (media " + mean.ToString("0.0") + ")";
    }

    static void DamageTable()
    {
        log.AppendLine("PARTE 1: dano y curacion efectivos (antes de proteccion y critico)");
        int[] levels = { 1, 3, 4, 5 };
        string relics = "E=colmillo_lobo|amuleto_vital;A=colmillo_lobo|amuleto_vital;N=amuleto_vital|anillo_hierro;V=anillo_hierro|amuleto_vital";

        for (int pass = 0; pass < 5; pass++)
        {
            int level = pass < 4 ? levels[pass] : 5;
            log.AppendLine("  == Nivel " + level + (pass == 4 ? " con reliquias (Colmillo +1 dano en Evanora y Elaina)" : ""));

            if (pass == 4)
            {
                GameSession.SetInCombat(false);   // equipar reliquias no se permite en combate
                RelicInventory.Clear();
                string[] ids = { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" };
                string[] a1 = { "colmillo_lobo", "colmillo_lobo", "amuleto_vital", "anillo_hierro" };
                string[] a2 = { "amuleto_vital", "amuleto_vital", "anillo_hierro", "amuleto_vital" };

                for (int h = 0; h < 4; h++)
                {
                    string r;
                    RelicInventory.TryEquip(RelicInventory.Add(a1[h]), ids[h], 0, out r);
                    RelicInventory.TryEquip(RelicInventory.Add(a2[h]), ids[h], 1, out r);
                }
                GameSession.SetInCombat(true);
            }

            foreach (string id in HeroIds)
            {
                BattleUnit hero = Hero(id);
                CharacterProgression p = hero.GetComponent<CharacterProgression>();
                p.SetProgress(level, 0);
                CharacterAbilityLoadout loadout = hero.GetComponent<CharacterAbilityLoadout>();

                bool[] versions = id == "PlayerNoelle" ? new[] { false, true } : new[] { false };

                foreach (bool transformed in versions)
                {
                    if (id == "PlayerNoelle")
                        loadout.SetTransformed(transformed);

                    hero.InitializeUnit();
                    log.AppendLine("    " + id.Replace("Player", "") + (id == "PlayerNoelle" ? (transformed ? " (sin mascara)" : " (enmascarada)") : "") +
                        ": " + hero.MaxHealth + " PV, dano base " + hero.DamageMin + "-" + hero.DamageMax + ", prot " + Mathf.RoundToInt(hero.ProtectionPercent) + " %, evasion " + Mathf.RoundToInt(hero.EvasionPercent) + " %");

                    foreach (AbilityData a in loadout.Abilities)
                        if (a != null)
                            log.AppendLine(Row(hero, a));
                }

                if (id == "PlayerNoelle")
                    loadout.SetTransformed(false);
            }
        }

        // Plegaria vs 30 %: valor fijo frente a vida maxima de cada heroe.
        log.AppendLine("  == Plegaria Focalizada (7-9 PV) frente a la curacion anterior del 30 %");

        foreach (int level in levels)
        {
            RelicInventory.Clear();
            string line = "    Nivel " + level + ": ";

            foreach (string id in HeroIds)
            {
                BattleUnit hero = Hero(id);
                hero.GetComponent<CharacterProgression>().SetProgress(level, 0);
                hero.InitializeUnit();
                int old = Mathf.RoundToInt(hero.MaxHealth * 0.30f);
                line += id.Replace("Player", "") + " " + hero.MaxHealth + " PV (30 % = " + old + " | 7-9 = " + Mathf.RoundToInt(800f / hero.MaxHealth) + " % de su vida) ; ";
            }

            log.AppendLine(line);
        }

        RelicInventory.Clear();
        log.AppendLine();
    }

    // ------------------------------------------------------------
    // Script de turnos
    // ------------------------------------------------------------

    static void Add(string hero, string label, Func<string> fn)
    {
        steps.Add(new Step { hero = hero, label = label, fn = fn });
    }

    static void Round(string r1, Func<string> a, string r2, Func<string> b, string r3, Func<string> c, string r4, Func<string> d)
    {
        // Orden de los heroes por velocidad: Elaina, Evanora, Noelle, Valerian.
        Add("PlayerArquera", r1, a);
        Add("PlayerEvanora", r2, b);
        Add("PlayerNoelle", r3, c);
        Add("PlayerCaballero", r4, d);
    }

    static string Run(Func<bool> action, string failure)
    {
        return action() ? null : failure;
    }

    static string Expect(bool ok, string text)
    {
        Check(ok, text);
        return null;
    }

    // Tasa de aturdimiento: repite Embate sobre el mismo objetivo (recargas y vida reiniciadas) y cuenta cuantas veces queda aturdido.
    static string StunRate(BattleUnit user, BattleUnit target, string abilityAsset, float expected, string label)
    {
        AbilityData ability = Ability(user, abilityAsset);
        AbilityCooldownController cooldowns = user.GetComponent<AbilityCooldownController>();

        if (cooldowns == null)
            cooldowns = user.gameObject.AddComponent<AbilityCooldownController>();

        StatusEffectController fx = target.GetComponent<StatusEffectController>();
        const int trials = 400;
        int stuns = 0, ran = 0;

        for (int i = 0; i < trials; i++)
        {
            cooldowns.ResetAll();
            fx.RemoveEffect(StatusEffectType.Stun);
            target.Heal(9999);

            if (!abilities.ExecuteAbility(user, target, ability))
                continue;

            ran++;

            if (fx.HasEffect(StatusEffectType.Stun))
                stuns++;
        }

        float rate = ran > 0 ? 100f * stuns / ran : 0f;
        bool ok = expected <= 0f ? stuns == 0 : Mathf.Abs(rate - expected) <= 8f;
        Check(ok && ran > 0, label + ": " + stuns + " aturdimientos en " + ran + " usos = " + rate.ToString("0.0") + " % (esperado " + expected + " %)");
        cooldowns.ResetAll();
        fx.RemoveEffect(StatusEffectType.Stun);
        return null;
    }

    static void BuildScenarioUnmasked()
    {
        steps.Clear();
        stepIndex = 0;

        int[] hpBefore = new int[8];
        BattleUnit[] marked = new BattleUnit[1];
        float[] baseProt = new float[1];

        // ---- Ronda 1 ----
        Round(
            "Elaina: Marca del Cazador sobre el Bruto", () =>
            {
                BattleUnit bruto = Enemy("bruto");
                bool ok = abilities.ExecuteAbility(Hero("PlayerArquera"), bruto, Ability(Hero("PlayerArquera"), "Elaina_MarcaDelCazador"));
                StatusEffectInstance mark = Fx(bruto, StatusEffectType.Mark);
                Expect(ok && mark != null && mark.RemainingTurns == 3, "Marca aplicada al Bruto con duracion 3 (turnos restantes " + (mark != null ? mark.RemainingTurns : -1) + ")");
                bool again = abilities.ExecuteAbility(Hero("PlayerArquera"), bruto, Ability(Hero("PlayerArquera"), "Elaina_MarcaDelCazador"));
                Expect(!again, "la Marca no se puede reaplicar mientras esta activa ni antes de su recarga (cd 2)");
                bool direct = bruto.GetComponent<StatusEffectController>().ApplyEffect(StatusEffectType.Mark, 0, 3, Hero("PlayerArquera"));
                Expect(!direct, "ni siquiera aplicandola directamente: no se acumula");
                return null;
            },
            "Evanora: Llamarada sobre el Bruto", () =>
            {
                BattleUnit bruto = Enemy("bruto");
                hpBefore[0] = bruto.CurrentHealth;
                AbilityData llamarada = Ability(Hero("PlayerEvanora"), "Evanora_Llamarada");
                bool ok = abilities.ExecuteAbility(Hero("PlayerEvanora"), bruto, llamarada);
                StatusEffectInstance burn = Fx(bruto, StatusEffectType.Burn);
                Expect(ok && llamarada.damagePercent == 85, "Llamarada ejecutada al 85 % (dano " + (hpBefore[0] - bruto.CurrentHealth) + ")");
                Expect(burn != null && burn.Potency == 2 && burn.RemainingTurns == 2, "Quemadura 2 PV x 2 turnos (potencia " + (burn != null ? burn.Potency : -1) + ", turnos " + (burn != null ? burn.RemainingTurns : -1) + ")");
                return null;
            },
            "Noelle: Amparo Divino (todo el equipo)", () =>
            {
                BattleUnit noelle = Hero("PlayerNoelle");
                baseProt[0] = Hero("PlayerCaballero").ProtectionPercent;
                bool ok = abilities.ExecuteGroupRegeneration(noelle, Ability(noelle, "NoelleUnmasked_AmparoDivino"));
                bool all = true;

                foreach (string id in HeroIds)
                {
                    StatusEffectInstance boost = Fx(Hero(id), StatusEffectType.DefenseBoost);
                    all &= boost != null && boost.Potency == 15 && boost.RemainingTurns == 2;
                }

                Expect(ok && all, "Amparo Divino: los 4 aliados con +15 % de proteccion durante 2 turnos");
                Expect(!abilities.ExecuteGroupRegeneration(noelle, Ability(noelle, "NoelleUnmasked_AmparoDivino")), "Amparo Divino no se acumula ni se repite mientras esta en recarga (cd 3)");
                return null;
            },
            "Valerian: Guardia de Trinchera sobre Elaina", () =>
            {
                BattleUnit v = Hero("PlayerCaballero");
                BattleUnit arq = Hero("PlayerArquera");
                bool ok = abilities.ExecuteAbility(v, arq, Ability(v, "Valerian_GuardiaDeTrinchera"));
                StatusEffectInstance guard = Fx(v, StatusEffectType.Guard);
                StatusEffectInstance boost = Fx(v, StatusEffectType.DefenseBoost);
                Expect(ok && guard != null && guard.RemainingTurns == 2 && guard.ProtectedAlly == arq && guard.InterceptPercent == 50 && guard.Potency == 0,
                    "Guardia: protege a Elaina 2 turnos e reparte el 50 %, sin reduccion multiplicativa propia (potencia Guard " + (guard != null ? guard.Potency : -1) + ")");
                Expect(boost != null && boost.Potency == 15 && boost.RemainingTurns == 2, "Valerian recibe +15 puntos de PROT (DefenseBoost) durante 2 turnos");
                Expect(Mathf.Approximately(v.ProtectionPercent, baseProt[0] + 15f), "su PROT es " + v.ProtectionPercent + " % = base " + baseProt[0] + " + 15 (el Amparo de Noelle, +15, NO se acumula: gana el mayor)");

                // Reparto exacto: un golpe de 10 (sin PROT) se divide 5/5 y cada uno aplica su PROT (aliado con Amparo +15 = 15 %, Valerian 30 %).
                int arqBefore = arq.CurrentHealth;
                int vBefore = v.CurrentHealth;
                int expectedAlly = DamageCalculator.ApplyProtection(5, arq.ProtectionPercent);
                int expectedV = DamageCalculator.ApplyProtection(5, v.ProtectionPercent);
                int toTargetPart;
                abilities.DeliverDirectDamage(null, arq, 10, 0f, out toTargetPart);
                Expect(arqBefore - arq.CurrentHealth == expectedAlly && vBefore - v.CurrentHealth == expectedV,
                    "reparto 50/50 con PROT propia: Elaina pierde " + (arqBefore - arq.CurrentHealth) + " (esperado " + expectedAlly + ") y Valerian " + (vBefore - v.CurrentHealth) + " (esperado " + expectedV + ") de un golpe de 10");
                arq.Heal(20);
                v.Heal(20);
                v.TakeDamage(Mathf.Max(0, v.CurrentHealth - 12));
                return null;
            });

        // ---- Ronda 2 ----
        Round(
            "Elaina: Disparo Certero sobre el Bruto marcado", () =>
            {
                BattleUnit bruto = Enemy("bruto");
                hpBefore[1] = bruto.CurrentHealth;
                bool ok = abilities.ExecuteAbility(Hero("PlayerArquera"), bruto, Ability(Hero("PlayerArquera"), "Elaina_DisparoCertero"));
                Expect(ok, "Disparo Certero ejecutado desde P" + Hero("PlayerArquera").Position + " (dano al marcado " + (hpBefore[1] - bruto.CurrentHealth) + ")");
                Expect(Fx(bruto, StatusEffectType.Mark) == null, "la Marca se consumio con ese ataque directo");
                BattleUnit v = Hero("PlayerCaballero");
                Expect(Mathf.Approximately(EvasionController.ChanceFor(v, AttackWeight.Light, 50f), v.EvasionPercent * 0.5f), "ignora el 50 % de la evasion (" + v.EvasionPercent + " % -> " + EvasionController.ChanceFor(v, AttackWeight.Light, 50f) + " %)");
                return null;
            },
            "Evanora: Ruptura Ignea sobre el Bruto con Quemadura", () =>
            {
                BattleUnit bruto = Enemy("bruto");
                AbilityData ruptura = Ability(Hero("PlayerEvanora"), "Evanora_SelloDeRuptura");
                float multBefore = ruptura.DamageMultiplierAgainst(bruto);
                hpBefore[2] = bruto.CurrentHealth;
                bool ok = abilities.ExecuteAbility(Hero("PlayerEvanora"), bruto, ruptura);
                Expect(ok && Mathf.Approximately(multBefore, 1.5f), "Ruptura Ignea con Quemadura: multiplicador x" + multBefore + " (150 %), dano " + (hpBefore[2] - bruto.CurrentHealth));
                Expect(Fx(bruto, StatusEffectType.Burn) == null && Mathf.Approximately(ruptura.DamageMultiplierAgainst(bruto), 1f), "la Quemadura se consumio (el siguiente golpe ya no tiene bono)");
                return null;
            },
            "Noelle: Bendicion Continua (todo el equipo)", () =>
            {
                BattleUnit noelle = Hero("PlayerNoelle");
                bool ok = abilities.ExecuteGroupRegeneration(noelle, Ability(noelle, "NoelleUnmasked_BendicionContinua"));
                bool all = true;

                foreach (string id in HeroIds)
                {
                    BattleUnit h = Hero(id);
                    RegenerationEffect r = h.GetComponent<RegenerationEffect>();
                    int expected = Mathf.Max(1, Mathf.RoundToInt(h.MaxHealth * 0.15f));   // 15 % de la vida maxima de cada aliado
                    all &= r != null && r.HealPerTurn == expected && r.RemainingTurns == 2;
                }

                Expect(ok && all, "Bendicion Continua: 15 % de la vida maxima por turno durante 2 turnos a los 4 aliados");
                return null;
            },
            "Valerian: Embate de Escudo sobre el Bruto (aturdimiento)", () =>
            {
                BattleUnit v = Hero("PlayerCaballero");
                int pos = v.Position;
                AbilityData embate = Ability(v, "Valerian_GolpeBajo");
                bool ok = abilities.ExecuteAbility(v, Enemy("bruto") ?? FirstInReach(), embate);
                Expect(ok && embate.damagePercent == 90, "Embate de Escudo al 90 % ejecutado desde P" + pos + " (el avance es opcional: posicion final P" + v.Position + ")");
                BattleUnit target = FirstInReach();
                StunRate(v, target, "Valerian_GolpeBajo", 30f, "Embate de Escudo vs enemigo normal");
                return null;
            });

        // ---- Ronda 3 ----
        Round(
            "Elaina: Tiro Evasivo sobre el Goblin", () =>
            {
                BattleUnit a = Hero("PlayerArquera");
                int pos = a.Position;
                float baseEva = a.EvasionPercent;
                AbilityData tiro = Ability(a, "Elaina_TiroEvasivo");
                bool ok = abilities.ExecuteAbility(a, Enemy("goblin com") ?? Enemy("goblin"), tiro);
                StatusEffectInstance boost = Fx(a, StatusEffectType.EvasionBoost);
                Expect(ok && tiro.damagePercent == 80 && a.Position == pos + 1, "Tiro Evasivo al 80 %: retrocede una posicion (P" + pos + " -> P" + a.Position + ")");
                Expect(boost != null && boost.Potency == 15 && a.EvasionPercent == Mathf.Min(40f, baseEva + 15f), "gana +15 de evasion (" + baseEva + " % -> " + a.EvasionPercent + " %)");
                return null;
            },
            "Evanora: prepara Golpe de Meteorito", () =>
            {
                BattleUnit e = Hero("PlayerEvanora");
                int alive = 0;

                foreach (BattleUnit u in UnityEngine.Object.FindObjectsByType<BattleUnit>())
                    if (u.Side == TeamSide.Enemy && u.IsAlive)
                        alive++;

                bool ok = abilities.ExecuteAreaAbility(e, Ability(e, "Evanora_GolpeDeMeteorito"));
                PreparedAbilityController p = e.GetComponent<PreparedAbilityController>();
                Expect(ok && p != null && p.HasPreparation, "Golpe de Meteorito queda preparado (consume el turno) con " + alive + " enemigos amenazados");
                return null;
            },
            "Noelle: Plegaria Focalizada sobre Valerian", () =>
            {
                BattleUnit noelle = Hero("PlayerNoelle");
                BattleUnit v = Hero("PlayerCaballero");
                int before = v.CurrentHealth;
                bool ok = abilities.ExecuteAbility(noelle, v, Ability(noelle, "NoelleUnmasked_PlegariaFocalizada"));
                int healed = v.CurrentHealth - before;
                Expect(ok && healed >= 7 && healed <= 9, "Plegaria Focalizada cura entre 7 y 9 (curo " + healed + ")");
                return null;
            },
            "Valerian: Herida Profunda", () =>
            {
                BattleUnit v = Hero("PlayerCaballero");
                BattleUnit target = Enemy("bruto") ?? FirstInReach();
                AbilityData herida = Ability(v, "Valerian_HeridaProfunda");
                bool ok = abilities.ExecuteAbility(v, target, herida);
                StatusEffectInstance bleed = Fx(target, StatusEffectType.Bleeding);
                Expect(ok && herida.damagePercent == 85 && bleed != null && bleed.Potency == 2 && bleed.RemainingTurns == 2, "Herida Profunda al 85 %: Sangrado 2 PV x 2 turnos (potencia " + (bleed != null ? bleed.Potency : -1) + ", turnos " + (bleed != null ? bleed.RemainingTurns : -1) + ")");
                return null;
            });

        // ---- Ronda 4: Marca + ataque de area; recarga de Tiro Evasivo ----
        Round(
            "Elaina: marca a un enemigo antes del Meteorito; Tiro Evasivo en recarga", () =>
            {
                BattleUnit a = Hero("PlayerArquera");
                Expect(Fx(a, StatusEffectType.EvasionBoost) == null, "la evasion extra termino al empezar el siguiente turno de Elaina");
                Expect(!abilities.ExecuteAbility(a, Enemy("goblin") ?? Enemy("arquero"), Ability(a, "Elaina_TiroEvasivo")), "Tiro Evasivo no esta listo en la ronda siguiente (cd 1) y ademas P" + a.Position + " no es una posicion valida");

                // Elaina vuelve a P3 (mover una posicion al frente) y marca al enemigo vivo mas cercano.
                positions.MoveUnit(a, -1);
                BattleUnit target = null;

                for (int p = 1; p <= 4 && target == null; p++)
                {
                    BattleUnit u = positions.GetUnitAtPosition(TeamSide.Enemy, p);

                    if (u != null && u.IsAlive)
                        target = u;
                }

                marked[0] = target;
                bool ok = target != null && abilities.ExecuteAbility(a, target, Ability(a, "Elaina_MarcaDelCazador"));
                Expect(ok && Fx(target, StatusEffectType.Mark) != null, "Marca del Cazador lista de nuevo (cd 2) y aplicada a " + (target != null ? target.Data.unitName : "nadie") + " desde P" + a.Position);
                return null;
            },
            "Evanora: el Meteorito impacta (ataque de area) sobre el enemigo marcado", () =>
            {
                Expect(meteorTargets > 0 && meteorHits + meteorDodges == meteorTargets, "el Meteorito alcanzo a sus " + meteorTargets + " objetivos: " + meteorHits + " impactos + " + meteorDodges + " esquivas (cada objetivo tira su propia evasion)");
                Expect(marked[0] == null || !marked[0].IsAlive || Fx(marked[0], StatusEffectType.Mark) == null, "el ataque de area consumio la Marca del enemigo marcado");
                return null;
            },
            null, null,
            null, null);

        // ---- Ronda 5: Tiro Evasivo listo; evasion sin acumular ----
        Round(
            "Elaina: Tiro Evasivo listo; la evasion no se acumula y respeta el tope", () =>
            {
                BattleUnit a = Hero("PlayerArquera");
                float baseEva = a.EvasionPercent;
                bool ok = abilities.ExecuteAbility(a, Enemy("goblin") ?? Enemy("arquero") ?? FirstInReach(), Ability(a, "Elaina_TiroEvasivo"));
                Expect(ok, "Tiro Evasivo listo tras 1 turno de recarga y usado desde una posicion valida");
                StatusEffectController fx = a.GetComponent<StatusEffectController>();
                fx.ApplyEffect(StatusEffectType.EvasionBoost, 15, 1, a);
                Expect(Mathf.Approximately(a.EvasionPercent, baseEva + 15f), "reaplicar +15 de evasion no la acumula (" + baseEva + " % -> " + a.EvasionPercent + " %)");
                fx.ApplyEffect(StatusEffectType.EvasionBoost, 60, 1, a);
                Expect(Mathf.Approximately(a.EvasionPercent, BattleUnit.MaxEvasionPercent), "la evasion respeta el tope de " + BattleUnit.MaxEvasionPercent + " % (" + a.EvasionPercent + " %)");
                return null;
            },
            null, null,
            "Noelle: Amparo Divino listo tras 3 turnos", () =>
            {
                BattleUnit noelle = Hero("PlayerNoelle");
                bool ok = abilities.ExecuteGroupRegeneration(noelle, Ability(noelle, "NoelleUnmasked_AmparoDivino"));
                Expect(ok, "Amparo Divino vuelve a estar listo tras su recarga de 3 turnos");
                return null;
            },
            null, null);

        // ---- Ronda 6: posiciones no permitidas ----
        Round(
            "Elaina: Disparo Certero desde P1 (no permitido)", () =>
            {
                BattleUnit a = Hero("PlayerArquera");
                while (a.Position > 1 && positions.MoveUnit(a, -1)) { }
                bool ok = abilities.ExecuteAbility(a, Enemy("goblin") ?? FirstInReach(), Ability(a, "Elaina_DisparoCertero"));
                Expect(a.Position == 1 && !ok, "Elaina en P" + a.Position + " no puede usar Disparo Certero (solo P2-P4)");
                return null;
            },
            null, null,
            null, null,
            "Valerian: Corte del Bastion desde P3 (no permitido)", () =>
            {
                BattleUnit v = Hero("PlayerCaballero");
                while (v.Position < 3 && positions.MoveUnit(v, 1)) { }
                bool ok = abilities.ExecuteAbility(v, FirstInReach(), Ability(v, "Valerian_CortePreciso"));
                Expect(v.Position >= 3 && !ok, "Valerian en P" + v.Position + " no puede usar Corte del Bastion (solo P1-P2)");
                return null;
            });
    }

    // Auditoria de Guardia de Trinchera con la formula adoptada: el golpe (sin PROT) se reparte 50/50 entre el aliado y
    // Valerian y cada uno aplica su propia PROT (con sus topes) a su parte. Usa el codigo real (DeliverDirectDamage).
    static void GuardAudit()
    {
        log.AppendLine("PARTE 5: Guardia de Trinchera con la formula de reparto (codigo real, DeliverDirectDamage)");
        BattleUnit v = Hero("PlayerCaballero");
        string[] allies = { "PlayerArquera", "PlayerNoelle" };

        foreach (string scenario in new[] { "sin Amparo", "con Amparo +15 en todos", "PROT de Valerian forzada a 60 (tope 50)" })
        {
            foreach (string id in allies)
            {
                foreach (int raw in new[] { 10, 14, 7 })
                {
                    foreach (string h in HeroIds)
                    {
                        BattleUnit u = Hero(h);
                        u.GetComponent<StatusEffectController>().ClearAllEffects();
                        u.Heal(9999);
                    }

                    BattleUnit ally = Hero(id);
                    StatusEffectController vfx = v.GetComponent<StatusEffectController>();
                    vfx.ApplyGuard(0, 2, v, ally, 50);
                    vfx.ApplyEffect(StatusEffectType.DefenseBoost, 15, 2, v);

                    if (scenario.StartsWith("con Amparo"))
                        foreach (string h in HeroIds)
                            Hero(h).GetComponent<StatusEffectController>().ApplyEffect(StatusEffectType.DefenseBoost, 15, 2, Hero("PlayerNoelle"));

                    if (scenario.StartsWith("PROT de Valerian"))
                        vfx.ApplyEffect(StatusEffectType.DefenseBoost, 60, 2, v);

                    float allyProt = ally.ProtectionPercent;
                    float vProt = v.ProtectionPercent;
                    int toV = Mathf.RoundToInt(raw * 0.5f);
                    int toAlly = raw - toV;
                    int expAlly = DamageCalculator.ApplyProtection(toAlly, allyProt);
                    int expV = DamageCalculator.ApplyProtection(toV, vProt);
                    int noGuard = DamageCalculator.ApplyProtection(raw, allyProt);

                    int allyBefore = ally.CurrentHealth;
                    int vBefore = v.CurrentHealth;
                    int toTarget;
                    int total = abilities.DeliverDirectDamage(null, ally, raw, 0f, out toTarget);
                    int allyLost = allyBefore - ally.CurrentHealth;
                    int vLost = vBefore - v.CurrentHealth;

                    log.AppendLine("  [" + scenario + "] " + ally.name.Replace("Player", "") + " PROT " + allyProt + " %, Valerian PROT " + vProt + " %, golpe " + raw + ": " +
                        "mitad aliado " + toAlly + " -> " + allyLost + " | mitad Valerian " + toV + " -> " + vLost + " | total " + (allyLost + vLost) + " (sin Guardia: " + noGuard + ")");
                    Check(allyLost == expAlly && vLost == expV && total == allyLost + vLost,
                        "reparto exacto con PROT propia: aliado " + allyLost + " (esperado " + expAlly + "), Valerian " + vLost + " (esperado " + expV + ")");
                    Check(vProt <= BattleUnit.MaxProtectionPercent && allyProt <= BattleUnit.MaxProtectionPercent, "las PROT respetan el tope de " + BattleUnit.MaxProtectionPercent + " % (" + allyProt + " / " + vProt + ")");
                    Check(allyLost + vLost >= Mathf.Min(1, raw) && allyLost + vLost >= Mathf.FloorToInt(raw * 0.5f * (1f - BattleUnit.MaxProtectionPercent / 100f)),
                        "sin reduccion excesiva: el total (" + (allyLost + vLost) + ") nunca baja de la mitad del golpe con el tope de PROT");
                }
            }
        }

        foreach (string h in HeroIds)
        {
            BattleUnit u = Hero(h);
            u.GetComponent<StatusEffectController>().ClearAllEffects();
            u.Heal(9999);
        }
    }

    // Escenario del jefe: inmunidad al aturdimiento, marca del Capitan y marca de Elaina sobre el jefe.
    static void BuildScenarioBoss()
    {
        steps.Clear();
        stepIndex = 0;

        Round(
            "Elaina: Marca del Cazador sobre el Capitan", () =>
            {
                BattleUnit boss = Enemy("capit");
                bool ok = abilities.ExecuteAbility(Hero("PlayerArquera"), boss, Ability(Hero("PlayerArquera"), "Elaina_MarcaDelCazador"));
                Expect(ok && Fx(boss, StatusEffectType.Mark) != null, "la Marca del Cazador funciona sobre el jefe");
                return null;
            },
            null, null,
            null, null,
            "Valerian: Embate de Escudo contra el jefe (inmune)", () =>
            {
                BattleUnit v = Hero("PlayerCaballero");
                BattleUnit boss = Enemy("capit");
                Expect(boss != null && boss.Data.isBoss, "el Capitan esta marcado como jefe");
                StunRate(v, boss, "Valerian_GolpeBajo", 0f, "Embate de Escudo vs Capitan");

                // Objetivo Prioritario del Capitan: +35 % al siguiente ataque directo y se consume.
                BattleUnit target = Hero("PlayerEvanora");
                StatusEffectController fx = target.GetComponent<StatusEffectController>();
                target.Heal(9999);
                fx.ApplyEffect(StatusEffectType.PriorityTarget, 35, 2, boss);
                int before = target.CurrentHealth;
                target.TakeDirectDamage(10, boss);
                int first = before - target.CurrentHealth;
                before = target.CurrentHealth;
                target.TakeDirectDamage(10, boss);
                int second = before - target.CurrentHealth;
                Expect(first == 14 && second == 10 && !fx.HasEffect(StatusEffectType.PriorityTarget), "Objetivo Prioritario: primer golpe de 10 -> " + first + " (+35 %), segundo -> " + second + " (la marca se consumio)");

                // Otro atacante no aprovecha la marca del Capitan.
                target.Heal(9999);
                fx.ApplyEffect(StatusEffectType.PriorityTarget, 35, 2, boss);
                before = target.CurrentHealth;
                target.TakeDirectDamage(10, Hero("PlayerNoelle"));
                Expect(before - target.CurrentHealth == 10 && fx.HasEffect(StatusEffectType.PriorityTarget), "un ataque de otra unidad no activa ni consume la marca del Capitan");
                return null;
            });
    }

    static void BuildScenarioMasked()
    {
        steps.Clear();
        stepIndex = 0;

        Round(
            null, null,
            null, null,
            "Noelle: Auxilio Improvisado sobre Valerian", () =>
            {
                BattleUnit noelle = Hero("PlayerNoelle");
                BattleUnit v = Hero("PlayerCaballero");
                v.TakeDamage(Mathf.Max(0, v.CurrentHealth - 6));
                int before = v.CurrentHealth;
                bool ok = abilities.ExecuteAbility(noelle, v, Ability(noelle, "NoelleMasked_AuxilioImprovisado"));
                int healed = v.CurrentHealth - before;
                Expect(ok && healed >= 4 && healed <= 6, "Auxilio Improvisado cura entre 4 y 6 (curo " + healed + ")");
                Expect(!abilities.ExecuteAbility(noelle, v, Ability(noelle, "NoelleMasked_AuxilioImprovisado")), "Auxilio Improvisado no se repite en el mismo turno (cd 1)");
                return null;
            },
            "Valerian: Corte del Bastion", () =>
            {
                BattleUnit v = Hero("PlayerCaballero");
                AbilityData corte = Ability(v, "Valerian_CortePreciso");
                bool ok = abilities.ExecuteAbility(v, Enemy("goblin"), corte);
                Expect(ok && corte.abilityName == "Corte del Bastión" && corte.damagePercent == 100, "Corte del Bastion (100 %) desde P" + v.Position);
                return null;
            });

        Round(
            null, null,
            null, null,
            "Noelle: Resistencia Obstinada", () =>
            {
                BattleUnit noelle = Hero("PlayerNoelle");
                bool ok = abilities.ExecuteAbility(noelle, noelle, Ability(noelle, "NoelleMasked_ResistenciaObstinada"));
                StatusEffectInstance boost = Fx(noelle, StatusEffectType.DefenseBoost);
                Expect(ok && boost != null && boost.Potency == 20 && boost.RemainingTurns == 2, "Resistencia Obstinada: +20 % de proteccion durante 2 turnos (potencia " + (boost != null ? boost.Potency : -1) + ")");
                return null;
            },
            "Valerian: Impacto con Noelle no aplica", null);

        Round(
            null, null,
            null, null,
            "Noelle: Auxilio Improvisado otra vez (cd 1: listo tras un turno)", () =>
            {
                BattleUnit noelle = Hero("PlayerNoelle");
                BattleUnit v = Hero("PlayerCaballero");
                v.TakeDamage(Mathf.Max(0, v.CurrentHealth - 6));
                bool ok = abilities.ExecuteAbility(noelle, v, Ability(noelle, "NoelleMasked_AuxilioImprovisado"));
                Expect(ok, "Auxilio Improvisado vuelve a estar listo dos turnos despues (cd 1 = un turno propio en recarga)");
                return null;
            },
            null, null);
    }

    // ------------------------------------------------------------
    // Maquina de estados
    // ------------------------------------------------------------

    static void Tick()
    {
        if (wait-- > 0)
            return;

        CombatIntroTransition intro = UnityEngine.Object.FindAnyObjectByType<CombatIntroTransition>();

        if (intro != null)
            UnityEngine.Object.Destroy(intro);

        if (phase == 0)
        {
            if (part == 0)
            {
                SetUp(3, true, "Pradera/P4", "");
                return;
            }

            if (part == 1)
            {
                meteorHits = 0;
                meteorDodges = 0;
                meteorTargets = 0;
                log.AppendLine("PARTE 2: habilidades de los protagonistas con turnos reales (nivel 3, Noelle sin mascara, enemigos aturdidos, encuentro P4)");
                SetUp(3, true, "Pradera/P4", "");
                return;
            }

            if (part == 2)
            {
                log.AppendLine();
                log.AppendLine("PARTE 3: Noelle enmascarada (nivel 1, encuentro P1)");
                SetUp(1, false, "Pradera/P1", "");
                return;
            }

            if (part == 3)
            {
                log.AppendLine();
                log.AppendLine("PARTE 4: jefe (nivel 5, encuentro del Capitan): inmunidad al aturdimiento y marcas");
                SetUp(5, true, "Jefe/BOSS_Capitan", "");
                return;
            }

            if (part == 4)
            {
                log.AppendLine();
                SetUp(3, true, "Pradera/P1", "");
                return;
            }

            Finish("completado");
            return;
        }

        tm = UnityEngine.Object.FindAnyObjectByType<TurnManager>();
        abilities = UnityEngine.Object.FindAnyObjectByType<AbilitySystem>();
        positions = UnityEngine.Object.FindAnyObjectByType<PositionManager>();

        if (tm == null || abilities == null || positions == null || Hero("PlayerArquera") == null || Hero("PlayerCaballero") == null)
        {
            wait = 1;
            return;
        }

        if (phase == 1)
        {
            if (part == 0)
            {
                // La parte 1 solo lee estadisticas: no necesita turnos.
                DamageTable();
                part = 1;
                phase = 0;
                wait = 1;
                return;
            }

            if (part == 4)
            {
                GuardAudit();
                part++;
                phase = 0;
                wait = 1;
                return;
            }

            if (part == 1)
                BuildScenarioUnmasked();
            else if (part == 2)
                BuildScenarioMasked();
            else
                BuildScenarioBoss();

            lastKey = -1;
            guard = 0;
            phase = 2;
            return;
        }

        // phase 2: ejecutar el guion en turnos reales.
        if (++guard > 30000 || tm.BattleFinished || stepIndex >= steps.Count)
        {
            if (stepIndex < steps.Count)
                log.AppendLine("   (guion incompleto: " + stepIndex + "/" + steps.Count + " pasos, combate " + (tm.BattleFinished ? "terminado" : "sin avance") + ")");

            part++;
            phase = 0;
            wait = 1;
            return;
        }

        BattleUnit unit = tm.CurrentUnit;

        if (unit == null || unit.Side != TeamSide.Player || !unit.IsAlive)
            return;

        int key = Array.IndexOf(HeroIds, unit.gameObject.name) * 1000 + tm.CurrentRound;

        if (key == lastKey)
            return;

        lastKey = key;

        // Los enemigos no jefes se mantienen aturdidos para que no interfieran.
        foreach (BattleUnit e in UnityEngine.Object.FindObjectsByType<BattleUnit>())
            if (e.Side == TeamSide.Enemy && e.IsAlive && !e.Data.isBoss)
            {
                e.GetComponent<StatusEffectController>().ApplyEffect(StatusEffectType.Stun, 1, 50, null);
                e.Heal(9999);   // los objetivos de prueba no mueren entre un paso y otro del guion
            }

        Step step = steps[stepIndex];

        if (step.hero == unit.gameObject.name)
        {
            stepIndex++;

            if (step.fn != null)
            {
                log.AppendLine("  R" + tm.CurrentRound + " " + step.label);
                string failure = step.fn();

                if (failure != null)
                    Check(false, step.label + ": " + failure);
            }
        }

        if (!tm.BattleFinished)
            tm.EndTurn();
    }
}
#endif
