using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>
/// Ajuste do Windows para jogos e stream: lê o estado atual, aplica e guarda o valor original (em AppSettings.TweakBackup)
/// na primeira vez que é alterado, para "Restaurar originais" desfazer tudo.
/// </summary>
public sealed class Tweak : INotifyPropertyChanged
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Effect { get; init; } = "";
    public bool NeedsRestart { get; init; }
    internal Func<bool?> Read { get; init; } = () => null;
    internal Action<bool, AppSettings> Write { get; init; } = (_, _) => { };
    internal Action<AppSettings> Restore { get; init; } = _ => { };

    private bool _isOn;
    public bool IsOn { get => _isOn; set { _isOn = value; Changed(nameof(IsOn)); } }
    private string _status = "";
    public string Status { get => _status; set { _status = value; Changed(nameof(Status)); } }
    public string Badge => NeedsRestart ? "REQUER REINICIAR" : "";

    public void Refresh() => IsOn = Read() ?? false;

    private void Changed(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    public event PropertyChangedEventHandler? PropertyChanged;
}

public static class WindowsTweaks
{
    private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string UltimatePerformance = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";

    public static List<Tweak> All() => new()
    {
        new Tweak
        {
            Id = "ultimate_power", Title = "Plano de energia: Desempenho Máximo (Ultimate)",
            Description = "Desbloqueia e ativa o plano oculto 'Desempenho Máximo' da Microsoft, que elimina micro-atrasos de gestão de energia da CPU.",
            Effect = "Clocks sempre prontos, zero latência de despertar de núcleos. Ideal para PC de secretária.",
            Read = () => ActiveScheme()?.Equals(UltimatePerformance, StringComparison.OrdinalIgnoreCase),
            Write = (on, s) =>
            {
                if (!s.TweakBackup.ContainsKey("ultimate_power")) s.TweakBackup["ultimate_power"] = ActiveScheme();
                if (on)
                {
                    if (!PowerCfg("/list").Contains(UltimatePerformance, StringComparison.OrdinalIgnoreCase))
                        PowerCfg($"-duplicatescheme {UltimatePerformance} {UltimatePerformance}");
                    PowerCfg($"/setactive {UltimatePerformance}");
                }
                else if (s.TweakBackup["ultimate_power"] is string prev && !prev.Equals(UltimatePerformance, StringComparison.OrdinalIgnoreCase))
                    PowerCfg($"/setactive {prev}");
                else PowerCfg($"/setactive {Balanced}");
            },
            Restore = s => { if (s.TweakBackup.GetValueOrDefault("ultimate_power") is string prev) PowerCfg($"/setactive {prev}"); },
        },
        new Tweak
        {
            Id = "power", Title = "Plano de energia: Alto desempenho",
            Description = "Mantém a CPU pronta para subir o clock na hora, sem economia agressiva de energia.",
            Effect = "Menos engasgos e 1% low melhor em alguns jogos. Em portáteis gasta mais bateria.",
            Read = () => ActiveScheme()?.Equals(HighPerformance, StringComparison.OrdinalIgnoreCase),
            Write = (on, s) =>
            {
                if (!s.TweakBackup.ContainsKey("power")) s.TweakBackup["power"] = ActiveScheme();
                if (on)
                {
                    if (!PowerCfg("/list").Contains(HighPerformance, StringComparison.OrdinalIgnoreCase))
                        PowerCfg($"-duplicatescheme {HighPerformance} {HighPerformance}");
                    PowerCfg($"/setactive {HighPerformance}");
                }
                else if (s.TweakBackup["power"] is string prev && !prev.Equals(HighPerformance, StringComparison.OrdinalIgnoreCase))
                    PowerCfg($"/setactive {prev}");
                else PowerCfg($"/setactive {Balanced}");
            },
            Restore = s => { if (s.TweakBackup.GetValueOrDefault("power") is string prev) PowerCfg($"/setactive {prev}"); },
        },
        RegTweak("win32prio", "Prioridade de CPU para Jogos em Primeiro Plano (Win32PrioritySeparation)",
            "Ajusta o agendador do Windows (valor 0x26 / 38) para dar fatias de tempo curtas e prioridade máxima à janela ativa (o jogo).",
            "Melhora a resposta de comandos (input lag) e a estabilidade de frametime com apps abertos em fundo.",
            Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", on: 38, off: 2, absentMeansOn: false),
        new Tweak
        {
            Id = "netthrottle", Title = "Desativar Estrangulamento de Rede Multimédia (Network Throttling)",
            Description = "Por padrão, o Windows limita o tráfego de rede quando há áudio/vídeo a tocar e reserva 20% de recursos para fundo.",
            Effect = "Ping mais estável em jogos online e evita perda de pacotes durante lives no OBS (SystemResponsiveness = 0).",
            NeedsRestart = true,
            Read = () => ReadDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex") == -1 &&
                         ReadDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness") == 0,
            Write = (on, s) =>
            {
                const string p = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
                SetDword(s, "netthrottle.a", Registry.LocalMachine, p, "NetworkThrottlingIndex", on ? -1 : 10);
                SetDword(s, "netthrottle.b", Registry.LocalMachine, p, "SystemResponsiveness", on ? 0 : 20);
            },
            Restore = s =>
            {
                const string p = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
                RestoreDword(s, "netthrottle.a", Registry.LocalMachine, p, "NetworkThrottlingIndex");
                RestoreDword(s, "netthrottle.b", Registry.LocalMachine, p, "SystemResponsiveness");
            },
        },
        new Tweak
        {
            Id = "mmcss_games", Title = "Prioridade Máxima de GPU e CPU no Agendador de Jogos (MMCSS)",
            Description = "Configura o perfil 'Games' do Windows para GPU Priority = 8, Priority = 6 e Scheduling Category = High.",
            Effect = "Garante que o motor do jogo tem prioridade sobre navegadores e apps secundários.",
            Read = () => ReadDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority") == 8 &&
                         ReadDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority") == 6,
            Write = (on, s) =>
            {
                const string p = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
                SetDword(s, "mmcss.gpu", Registry.LocalMachine, p, "GPU Priority", on ? 8 : 2);
                SetDword(s, "mmcss.prio", Registry.LocalMachine, p, "Priority", on ? 6 : 2);
            },
            Restore = s =>
            {
                const string p = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
                RestoreDword(s, "mmcss.gpu", Registry.LocalMachine, p, "GPU Priority");
                RestoreDword(s, "mmcss.prio", Registry.LocalMachine, p, "Priority");
            },
        },
        RegTweak("gamemode", "Modo de Jogo do Windows",
            "Dá prioridade ao jogo e evita que o Windows Update instale ou reinicie durante a partida.",
            "Recomendado ligado (é o padrão do Windows 11).",
            Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", on: 1, off: 0, absentMeansOn: true),
        new Tweak
        {
            Id = "gamedvr", Title = "Desativar gravação em segundo plano (Game DVR)",
            Description = "Desliga a captura contínua do Xbox Game Bar, que grava os últimos minutos de jogo o tempo todo.",
            Effect = "Recupera FPS e reduz uso de GPU/codificador NVENC (muito importante se fazes live com OBS).",
            Read = () => (ReadDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled") ?? 1) == 0 &&
                         (ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") ?? 1) == 0,
            Write = (on, s) =>
            {
                SetDword(s, "gamedvr.a", Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", on ? 0 : 1);
                SetDword(s, "gamedvr.b", Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", on ? 0 : 1);
            },
            Restore = s =>
            {
                RestoreDword(s, "gamedvr.a", Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled");
                RestoreDword(s, "gamedvr.b", Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled");
            },
        },
        RegTweak("hags", "Agendamento de GPU acelerado por hardware (HAGS)",
            "A própria placa de vídeo gerencia a sua fila de trabalho, em vez da CPU.",
            "Necessário para o Frame Generation do DLSS 3/4. Pode reduzir latência.",
            Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", on: 2, off: 1,
            absentMeansOn: false, needsRestart: true),
        new Tweak
        {
            Id = "windowed", Title = "Otimizações para jogos em janela",
            Description = "Faz jogos DirectX 10/11 em janela ou \"sem bordas\" usarem o modelo de apresentação moderno (flip).",
            Effect = "Menor latência e suporte a G-Sync/FreeSync em modo janela. Recurso do Windows 11.",
            Read = () => DxSettings().GetValueOrDefault("SwapEffectUpgradeEnable") == "1",
            Write = (on, s) =>
            {
                if (!s.TweakBackup.ContainsKey("windowed"))
                    s.TweakBackup["windowed"] = Registry.CurrentUser.OpenSubKey(DxPath)?.GetValue("DirectXUserGlobalSettings") as string ?? "<ausente>";
                var d = DxSettings();
                d["SwapEffectUpgradeEnable"] = on ? "1" : "0";
                using var k = Registry.CurrentUser.CreateSubKey(DxPath);
                k.SetValue("DirectXUserGlobalSettings", string.Concat(d.Select(kv => $"{kv.Key}={kv.Value};")));
            },
            Restore = s =>
            {
                if (s.TweakBackup.GetValueOrDefault("windowed") is not string prev) return;
                using var k = Registry.CurrentUser.CreateSubKey(DxPath);
                if (prev == "<ausente>") k.DeleteValue("DirectXUserGlobalSettings", false);
                else k.SetValue("DirectXUserGlobalSettings", prev);
            },
        },
        new Tweak
        {
            Id = "mouse", Title = "Desligar aceleração do rato (Mira 1:1)",
            Description = "Desmarca \"Aprimorar precisão do ponteiro\": o cursor anda sempre a mesma distância para o mesmo movimento físico.",
            Effect = "Deixa a memória muscular e a mira consistentes em jogos competitivos.",
            Read = () => ReadString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed") == "0",
            Write = (on, s) =>
            {
                foreach (var n in new[] { "MouseSpeed", "MouseThreshold1", "MouseThreshold2" })
                    if (!s.TweakBackup.ContainsKey($"mouse.{n}")) s.TweakBackup[$"mouse.{n}"] = ReadString(Registry.CurrentUser, @"Control Panel\Mouse", n);
                var v = on ? new[] { "0", "0", "0" } : new[] { "1", "6", "10" };
                ApplyMouse(v);
            },
            Restore = s =>
            {
                if (!s.TweakBackup.ContainsKey("mouse.MouseSpeed")) return;
                ApplyMouse(new[] { "MouseSpeed", "MouseThreshold1", "MouseThreshold2" }
                    .Select(n => s.TweakBackup.GetValueOrDefault($"mouse.{n}") ?? "0").ToArray());
            },
        },
        RegTweak("hiber", "Desativar Hibernação e Inicialização Rápida (Fast Startup)",
            "Desliga o ficheiro hiberfil.sys para que o PC faça um encerramento real quando o desligas.",
            "Libera vários GB no SSD principal e evita que o Windows acumule dias de uptime e bugs de drivers.",
            Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", on: 0, off: 1, absentMeansOn: false),
        RegTweak("bing", "Desativar Pesquisa Web (Bing) e Sugestões no Menu Iniciar",
            "Impede que o Menu Iniciar faça buscas na internet cada vez que digitas o nome de um programa.",
            "Menu Iniciar instantâneo e menos processos de pesquisa a correr em segundo plano.",
            Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", on: 1, off: 0, absentMeansOn: false),
    };

    public static void RestoreAll(IEnumerable<Tweak> tweaks, AppSettings s)
    {
        foreach (var t in tweaks)
            try { t.Restore(s); } catch { }
        s.TweakBackup.Clear();
        s.Save();
    }

    private static Tweak RegTweak(string id, string title, string desc, string effect, RegistryKey hive, string path, string name,
                                  int on, int off, bool absentMeansOn, bool needsRestart = false) => new()
    {
        Id = id, Title = title, Description = desc, Effect = effect, NeedsRestart = needsRestart,
        Read = () => (ReadDword(hive, path, name) is int v) ? v == on : absentMeansOn,
        Write = (enable, s) => SetDword(s, id, hive, path, name, enable ? on : off),
        Restore = s => RestoreDword(s, id, hive, path, name),
    };

    private static int? ReadDword(RegistryKey hive, string path, string name)
    {
        using var k = hive.OpenSubKey(path);
        return k?.GetValue(name) is int v ? v : null;
    }

    private static string? ReadString(RegistryKey hive, string path, string name)
    {
        using var k = hive.OpenSubKey(path);
        return k?.GetValue(name)?.ToString();
    }

    private static void SetDword(AppSettings s, string backupKey, RegistryKey hive, string path, string name, int value)
    {
        if (!s.TweakBackup.ContainsKey(backupKey))
            s.TweakBackup[backupKey] = ReadDword(hive, path, name)?.ToString() ?? "<ausente>";
        using var k = hive.CreateSubKey(path);
        k.SetValue(name, value, RegistryValueKind.DWord);
    }

    private static void RestoreDword(AppSettings s, string backupKey, RegistryKey hive, string path, string name)
    {
        if (s.TweakBackup.GetValueOrDefault(backupKey) is not string prev) return;
        using var k = hive.CreateSubKey(path);
        if (prev == "<ausente>") k.DeleteValue(name, false);
        else k.SetValue(name, int.Parse(prev), RegistryValueKind.DWord);
    }

    private const string DxPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

    private static Dictionary<string, string> DxSettings()
    {
        var d = new Dictionary<string, string>();
        string raw = Registry.CurrentUser.OpenSubKey(DxPath)?.GetValue("DirectXUserGlobalSettings") as string ?? "";
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2) d[kv[0].Trim()] = kv[1].Trim();
        }
        return d;
    }

    private static void ApplyMouse(string[] v)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse"))
        {
            k.SetValue("MouseSpeed", v[0]);
            k.SetValue("MouseThreshold1", v[1]);
            k.SetValue("MouseThreshold2", v[2]);
        }
        var p = v.Select(int.Parse).ToArray();
        SystemParametersInfo(0x0004 /* SPI_SETMOUSE */, 0, p, 0x01 | 0x02);
    }

    private static string? ActiveScheme()
    {
        var m = System.Text.RegularExpressions.Regex.Match(PowerCfg("/getactivescheme"), @"[0-9a-fA-F\-]{36}");
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    private static string PowerCfg(string args)
    {
        var psi = new ProcessStartInfo("powercfg.exe", args)
        {
            RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(5000);
        return output;
    }

    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action, uint param, int[] data, uint winIni);
}