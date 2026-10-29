using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ResetExplode;

public sealed record AiFinding(string Category, string Title, string Impact, bool IsOptimal);

public sealed class AiReport
{
    public int CurrentScore { get; init; }
    public int ProjectedScore { get; init; }
    public string Verdict { get; init; } = "";
    public string Summary { get; init; } = "";
    public long TempBytes { get; init; }
    public List<AiFinding> Findings { get; init; } = new();
}

/// <summary>
/// Motor Heurístico ("Trin0 AI Advisor"): analisa Switches do Windows, RAM, Apps em segundo plano,
/// Lixo Temporário (%TEMP% e DirectX Cache), Arranque e Temperaturas para dar uma nota de 0 a 100% e otimizar num clique.
/// </summary>
public static class PcAdvisor
{
    public static AiReport Analyze(IReadOnlyList<Tweak> tweaks, IReadOnlyList<BackgroundApp> apps, float? cpuTemp, float? gpuTemp)
    {
        int score = 100;
        int recoverable = 0;
        var findings = new List<AiFinding>();

        // 1) Switches de Otimização do Windows (até 36 pontos)
        string[] coreTweaks = { "gamemode", "gamedvr", "win32prio", "netthrottle", "mmcss_games", "hags" };
        int offTweaks = 0;
        foreach (var id in coreTweaks)
        {
            var t = tweaks.FirstOrDefault(x => x.Id == id);
            if (t == null) continue;
            try { t.Refresh(); } catch { }
            if (!t.IsOn) offTweaks++;
        }
        bool hasPower = tweaks.Any(t => (t.Id == "ultimate_power" || t.Id == "power") && t.IsOn);
        if (!hasPower) offTweaks++;

        if (offTweaks > 0)
        {
            int penalty = Math.Min(36, offTweaks * 6);
            score -= penalty;
            recoverable += penalty;
            findings.Add(new("WINDOWS", $"{offTweaks} ajustes de performance desativados", $"+{penalty}% ao ativar", false));
        }
        else
        {
            findings.Add(new("WINDOWS", "Switches de kernel, rede e energia otimizados", "Ótimo", true));
        }

        // 2) Memória RAM & Processos em Segundo Plano (até 24 pontos)
        var (totalRam, availRam) = Optimizer.MemoryStatus();
        double ramPct = totalRam > 0 ? 100.0 * (totalRam - availRam) / totalRam : 40;
        int heavyApps = apps.Count(a => a.Suggested);

        if (ramPct > 75 || heavyApps >= 2)
        {
            int penalty = (ramPct > 80 ? 14 : ramPct > 65 ? 8 : 4) + Math.Min(10, heavyApps * 4);
            score -= penalty;
            recoverable += penalty;
            findings.Add(new("MEMÓRIA", $"RAM a {ramPct:0}% e {heavyApps} apps em fundo", $"+{penalty}% ao limpar", false));
        }
        else
        {
            findings.Add(new("MEMÓRIA", $"RAM livre saudável ({100 - ramPct:0}% livre)", "Ótimo", true));
        }

        // 3) Ficheiros Temporários & Cache de Shaders (até 20 pontos)
        long tempBytes = MeasureTempBytes();
        double tempMb = tempBytes / 1048576.0;
        if (tempMb > 250)
        {
            int penalty = tempMb > 2000 ? 20 : tempMb > 800 ? 14 : 8;
            score -= penalty;
            recoverable += penalty;
            string sizeTxt = tempMb >= 1024 ? $"{tempMb / 1024:0.0} GB" : $"{tempMb:0} MB";
            findings.Add(new("CACHE & TEMP", $"{sizeTxt} de ficheiros temporários acumulados", $"+{penalty}% ao limpar", false));
        }
        else
        {
            findings.Add(new("CACHE & TEMP", "Pastas temporárias limpas", "Ótimo", true));
        }

        // 4) Programas no Arranque do Windows (até 10 pontos)
        int startupEnabled = 0;
        try { startupEnabled = StartupManager.List().Count(s => s.Enabled); } catch { }
        if (startupEnabled > 8)
        {
            score -= 10;
            findings.Add(new("ARRANQUE", $"{startupEnabled} programas a iniciar com o Windows", "Reveja na aba Inicialização", false));
        }
        else
        {
            findings.Add(new("ARRANQUE", $"{startupEnabled} programas ativos no arranque", "Ótimo", true));
        }

        // 5) Temperatura em Repouso / Carga (até 10 pontos)
        if (cpuTemp >= 82 || gpuTemp >= 80)
        {
            score -= 10;
            findings.Add(new("TÉRMICO", $"Temperatura elevada (CPU {cpuTemp:0}°C / GPU {gpuTemp:0}°C)", "Verificar arrefecimento", false));
        }
        else
        {
            findings.Add(new("TÉRMICO", "Temperaturas dentro do ideal", "Ótimo", true));
        }

        score = Math.Clamp(score, 12, 100);
        int projected = Math.Clamp(score + recoverable, score, 100);

        string verdict = score switch
        {
            >= 90 => "OTIMIZAÇÃO MÁXIMA",
            >= 72 => "BOM · PODE MELHORAR",
            >= 50 => "DESEMPENHO LIMITADO",
            _ => "PRECISA DE OTIMIZAÇÃO"
        };

        string summary = score >= 90
            ? "A IA analisou o teu sistema: os switches principais, a RAM e a cache estão afinados para máxima performance!"
            : $"A IA detetou que o teu PC está a operar a {score}% do potencial de otimização. Podes subir para ~{projected}% automaticamente com 1 clique.";

        return new AiReport
        {
            CurrentScore = score,
            ProjectedScore = projected,
            Verdict = verdict,
            Summary = summary,
            TempBytes = tempBytes,
            Findings = findings
        };
    }

    public static long MeasureTempBytes()
    {
        long total = 0;
        foreach (string dir in TempFolders())
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                var di = new DirectoryInfo(dir);
                foreach (var f in di.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
                    total += f.Length;
            }
            catch { }
        }
        return total;
    }

    public static long CleanTempFiles()
    {
        long freed = 0;
        foreach (string dir in TempFolders())
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                var di = new DirectoryInfo(dir);
                foreach (var f in di.EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
                {
                    try { long len = f.Length; f.Delete(); freed += len; } catch { }
                }
                foreach (var sub in di.EnumerateDirectories("*", new EnumerationOptions { IgnoreInaccessible = true }))
                {
                    try { sub.Delete(true); } catch { }
                }
            }
            catch { }
        }
        return freed;
    }

    private static string[] TempFolders() => new[]
    {
        Path.GetTempPath(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"D3DSCache"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NVIDIA\GLCache"),
    };

    /// <summary>
    /// Desenha um arco de velocímetro (240 graus, de -120° a +120° a partir do topo).
    /// </summary>
    public static Geometry GaugeArc(double fraction, double cx, double cy, double r)
    {
        fraction = Math.Clamp(fraction, 0.001, 1.0);
        const double startDeg = -210; // canto inferior esquerdo
        const double sweepMax = 240;  // até ao canto inferior direito
        double endDeg = startDeg + sweepMax * fraction;

        double sRad = startDeg * Math.PI / 180.0;
        double eRad = endDeg * Math.PI / 180.0;

        var start = new Point(cx + r * Math.Cos(sRad), cy + r * Math.Sin(sRad));
        var end = new Point(cx + r * Math.Cos(eRad), cy + r * Math.Sin(eRad));
        bool isLarge = (sweepMax * fraction) > 180.0;

        var fig = new PathFigure { StartPoint = start, IsClosed = false };
        fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, isLarge, SweepDirection.Clockwise, true));
        var g = new PathGeometry(new[] { fig });
        g.Freeze();
        return g;
    }
}