using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>App do catálogo "Instalar apps" (estilo Ninite), instalado pelo winget.</summary>
public sealed class InstallableApp : INotifyPropertyChanged
{
    public string Category { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string WingetId { get; init; } = "";
    public string DisplayPrefix { get; init; } = "";
    public bool PreSelect { get; init; } = false;

    private bool _selected, _installed;
    private string _status = "";

    public bool Selected { get => _selected; set => Set(ref _selected, value); }
    public bool Installed
    {
        get => _installed;
        set { Set(ref _installed, value); OnChanged(nameof(CanSelect)); }
    }
    public bool CanSelect => !Installed;
    public string Status { get => _status; set => Set(ref _status, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? p = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnChanged(p);
    }
}

public static class AppInstaller
{
    public static List<InstallableApp> Catalog() => new()
    {
        // Navegadores
        new() { Category = "NAVEGADORES", Name = "Google Chrome", Description = "Navegador rápido do Google.", WingetId = "Google.Chrome", DisplayPrefix = "Google Chrome", PreSelect = true },
        new() { Category = "NAVEGADORES", Name = "Brave Browser", Description = "Navegador focado em privacidade e bloqueio nativo de anúncios.", WingetId = "Brave.Brave", DisplayPrefix = "Brave" },
        new() { Category = "NAVEGADORES", Name = "Mozilla Firefox", Description = "Navegador aberto e independente da Mozilla.", WingetId = "Mozilla.Firefox", DisplayPrefix = "Mozilla Firefox" },
        new() { Category = "NAVEGADORES", Name = "Opera GX", Description = "Navegador com limitadores de CPU, RAM e rede.", WingetId = "Opera.OperaGX", DisplayPrefix = "Opera GX" },

        // Jogos & Launchers
        new() { Category = "JOGOS & LAUNCHERS", Name = "Steam", Description = "Principal loja e biblioteca de jogos de PC.", WingetId = "Valve.Steam", DisplayPrefix = "Steam", PreSelect = true },
        new() { Category = "JOGOS & LAUNCHERS", Name = "Epic Games Launcher", Description = "Loja da Epic Games (Fortnite, jogos grátis semanais).", WingetId = "EpicGames.EpicGamesLauncher", DisplayPrefix = "Epic Games Launcher" },
        new() { Category = "JOGOS & LAUNCHERS", Name = "EA App", Description = "Plataforma de jogos da Electronic Arts (EA FC, Battlefield, Apex).", WingetId = "ElectronicArts.EADesktop", DisplayPrefix = "EA app" },
        new() { Category = "JOGOS & LAUNCHERS", Name = "Ubisoft Connect", Description = "Plataforma da Ubisoft (Rainbow Six Siege, Assassin's Creed).", WingetId = "Ubisoft.Connect", DisplayPrefix = "Ubisoft Connect" },

        // Comunicação & Multimédia
        new() { Category = "COMUNICAÇÃO & MÚSICA", Name = "Discord", Description = "Chat de voz, vídeo e texto para jogar com amigos.", WingetId = "Discord.Discord", DisplayPrefix = "Discord", PreSelect = true },
        new() { Category = "COMUNICAÇÃO & MÚSICA", Name = "Spotify", Description = "Streaming de música e podcasts.", WingetId = "Spotify.Spotify", DisplayPrefix = "Spotify" },
        new() { Category = "COMUNICAÇÃO & MÚSICA", Name = "VLC Media Player", Description = "Reprodutor de vídeo e áudio que abre qualquer formato.", WingetId = "VideoLAN.VLC", DisplayPrefix = "VLC media player", PreSelect = true },

        // Streaming & Criação
        new() { Category = "STREAMING & ÁUDIO", Name = "OBS Studio", Description = "Gravador e transmissor de lives (Twitch, YouTube).", WingetId = "OBSProject.OBSStudio", DisplayPrefix = "OBS Studio", PreSelect = true },
        new() { Category = "STREAMING & ÁUDIO", Name = "Voicemeeter Banana", Description = "Mesa de mistura virtual para separar o áudio do jogo, Discord e música na live.", WingetId = "VB-Audio.Voicemeeter.Banana", DisplayPrefix = "VB-Audio VoiceMeeter" },
        new() { Category = "STREAMING & ÁUDIO", Name = "VB-Audio Virtual Cable", Description = "Cabo de áudio virtual complementar ao Voicemeeter / OBS.", WingetId = "VB-Audio.Cable", DisplayPrefix = "VB-Audio Virtual Cable" },

        // Utilitários & Ferramentas
        new() { Category = "UTILITÁRIOS & HARDWARE", Name = "7-Zip", Description = "Descompactador gratuito e ultraleve (zip, rar, 7z).", WingetId = "7zip.7zip", DisplayPrefix = "7-Zip", PreSelect = true },
        new() { Category = "UTILITÁRIOS & HARDWARE", Name = "WinRAR", Description = "Compactador clássico para ficheiros .rar e .zip.", WingetId = "RARLab.WinRAR", DisplayPrefix = "WinRAR" },
        new() { Category = "UTILITÁRIOS & HARDWARE", Name = "Lightshot", Description = "Captura de ecrã rápida e leve com a tecla Print Screen.", WingetId = "Skillbrains.Lightshot", DisplayPrefix = "Lightshot", PreSelect = true },
        new() { Category = "UTILITÁRIOS & HARDWARE", Name = "ShareX", Description = "Captura de ecrã avançada, gravação de GIF/vídeo e upload rápido.", WingetId = "ShareX.ShareX", DisplayPrefix = "ShareX" },
        new() { Category = "UTILITÁRIOS & HARDWARE", Name = "qBittorrent", Description = "Cliente de torrent leve, código aberto e sem anúncios.", WingetId = "qBittorrent.qBittorrent", DisplayPrefix = "qBittorrent" },
        new() { Category = "UTILITÁRIOS & HARDWARE", Name = "CPU-Z", Description = "Informações detalhadas sobre processador, motherboard e timings da RAM.", WingetId = "CPUID.CPU-Z", DisplayPrefix = "CPUID CPU-Z" },
        new() { Category = "UTILITÁRIOS & HARDWARE", Name = "HWiNFO", Description = "Monitorização profunda de todos os sensores do hardware.", WingetId = "REALiX.HWiNFO", DisplayPrefix = "HWiNFO" },
        new() { Category = "UTILITÁRIOS & HARDWARE", Name = "MSI Afterburner", Description = "Controlo de ventoinhas, overclock e undervolt da placa gráfica.", WingetId = "Guru3D.Afterburner", DisplayPrefix = "MSI Afterburner" },
    };

    public static string WingetPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\winget.exe");

    public static bool WingetAvailable => File.Exists(WingetPath);

    public static HashSet<string> InstalledNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const string path = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        foreach (var (hive, view) in new[] { (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32),
                                             (RegistryHive.CurrentUser, RegistryView.Default) })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(path);
                if (root == null) continue;
                foreach (var sub in root.GetSubKeyNames())
                {
                    using var k = root.OpenSubKey(sub);
                    if (k?.GetValue("DisplayName") is string n && !string.IsNullOrWhiteSpace(n)) names.Add(n.Trim());
                }
            }
            catch { }
        }
        return names;
    }

    public static void RefreshInstalled(IEnumerable<InstallableApp> apps)
    {
        var names = InstalledNames();
        foreach (var a in apps)
        {
            a.Installed = names.Any(n => n.StartsWith(a.DisplayPrefix, StringComparison.OrdinalIgnoreCase) ||
                                         n.Contains(a.DisplayPrefix, StringComparison.OrdinalIgnoreCase));
            if (a.Installed) a.Selected = false;
        }
    }

    public static async Task<(bool Ok, string Message)> InstallAsync(InstallableApp app, CancellationToken ct, bool upgrade = false)
    {
        var psi = new ProcessStartInfo(WingetPath,
            $"{(upgrade ? "upgrade" : "install")} --id {app.WingetId} -e --silent --accept-package-agreements --accept-source-agreements --disable-interactivity")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o winget.");
        var outTask = p.StandardOutput.ReadToEndAsync(ct);
        var errTask = p.StandardError.ReadToEndAsync(ct);
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { try { p.Kill(true); } catch { } throw; }
        string output = await outTask + await errTask;

        return p.ExitCode switch
        {
            0 => (true, "Instalado."),
            unchecked((int)0x8A15002B) or unchecked((int)0x8A150061) => (true, "Já estava instalado."),
            unchecked((int)0x8A150014) => (false, "Pacote não encontrado no winget."),
            unchecked((int)0x8A150008) => (false, "Falha no download: confira a internet e tente de novo."),
            _ => (false, $"Falhou (código 0x{p.ExitCode:X8}). {LastLine(output)}".Trim()),
        };
    }

    private static string LastLine(string output) =>
        output.Split('\n', '\r').Select(l => l.Trim())
              .LastOrDefault(l => l.Length > 3 && !l.All(c => "-\\|/█▒ ".Contains(c)) && !l.Contains("KB /") && !l.Contains("MB /")) ?? "";
}