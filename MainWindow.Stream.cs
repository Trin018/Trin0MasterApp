using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Aba STREAM & OBS + Motor Trin0 AI Advisor com Velocímetro na aba Otimização.</summary>
public partial class MainWindow
{
    private bool _streamBusy;

    // ================== TRIN0 AI ADVISOR (VELOCÍMETRO) ==================

    private void RefreshAiAdvisor()
    {
        if (AiGaugeTrack == null) return;
        var rep = PcAdvisor.Analyze(_tweaks, _apps, _monitor.CpuTemperature, MainGpuTemp());

        AiScoreText.Text = $"{rep.CurrentScore}%";
        AiVerdictText.Text = rep.Verdict;
        AiSummaryText.Text = rep.Summary;
        AiProjectedText.Text = rep.ProjectedScore > rep.CurrentScore
            ? $"Potencial estimado após otimizar: {rep.ProjectedScore}%"
            : "O teu PC já está no nível máximo de otimização!";

        AiGaugeTrack.Data = PcAdvisor.GaugeArc(1.0, 110, 115, 86);
        AiGaugeFill.Data = PcAdvisor.GaugeArc(rep.CurrentScore / 100.0, 110, 115, 86);

        // Cor dinâmica do velocímetro consoante a nota (Vermelho -> Laranja -> Cor de Destaque)
        Brush gaugeBrush = rep.CurrentScore < 55
            ? new SolidColorBrush(Color.FromRgb(244, 63, 94))
            : rep.CurrentScore < 80
                ? new SolidColorBrush(Color.FromRgb(245, 158, 11))
                : (Brush)FindResource("Accent");
        AiGaugeFill.Stroke = gaugeBrush;
        AiScoreText.Foreground = gaugeBrush;

        AiFindingsList.ItemsSource = rep.Findings;
    }

    private async void BtnAiAuto_Click(object sender, RoutedEventArgs e)
    {
        BtnAiAuto.IsEnabled = false;
        BtnAiAuto.Content = "A OTIMIZAR COM IA…";
        OptResult.Text = "A aplicar ajustes inteligentes, a limpar ficheiros temporários e a libertar memória RAM…";

        try
        {
            // 1) Ativa os switches seguros recomendados pela IA
            string[] safeIds = { "ultimate_power", "gamemode", "gamedvr", "win32prio", "netthrottle", "mmcss_games" };
            foreach (var id in safeIds)
            {
                var t = _tweaks.FirstOrDefault(x => x.Id == id);
                if (t == null) continue;
                try
                {
                    t.Refresh();
                    if (!t.IsOn)
                    {
                        t.Write(true, _settings);
                        t.Refresh();
                        t.Status = "Ativado pela IA.";
                    }
                }
                catch { }
            }
            _settings.Save();
            OptTweaks.ItemsSource = null;
            OptTweaks.ItemsSource = _tweaks;

            // 2) Fecha apps em segundo plano sugeridas e limpa a RAM + Standby List
            var toClose = _apps.Where(a => a.Selected).ToList();
            var closeRes = await Task.Run(() => Optimizer.CloseApps(toClose));
            var memRes = await Task.Run(Optimizer.CleanMemory);

            // 3) Limpa pastas %TEMP% e Cache de Shaders
            long tempFreed = await Task.Run(PcAdvisor.CleanTempFiles);

            RefreshApps();
            UpdateRamStatus();
            RefreshAiAdvisor();

            double ramMb = memRes.FreedBytes / 1048576.0;
            double diskMb = tempFreed / 1048576.0;
            OptResult.Text = $"✔ Otimização IA concluída! Switches ativados · {ramMb:N0} MB de RAM libertada · {diskMb:N0} MB de cache/temp apagados.";
        }
        catch (Exception ex)
        {
            OptResult.Text = "Erro durante a otimização: " + ex.Message;
        }
        finally
        {
            BtnAiAuto.IsEnabled = true;
            BtnAiAuto.Content = "AUTO-OTIMIZAR COM IA (1 CLIQUE)";
        }
    }

    // ================== ABA STREAM & OBS ==================

    private static string? FindObsExe()
    {
        string[] paths =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"obs-studio\bin\64bit\obs64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"obs-studio\bin\64bit\obs64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\obs-studio\bin\64bit\obs64.exe"),
        };
        return paths.FirstOrDefault(File.Exists);
    }

    private static string? FindVoicemeeterExe()
    {
        string[] paths =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"VB\Voicemeeter\voicemeeterpro.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VB\Voicemeeter\voicemeeterpro.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"VB\Voicemeeter\voicemeeter8.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"VB\Voicemeeter\voicemeeter.exe"),
        };
        return paths.FirstOrDefault(File.Exists);
    }

    private static bool HasVbCable()
    {
        var names = AppInstaller.InstalledNames();
        return names.Any(n => n.Contains("VB-Audio Virtual Cable", StringComparison.OrdinalIgnoreCase) ||
                              n.Contains("VBCABLE", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExeRunAsAdmin(string exePath)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
            string? val = k?.GetValue(exePath) as string;
            return val != null && val.Contains("RUNASADMIN", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static void SetExeRunAsAdmin(string exePath, bool admin)
    {
        const string flag = "RUNASADMIN";
        using var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
        var tokens = ((k.GetValue(exePath) as string) ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t != "~").ToList();
        if (admin && !tokens.Contains(flag, StringComparer.OrdinalIgnoreCase)) tokens.Add(flag);
        if (!admin) tokens.RemoveAll(t => t.Equals(flag, StringComparison.OrdinalIgnoreCase));
        if (tokens.Count == 0) k.DeleteValue(exePath, false);
        else k.SetValue(exePath, "~ " + string.Join(" ", tokens), RegistryValueKind.String);
    }

    private static bool IsExeGpuHigh(string exePath)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences");
            return (k?.GetValue(exePath) as string)?.Contains("GpuPreference=2") == true;
        }
        catch { return false; }
    }

    private void InitStream()
    {
        RefreshStreamStatus();
        BuildEncoderAdvice();
    }

    private void RefreshStreamStatus()
    {
        string? obs = FindObsExe();
        bool obsRunning = Process.GetProcessesByName("obs64").Length > 0;
        if (obs != null)
        {
            ObsStatusText.Text = $"OBS Studio detetado em {obs}" + (obsRunning ? " · ABERTO AGORA" : "");
            BtnObsAction.Content = obsRunning ? "OTIMIZAR OBS EM EXECUÇÃO (PRIORIDADE ALTA)" : "ABRIR OBS STUDIO";
            ChkObsAdmin.IsEnabled = ChkObsGpu.IsEnabled = true;
            ChkObsAdmin.IsChecked = IsExeRunAsAdmin(obs);
            ChkObsGpu.IsChecked = IsExeGpuHigh(obs);
        }
        else
        {
            ObsStatusText.Text = "OBS Studio não encontrado na pasta padrão.";
            BtnObsAction.Content = "INSTALAR OBS STUDIO (VIA WINGET)";
            ChkObsAdmin.IsEnabled = ChkObsGpu.IsEnabled = false;
        }

        string? vm = FindVoicemeeterExe();
        bool cable = HasVbCable();
        VmStatusText.Text = (vm != null ? "✔ Voicemeeter Banana instalado" : "✖ Voicemeeter Banana não instalado") +
                            "  ·  " +
                            (cable ? "✔ VB-Audio Virtual Cable instalado" : "✖ VB-Audio Virtual Cable não instalado");
        BtnVmAction.Content = vm != null ? "ABRIR VOICEMEETER BANANA" : "INSTALAR VOICEMEETER BANANA";
        BtnCableAction.Content = cable ? "VB-CABLE JÁ INSTALADO" : "INSTALAR VB-AUDIO CABLE";
        BtnCableAction.IsEnabled = !cable && !_streamBusy;
    }

    private void ChkObsAdmin_Click(object sender, RoutedEventArgs e)
    {
        string? obs = FindObsExe();
        if (obs == null) return;
        try
        {
            bool on = ChkObsAdmin.IsChecked == true;
            SetExeRunAsAdmin(obs, on);
            StreamResult.Text = on
                ? "OBS configurado para abrir sempre como Administrador (evita travadas na preview quando a GPU chega a 99%)."
                : "Execução como Administrador removida do OBS.";
        }
        catch (Exception ex) { StreamResult.Text = "Falhou: " + ex.Message; }
    }

    private void ChkObsGpu_Click(object sender, RoutedEventArgs e)
    {
        string? obs = FindObsExe();
        if (obs == null) return;
        try
        {
            bool on = ChkObsGpu.IsChecked == true;
            GameLauncher.SetGpuPreference(obs, on);
            StreamResult.Text = on
                ? "OBS configurado para usar sempre a GPU dedicada de Alto Desempenho (evita erro de ecrã preto na captura)."
                : "Preferência de GPU do OBS reposta.";
        }
        catch (Exception ex) { StreamResult.Text = "Falhou: " + ex.Message; }
    }

    private async void BtnObsAction_Click(object sender, RoutedEventArgs e)
    {
        if (_streamBusy) return;
        string? obs = FindObsExe();
        if (obs == null)
        {
            await InstallStreamToolAsync(new InstallableApp { Name = "OBS Studio", WingetId = "OBSProject.OBSStudio" });
            return;
        }

        var procs = Process.GetProcessesByName("obs64");
        if (procs.Length > 0)
        {
            int ok = 0;
            foreach (var p in procs)
            {
                try { p.PriorityClass = ProcessPriorityClass.High; ok++; } catch { }
                p.Dispose();
            }
            StreamResult.Text = ok > 0 ? "Prioridade Alta aplicada ao processo obs64.exe em execução!" : "Não foi possível alterar a prioridade do OBS.";
        }
        else
        {
            try
            {
                Process.Start(new ProcessStartInfo(obs)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(obs) ?? ""
                });
                StreamResult.Text = "A abrir o OBS Studio…";
            }
            catch (Exception ex) { StreamResult.Text = "Não foi possível abrir o OBS: " + ex.Message; }
        }
    }

    private async void BtnVmAction_Click(object sender, RoutedEventArgs e)
    {
        if (_streamBusy) return;
        string? vm = FindVoicemeeterExe();
        if (vm != null)
        {
            try { Process.Start(new ProcessStartInfo(vm) { UseShellExecute = true }); }
            catch (Exception ex) { StreamResult.Text = "Erro ao abrir o Voicemeeter: " + ex.Message; }
            return;
        }
        await InstallStreamToolAsync(new InstallableApp { Name = "Voicemeeter Banana", WingetId = "VB-Audio.Voicemeeter.Banana" });
    }

    private async void BtnCableAction_Click(object sender, RoutedEventArgs e)
    {
        if (_streamBusy) return;
        await InstallStreamToolAsync(new InstallableApp { Name = "VB-Audio Virtual Cable", WingetId = "VB-Audio.Cable" });
    }

    private void BtnWinSound_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:apps-volume") { UseShellExecute = true }); }
        catch { }
    }

    private async Task InstallStreamToolAsync(InstallableApp app)
    {
        if (!AppInstaller.WingetAvailable)
        {
            StreamResult.Text = "O winget não está disponível neste PC.";
            return;
        }
        _streamBusy = true;
        BtnObsAction.IsEnabled = BtnVmAction.IsEnabled = BtnCableAction.IsEnabled = false;
        StreamResult.Text = $"A transferir e instalar {app.Name} silenciosamente via winget… aguarde.";
        try
        {
            var (ok, msg) = await AppInstaller.InstallAsync(app, CancellationToken.None);
            StreamResult.Text = $"{app.Name}: {msg}" + (ok && app.WingetId.Contains("VB-Audio") ? " (Reinicie o PC se os novos canais de áudio ainda não aparecerem no Windows)." : "");
        }
        catch (Exception ex) { StreamResult.Text = $"Falhou: {ex.Message}"; }
        finally
        {
            _streamBusy = false;
            BtnObsAction.IsEnabled = BtnVmAction.IsEnabled = true;
            RefreshStreamStatus();
        }
    }

    private void BuildEncoderAdvice()
    {
        string gpu = _gpus.Count > 0 ? _gpus[0].Name : "Desconhecida";
        string vendor = _gpus.Count > 0 ? _gpus[0].Vendor : "";
        string cpu = GameCatalog.ShortCpuName(_cpuName);

        string encoder, preset, extra;
        if (vendor == "NVIDIA" || gpu.Contains("RTX", StringComparison.OrdinalIgnoreCase) || gpu.Contains("GTX", StringComparison.OrdinalIgnoreCase))
        {
            encoder = "NVIDIA NVENC H.264 (ou AV1/HEVC no YouTube se for RTX 40/50)";
            preset = "P5 (Bom) ou P6 (Mais qualidade) · Tuning: High Quality · Multipass: Quarter Resolution";
            extra = "Look-ahead: Desligado (para jogos de movimento rápido) · Psycho Visual Tuning: Ligado · B-Frames: 2";
        }
        else if (vendor == "AMD" || gpu.Contains("Radeon", StringComparison.OrdinalIgnoreCase) || gpu.Contains("RX ", StringComparison.OrdinalIgnoreCase))
        {
            encoder = "AMD HW H.264 (AVC) na Twitch · AMD HW HEVC / AV1 no YouTube";
            preset = "Quality · Profile: High";
            extra = "B-Frames: 2 (nas RX 6000/7000/9000 recentes)";
        }
        else
        {
            encoder = $"x264 (Processador: {cpu}) ou QuickSync H.264";
            preset = "veryfast (ou faster se a CPU tiver 8+ núcleos sobrando)";
            extra = "Profile: high · Tune: nenhum";
        }

        StreamEncoderList.ItemsSource = new[]
        {
            new KeyValuePair<string, string>("Placa gráfica detetada", gpu),
            new KeyValuePair<string, string>("Encoder recomendado", encoder),
            new KeyValuePair<string, string>("Controlo de Taxa (Rate Control)", "CBR (Taxa de bits constante)"),
            new KeyValuePair<string, string>("Bitrate Twitch (1080p 60fps / 936p 60fps)", "6000 Kbps (máx. 7500 Kbps)"),
            new KeyValuePair<string, string>("Bitrate YouTube (1080p / 1440p 60fps)", "10.000 a 18.000 Kbps (HEVC / AV1)"),
            new KeyValuePair<string, string>("Intervalo de Keyframe", "2 s"),
            new KeyValuePair<string, string>("Preset sugerido", preset),
            new KeyValuePair<string, string>("Ajustes extras", extra),
        };
    }
}