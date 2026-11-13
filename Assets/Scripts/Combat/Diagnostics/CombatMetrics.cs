using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Datos del diagnostico de balance. Solo lectura: nunca modifica el
// combate ni el balance, no forma parte de la partida (no se guarda en
// GameSession) y vive unicamente en memoria mientras el juego esta
// abierto. Sin telemetria ni servicios externos.
public static class CombatMetrics
{
    public const int MaxReports = 200;

    public enum Duration
    {
        BelowTarget,
        WithinTarget,
        AboveTarget,
        NoTarget
    }

    public class MemberReport
    {
        public string name;
        public int directDamage;
        public int periodicDamage;
        public int finalHealth;      // antes de la recuperacion poscombate
        public int maxHealth;
        public bool defeated;
    }

    public class Report
    {
        public string encounterId;
        public string zone;
        public int rounds;
        public bool victory;
        public int targetMin;
        public int targetMax;
        public Duration duration;
        public int potionsUsed;
        public int antidotesUsed;
        public int experienceAwarded;
        public int coinsAwarded;

        // IA enemiga usada y su actividad (solo lectura).
        public string aiVersion = "v0.1";
        public int enemySpecialUses;
        public int enemyHeals;
        public readonly Dictionary<string, int> enemyTargets = new Dictionary<string, int>();

        public readonly List<MemberReport> members = new List<MemberReport>();

        public string TargetsText()
        {
            if (enemyTargets.Count == 0)
                return "-";

            List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(enemyTargets);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));

            List<string> parts = new List<string>();

            foreach (KeyValuePair<string, int> pair in list)
                parts.Add(pair.Key.Replace("Player", "") + " x" + pair.Value);

            return string.Join(", ", parts);
        }

        public int DefeatedCount
        {
            get
            {
                int n = 0;
                foreach (MemberReport m in members)
                    if (m.defeated) n++;
                return n;
            }
        }

        public int TotalDirect
        {
            get { int n = 0; foreach (MemberReport m in members) n += m.directDamage; return n; }
        }

        public int TotalPeriodic
        {
            get { int n = 0; foreach (MemberReport m in members) n += m.periodicDamage; return n; }
        }
    }

    private static readonly List<Report> reports = new List<Report>();

    public static IReadOnlyList<Report> Reports => reports;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        reports.Clear();
    }

    public static void Clear()
    {
        reports.Clear();
    }

    public static void Add(Report report)
    {
        if (report == null)
            return;

        reports.Add(report);

        if (reports.Count > MaxReports)
            reports.RemoveAt(0);
    }

    // Objetivo de rondas por zona (Balance v0.1). 0/0 = sin objetivo.
    public static void TargetRounds(EnemyArchetypeData.Zone zone, out int min, out int max)
    {
        switch (zone)
        {
            case EnemyArchetypeData.Zone.Bosque: min = 4; max = 6; break;
            case EnemyArchetypeData.Zone.Corrupcion: min = 5; max = 7; break;
            case EnemyArchetypeData.Zone.Jefe: min = 0; max = 0; break;
            default: min = 2; max = 4; break;
        }
    }

    public static Duration Classify(int rounds, int min, int max)
    {
        if (max <= 0)
            return Duration.NoTarget;

        if (rounds < min) return Duration.BelowTarget;
        if (rounds > max) return Duration.AboveTarget;
        return Duration.WithinTarget;
    }

    public static string DurationText(Duration d)
    {
        switch (d)
        {
            case Duration.BelowTarget: return "POR DEBAJO del objetivo";
            case Duration.AboveTarget: return "POR ENCIMA del objetivo";
            case Duration.WithinTarget: return "DENTRO del objetivo";
            default: return "sin objetivo definido";
        }
    }

    // Ojo: el HUD copia a su registro los mensajes con ciertas palabras
    // (VICTORIA, DERROTA, derrotado, recibe, Experiencia...). Este
    // texto las evita a proposito.
    public static string Summary(Report r)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("[Metricas] Encuentro " + r.encounterId + " (" + r.zone + ") - " +
                      (r.victory ? "Victoria" : "Derrota"));
        sb.AppendLine("  Rondas: " + r.rounds +
                      (r.targetMax > 0 ? " (objetivo " + r.targetMin + "-" + r.targetMax + ")" : "") +
                      " -> " + DurationText(r.duration));
        sb.AppendLine("  Dano recibido: directo " + r.TotalDirect + ", periodico " + r.TotalPeriodic);

        foreach (MemberReport m in r.members)
        {
            sb.AppendLine("    " + m.name + ": directo " + m.directDamage + ", periodico " +
                          m.periodicDamage + ", HP final " + m.finalHealth + "/" + m.maxHealth +
                          (m.defeated ? " (caido)" : ""));
        }

        sb.AppendLine("  IA enemiga " + r.aiVersion + ": especiales usados " + r.enemySpecialUses +
                      ", curaciones enemigas " + r.enemyHeals + ", objetivos: " + r.TargetsText());
        sb.AppendLine("  Caidos: " + r.DefeatedCount);
        sb.AppendLine("  Objetos: pociones " + r.potionsUsed + ", antidotos " + r.antidotesUsed);
        sb.Append("  Recompensas: XP " + r.experienceAwarded + ", Coronas " + r.coinsAwarded);

        return sb.ToString();
    }

    // Una fila por protagonista y encuentro. Solo editor o compilaciones
    // de desarrollo. Devuelve la ruta escrita o null.
    public static string ExportCsv(string path = null)
    {
        if (!Debug.isDebugBuild)
            return null;

        if (string.IsNullOrEmpty(path))
            path = Path.Combine(Application.persistentDataPath, "combat_metrics.csv");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("encuentro,zona,rondas,objetivo,duracion,resultado,personaje,dano_directo,dano_periodico,hp_final,hp_max,derrotado,pociones,antidotos,xp,coronas,ia,especiales_enemigos,curaciones_enemigas");

        foreach (Report r in reports)
        {
            string target = r.targetMax > 0 ? r.targetMin + "-" + r.targetMax : "";

            foreach (MemberReport m in r.members)
            {
                sb.AppendLine(string.Join(",",
                    r.encounterId, r.zone, r.rounds, target, r.duration,
                    r.victory ? "victoria" : "derrota",
                    m.name, m.directDamage, m.periodicDamage,
                    m.finalHealth, m.maxHealth, m.defeated ? 1 : 0,
                    r.potionsUsed, r.antidotesUsed,
                    r.experienceAwarded, r.coinsAwarded,
                    r.aiVersion, r.enemySpecialUses, r.enemyHeals));
            }
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        return path;
    }
}
