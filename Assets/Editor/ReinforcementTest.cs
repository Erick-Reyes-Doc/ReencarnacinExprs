#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Herramienta de DESARROLLO (solo editor). Comprueba las reglas del refuerzo experimental del Capitan (BossReinforcements):
// fase 2 con el Errante vivo -> no aparece nada; al caer el Errante aparece UN Acechador; solo una vez; nunca mas de un
// secuaz activo; posicion, orden de turnos, seleccion de objetivos y condiciones de victoria.
// Uso (Play Mode, desde el mapa): ReinforcementTest.Begin(rutaSalida).
public static class ReinforcementTest
{
    static readonly StringBuilder log = new StringBuilder();
    static string outPath;
    static bool active;
    static int step;
    static int wait;
    static int fails, checks;
    static TurnManager tm;
    static BattleUnit boss, errante;
    static BossReinforcements reinforcements;
    static int startRound;
    static readonly List<float> delays = new List<float>();
    static readonly List<EnemyArchetypeData> archetypes = new List<EnemyArchetypeData>();

    public static bool Done => !active;

    public static void Begin(string path)
    {
        outPath = path;
        log.Length = 0;
        fails = 0;
        checks = 0;
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

        for (int i = 0; i < archetypes.Count; i++)
            archetypes[i].actionDelay = delays[i];

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

    static List<BattleUnit> Enemies()
    {
        List<BattleUnit> list = new List<BattleUnit>();

        foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
            if (u.Side == TeamSide.Enemy && u.IsAlive)
                list.Add(u);

        return list;
    }

    static int Minions()
    {
        int n = 0;

        foreach (BattleUnit u in Enemies())
            if (u != boss)
                n++;

        return n;
    }

    static BattleUnit Hero(string id)
    {
        foreach (BattleUnit u in Object.FindObjectsByType<BattleUnit>())
            if (u.Side == TeamSide.Player && u.gameObject.name == id)
                return u;

        return null;
    }

    static void Tick()
    {
        if (wait-- > 0)
            return;

        CombatIntroTransition intro = Object.FindAnyObjectByType<CombatIntroTransition>();

        if (intro != null)
            Object.Destroy(intro);

        switch (step)
        {
            case 0:
            {
                GameSession.StartNewGame();
                RelicInventory.Clear();
                List<GameSession.PartyMemberState> party = new List<GameSession.PartyMemberState>();

                foreach (string id in new[] { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" })
                    party.Add(new GameSession.PartyMemberState { id = id, level = 5, health = 9999, maxHealth = 9999, transformed = id == "PlayerNoelle" });

                GameSession.SavePartySnapshot(party);

                EncounterData encounter = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EncounterData>("Assets/ScriptableObjects/Encounters/Jefe/BOSS_Capitan.asset"));
                encounter.enemies = new List<EncounterData.EnemySlot>
                {
                    new EncounterData.EnemySlot { prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bosses/CapitanExpedicionario.prefab"), position = 1 },
                    new EncounterData.EnemySlot { prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/CorruptoErrante.prefab"), position = 2 }
                };

                EncounterLoader.DevEncounter = encounter;
                Time.timeScale = 1f;
                step = 1;
                wait = 8;
                SceneManager.LoadScene("CombateIntegracion");
                return;
            }

            case 1:
            {
                tm = Object.FindAnyObjectByType<TurnManager>();

                if (tm == null || !tm.BattleActive || Hero("PlayerCaballero") == null)
                {
                    wait = 1;
                    return;
                }

                foreach (BattleUnit u in Enemies())
                {
                    if (u.Data.isBoss)
                        boss = u;
                    else
                        errante = u;
                }

                GameObject holder = new GameObject("RefuerzoExperimental");
                reinforcements = holder.AddComponent<BossReinforcements>();
                reinforcements.boss = boss;
                reinforcements.reinforcementPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/AcechadorOscuro.prefab");

                Check(boss != null && errante != null && boss.Position == 1 && errante.Position == 2, "fase 1: Capitan en E1 y Errante en E2 (solo 2 enemigos: " + Enemies().Count + ")");

                // Llevar al Capitan al 50 % con el Errante VIVO: entra en fase 2 y no debe aparecer nada.
                boss.TakeDamage(boss.CurrentHealth - boss.MaxHealth / 2);
                step = 2;
                wait = 10;
                return;
            }

            case 2:
            {
                BossPhaseController phases = boss.GetComponent<BossPhaseController>();
                Check(phases != null && phases.CurrentIndex == 1, "el Capitan esta en la fase 2 (vida " + boss.CurrentHealth + "/" + boss.MaxHealth + ")");
                Check(errante.IsAlive && Minions() == 1 && !reinforcements.Spawned, "fase 2 con el Errante vivo: no aparece ningun refuerzo (secuaces activos: " + Minions() + ")");

                errante.TakeDamage(9999);
                step = 3;
                wait = 10;
                return;
            }

            case 3:
            {
                Check(reinforcements.Spawned && Minions() == 1, "al caer el Errante aparece UN refuerzo (secuaces activos: " + Minions() + ")");
                BattleUnit minion = null;

                foreach (BattleUnit u in Enemies())
                    if (u != boss)
                        minion = u;

                Check(minion != null && minion.Data.unitName.Contains("Acechador") && minion.Position == 2, "el refuerzo es el Acechador Oscuro en E2 (posicion " + (minion != null ? minion.Position : -1) + ", vida " + (minion != null ? minion.CurrentHealth + "/" + minion.MaxHealth : "-") + ")");
                Check(boss.Position == 1, "el Capitan sigue en E1");

                // Orden de turnos: el refuerzo entra en la ronda siguiente.
                List<BattleUnit> order = tm.GetTurnOrderSnapshot();
                log.AppendLine("   orden de turnos de la ronda actual: " + string.Join(", ", order.ConvertAll(x => x != null ? x.Data.unitName : "-").ToArray()));
                Check(!order.Contains(minion), "el refuerzo no altera el orden de la ronda en curso (entra desde la siguiente)");

                // Objetivo valido: las habilidades de los heroes pueden apuntar a E2.
                AbilityData disparo = null;

                foreach (AbilityData a in Hero("PlayerArquera").GetComponent<CharacterAbilityLoadout>().Abilities)
                    if (a != null && a.name == "Elaina_DisparoCertero")
                        disparo = a;

                Check(disparo != null && disparo.CanTargetPosition(minion.Position), "el refuerzo es un objetivo valido para los heroes (Disparo Certero apunta a E" + minion.Position + ")");

                startRound = tm.CurrentRound;
                step = 4;
                wait = 2;
                return;
            }

            case 4:
            {
                foreach (string hid in new[] { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" })
                    if (Hero(hid) != null)
                        Hero(hid).Heal(9999);   // los heroes pasan turnos: se mantienen con vida para no mezclar la derrota

                // Avanzar hasta la ronda siguiente y comprobar que el refuerzo actua.
                int round = startRound;
                int guard = 0;
                BattleUnit minion = null;

                foreach (BattleUnit u in Enemies())
                    if (u != boss)
                        minion = u;

                // Los heroes pasan; los enemigos juegan solos.
                BattleUnit current = tm.CurrentUnit;

                if (current != null && current.Side == TeamSide.Player && current.IsAlive && !tm.BattleFinished)
                {
                    tm.EndTurn();
                    wait = 1;
                    guard++;
                    return;
                }

                if (tm.CurrentRound >= round + 1 || tm.BattleFinished)
                {
                    List<BattleUnit> order = tm.GetTurnOrderSnapshot();
                    Check(minion == null || order.Contains(minion), "el Acechador figura en el orden de turnos de la ronda " + tm.CurrentRound + " (velocidad " + (minion != null ? minion.Speed : 0) + ")");
                    log.AppendLine("   orden de la ronda " + tm.CurrentRound + ": " + string.Join(", ", order.ConvertAll(x => x != null ? x.Data.unitName + "(" + x.Speed + ")" : "-").ToArray()));

                    // Matar al Acechador: no debe aparecer otro refuerzo (cada refuerzo, una sola vez).
                    if (minion != null)
                        minion.TakeDamage(9999);

                    step = 5;
                    wait = 12;
                    return;
                }

                wait = 1;
                return;
            }

            case 5:
            {
                foreach (string hid in new[] { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" })
                    if (Hero(hid) != null)
                        Hero(hid).Heal(9999);

                Check(Minions() == 0 && reinforcements.Spawned, "al caer el Acechador no aparece otro refuerzo (secuaces activos: " + Minions() + ")");

                // Victoria: con el Capitan derrotado y sin secuaces el combate termina en victoria.
                boss.TakeDamage(9999);
                step = 6;
                wait = 10;
                return;
            }

            case 6:
            {
                Check(tm.BattleFinished && !tm.PartyDefeated, "al caer el ultimo enemigo el combate termina en victoria");
                log.AppendLine();
                log.AppendLine("ENCUENTRO B REAL (asset del Capitan con el Errante, sin refuerzo)");
                step = 7;
                wait = 2;
                return;
            }

            case 7:
            {
                GameSession.StartNewGame();
                RelicInventory.Clear();
                List<GameSession.PartyMemberState> party = new List<GameSession.PartyMemberState>();

                foreach (string id in new[] { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" })
                    party.Add(new GameSession.PartyMemberState { id = id, level = 5, health = 9999, maxHealth = 9999, transformed = id == "PlayerNoelle" });

                GameSession.SavePartySnapshot(party);
                EncounterLoader.DevEncounter = AssetDatabase.LoadAssetAtPath<EncounterData>("Assets/ScriptableObjects/Encounters/Jefe/BOSS_Capitan.asset");
                step = 8;
                wait = 8;
                SceneManager.LoadScene("CombateIntegracion");
                return;
            }

            case 8:
            {
                tm = Object.FindAnyObjectByType<TurnManager>();

                if (tm == null || !tm.BattleActive || Hero("PlayerCaballero") == null)
                {
                    wait = 1;
                    return;
                }

                boss = null;
                errante = null;

                foreach (BattleUnit u in Enemies())
                {
                    if (u.Data.isBoss) boss = u;
                    else errante = u;
                }

                Check(boss != null && errante != null && boss.Position == 1 && errante.Position == 2 && Enemies().Count == 2,
                    "el encuentro real tiene Capitan en E1 y Corrupto Errante en E2 (" + Enemies().Count + " enemigos)");
                Check(Object.FindAnyObjectByType<BossReinforcements>() == null, "sin refuerzos: ningun componente experimental en el encuentro real");
                boss.TakeDamage(boss.CurrentHealth - boss.MaxHealth / 2);
                step = 9;
                wait = 8;
                return;
            }

            case 9:
            {
                BossPhaseController phases = boss.GetComponent<BossPhaseController>();
                Check(phases != null && phases.CurrentIndex == 1 && boss.CurrentHealth * 2 == boss.MaxHealth, "transicion a la fase 2 al 50 % exacto de la vida (" + boss.CurrentHealth + "/" + boss.MaxHealth + ")");
                Check(Enemies().Count == 2, "en la fase 2 no aparece ningun enemigo nuevo (enemigos: " + Enemies().Count + ")");

                // El Capitan cae con el Errante vivo.
                foreach (string hid in new[] { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" })
                    Hero(hid).Heal(9999);

                boss.TakeDamage(9999);
                step = 10;
                wait = 10;
                return;
            }

            case 10:
            {
                bool ended = tm.BattleFinished;
                log.AppendLine("   Capitan derrotado con el Errante vivo: combate terminado = " + ended + ", enemigos vivos = " + Enemies().Count);
                Check(!ended && errante.IsAlive, "REGLA ACTUAL: si el Capitan cae con el Errante vivo el combate CONTINUA hasta derrotar al secuaz");

                // El Errante sigue actuando: se le deja jugar una ronda con los heroes pasando.
                int round = tm.CurrentRound;
                log.AppendLine("   ronda " + round + ", turno de " + (tm.CurrentUnit != null ? tm.CurrentUnit.Data.unitName : "-"));
                errante.TakeDamage(9999);
                step = 11;
                wait = 10;
                return;
            }

            case 11:
            {
                Check(tm.BattleFinished && !tm.PartyDefeated, "al caer tambien el Errante el combate termina en victoria");

                // Derrota: todos los heroes caen -> derrota total.
                EncounterLoader.DevEncounter = AssetDatabase.LoadAssetAtPath<EncounterData>("Assets/ScriptableObjects/Encounters/Jefe/BOSS_Capitan.asset");
                step = 12;
                wait = 2;
                SceneManager.LoadScene("CombateIntegracion");
                return;
            }

            case 12:
            {
                tm = Object.FindAnyObjectByType<TurnManager>();

                if (tm == null || !tm.BattleActive || Hero("PlayerCaballero") == null)
                {
                    wait = 1;
                    return;
                }

                foreach (string hid in new[] { "PlayerEvanora", "PlayerArquera", "PlayerNoelle", "PlayerCaballero" })
                    Hero(hid).TakeDamage(9999);

                step = 13;
                wait = 10;
                return;
            }

            case 13:
            {
                Check(tm.BattleFinished && tm.PartyDefeated && GameSession.IsDefeated, "si caen los cuatro heroes: derrota total y sesion marcada como perdida");
                Finish("completado");
                return;
            }
        }
    }
}
#endif
