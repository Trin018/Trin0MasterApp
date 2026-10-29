using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResetExplode;

/// <summary>Aba Overclock: ajustes da GPU (NVML), verificador de estabilidade e guia de overclock na BIOS.</summary>
public partial class MainWindow
{
    public sealed record GuideStep(string Number, string Title, string Where, string Text);

    private uint? _ocClockCap;
    private uint? _ocPendingCap;
    private CancellationTokenSource? _ocCts;
    private bool _ocRunning;

    private nint OcGpu => _gpus.Count > 0 ? _gpus[0].NvmlHandle : IntPtr.Zero;

    private void InitOc()
    {
        nint ocGpu = OcGpu;
        OcGpuTitle.Text = _gpus.Count > 0 ? ("GPU · " + GameCatalog.ShortGpuName(_gpus[0].Name).ToUpperInvariant()) : "GPU";
        if (ocGpu == IntPtr.Zero)
        {
            OcGpuState.Text = "Ajustes disponíveis só para placas NVIDIA (via driver oficial).";
            OcPower.IsEnabled = BtnOcApply.IsEnabled = false;
        }
        else
        {
            if (Nvml.PowerConstraintsMw(ocGpu) is var (minMw, maxMw))
            {
                OcPower.Minimum = minMw / 1000.0;
                OcPower.Maximum = maxMw / 1000.0;
            }
            if (Nvml.PowerLimitMw(ocGpu) is uint limMw)
                OcPower.Value = limMw / 1000.0;

            uint maxClk = Nvml.MaxClock(ocGpu, 0).GetValueOrDefault(3000u);
            Chip("Sem teto", null, on: true);
            Chip($"Leve · {maxClk - 300} MHz", maxClk - 300, on: false);
            Chip($"Equilibrado · {maxClk - 500} MHz", maxClk - 500, on: false);
            Chip($"Econômico · {maxClk - 800} MHz", maxClk - 800, on: false);
            UpdateOcGpuState();
        }
        BuildOcGuide();
        UpdateOcHistory();
        UpdateRecentErrors();

        void Chip(string label, uint? cap, bool on)
        {
            var rb = new RadioButton
            {
                Content = label,
                GroupName = "OcClock",
                Style = (Style)FindResource("Chip"),
                IsChecked = on
            };
            rb.Checked += (_, _) => _ocPendingCap = cap;
            OcClockPanel.Children.Add(rb);
        }
    }

    private void UpdateOcGpuState()
    {
        nint ocGpu = OcGpu;
        if (ocGpu == IntPtr.Zero) return;
        string lim = Nvml.PowerLimitMw(ocGpu) is uint l ? $"{l / 1000.0:0} W" : "—";
        string def = Nvml.PowerDefaultMw(ocGpu) is uint d ? $"{d / 1000.0:0} W" : "—";
        string cap = _ocClockCap is uint c ? $"{c} MHz" : "nenhum";
        string cur = Nvml.Clock(ocGpu, 0) is uint k ? $"{k} MHz" : "—";
        OcGpuState.Text = $"Limite de energia atual {lim} (padrão {def}) · teto de clock: {cap} · clock máximo atual: {cur}";
    }

    private void OcPower_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OcPowerValue != null)
            OcPowerValue.Text = $"{OcPower.Value:0} W";
    }

    private void BtnOcApply_Click(object sender, RoutedEventArgs e)
    {
        nint ocGpu = OcGpu;
        if (ocGpu == IntPtr.Zero || !RequireAdmin()) return;

        var list = new List<string>();
        int code = Nvml.SetPowerLimitMw(ocGpu, (uint)(OcPower.Value * 1000.0));
        string? err1 = Nvml.Explain(code);
        list.Add(err1 != null ? $"Energia: não aplicado ({err1})." : $"Energia: {OcPower.Value:0} W aplicado.");

        int code2 = _ocPendingCap is uint cap ? Nvml.LockGpuClocks(ocGpu, 210u, cap) : Nvml.ResetGpuClocks(ocGpu);
        string? err2 = Nvml.Explain(code2);
        if (err2 != null)
        {
            list.Add($"Teto de clock: não aplicado ({err2}).");
        }
        else
        {
            _ocClockCap = _ocPendingCap;
            list.Add(_ocClockCap is uint applied ? $"Teto de clock: {applied} MHz aplicado." : "Teto de clock: removido.");
        }
        OcGpuResult.Text = string.Join(" ", list) + " Rode o verificador para confirmar a estabilidade.";
        UpdateOcGpuState();
    }

    private void BtnOcReset_Click(object sender, RoutedEventArgs e)
    {
        nint ocGpu = OcGpu;
        if (ocGpu == IntPtr.Zero || !RequireAdmin()) return;

        uint? defMw = Nvml.PowerDefaultMw(ocGpu);
        int code = defMw is uint d ? Nvml.SetPowerLimitMw(ocGpu, d) : 3;
        int code2 = Nvml.ResetGpuClocks(ocGpu);
        if (defMw is uint d2) OcPower.Value = d2 / 1000.0;

        foreach (var rb in OcClockPanel.Children.OfType<RadioButton>())
            rb.IsChecked = (rb.Content as string) == "Sem teto";
        if (code2 == 0) _ocClockCap = null;

        string? err = Nvml.Explain(code) ?? Nvml.Explain(code2);
        OcGpuResult.Text = err != null ? $"Restauração parcial: {err}." : "GPU de volta ao padrão de fábrica.";
        UpdateOcGpuState();
    }

    private void UpdateRecentErrors()
    {
        var errs = OcCheck.ErrorsSince(DateTime.Now.AddDays(-7.0));
        OcRecentErrors.Text = errs.Error != null
            ? "Não foi possível ler o registro: " + errs.Error
            : errs.Total == 0
                ? "Nenhum erro de hardware nem reinício do driver de vídeo nos últimos 7 dias."
                : $"Últimos 7 dias: {errs.Whea} erros WHEA · {errs.GpuResets} reinícios do driver de vídeo.\nÚltimo: {errs.LastDetail}";
    }

    private void UpdateOcHistory()
    {
        var source = _settings.BenchRecords.Where(r => r.Test == "oc").ToList();
        OcHistory.ItemsSource = source.Select(r => new MeterItem($"{r.Date:dd/MM HH:mm}", r.Summary, r.Score)).ToList();
    }

    private async void BtnOcCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_ocRunning) { _ocCts?.Cancel(); return; }
        if (TestRunning || AnyBenchRunning) { OcStatus.Text = "Termine o outro teste antes."; return; }

        string target = CheckedTag(OcTargetPanel) ?? "cpu";
        bool cpu = target is "cpu" or "both";
        bool gpu = target is "gpu" or "both";
        TimeSpan duration = TimeSpan.FromMinutes(int.Parse(CheckedTag(OcDurPanel) ?? "15"));
        string label = OcLabel.Text.Trim().Length > 0 ? OcLabel.Text.Trim() : "sem descrição";

        _ocCts = new CancellationTokenSource();
        var ct = _ocCts.Token;
        _ocRunning = true;
        BtnOcCheck.Content = "CANCELAR";
        BtnOcCheck.Style = (Style)FindResource("OutlineButton");
        OcVerdictCard.Visibility = Visibility.Collapsed;
        OcTargetPanel.IsEnabled = OcDurPanel.IsEnabled = false;

        DateTime start = DateTime.Now;
        double? cpuScore = null, gpuScore = null;
        float maxCpu = 0f, maxGpu = 0f;
        double sumCpuW = 0.0, sumGpuW = 0.0;
        int nCpuW = 0, nGpuW = 0;
        string? fail = null;
        var stress = new StressTester();
        var burn = new GpuBurn();

        try
        {
            if (cpu)
            {
                OcStatus.Text = "Medindo a nota da CPU (render multi-core)…";
                var br = await Task.Run(() => _bench.Run(Environment.ProcessorCount, TimeSpan.Zero, ct), ct);
                if (!ct.IsCancellationRequested) cpuScore = br.Score;
            }
            if (gpu && !ct.IsCancellationRequested)
            {
                OcStatus.Text = "Medindo a nota da GPU (cena de jogo, 30 s)…";
                var gb = new GpuGameBench
                {
                    Duration = TimeSpan.FromSeconds(30.0),
                    AdapterName = _gpus.Count > 0 ? _gpus[0].Name : ""
                };
                _gpuBenchActive = true;
                using (ct.Register(gb.Cancel))
                {
                    var gr = await Task.Factory.StartNew(gb.Run, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    if (gr != null) gpuScore = gr.Score;
                    else if (!gb.Cancelled) fail = $"a cena de GPU falhou ({gb.Error})";
                }
                _gpuBenchActive = false;
            }
            if (fail == null && !ct.IsCancellationRequested)
            {
                if (cpu) stress.Start(cpu: true, ram: true);
                if (gpu)
                {
                    burn.Start(new GpuBurnSettings
                    {
                        Width = 1920, Height = 1080, Msaa = 4, Shells = 128, Iterations = 48,
                        AdapterName = _gpus.Count > 0 ? _gpus[0].Name : ""
                    });
                }
                var sw = Stopwatch.StartNew();
                while (sw.Elapsed < duration && !ct.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                    float? ctNow = _monitor.CpuTemperature;
                    float? gtNow = MainGpuTemp();
                    if (ctNow is float cVal) maxCpu = Math.Max(maxCpu, cVal);
                    if (gtNow is float gVal) maxGpu = Math.Max(maxGpu, gVal);
                    if (cpu && _monitor.CpuPower is float cw) { sumCpuW += cw; nCpuW++; }
                    if (gpu && MainGpuPower() is float gw) { sumGpuW += gw; nGpuW++; }

                    OcProgress.ScaleX = sw.Elapsed / duration;
                    OcStatus.Text = $"Stress {(cpu && gpu ? "CPU + RAM + GPU" : cpu ? "CPU + RAM" : "GPU")} · {sw.Elapsed:mm\\:ss} de {duration:mm\\:ss} · máx. CPU {(maxCpu > 0f ? $"{maxCpu:0} °C" : "—")} · máx. GPU {(maxGpu > 0f ? $"{maxGpu:0} °C" : "—")}";

                    if (cpu && ctNow >= 95f) { fail = $"CPU chegou a {ctNow:0} °C (limite de segurança 95 °C)"; break; }
                    if (gpu && gtNow >= 88f) { fail = $"GPU chegou a {gtNow:0} °C (limite de segurança 88 °C)"; break; }
                    if (cpu && stress.MemoryErrors > 0) { fail = $"{stress.MemoryErrors} erros de memória (RAM/EXPO instável)"; break; }
                    if (gpu && !burn.Running) { fail = burn.Error != null ? $"a GPU falhou ({burn.Error})" : "a janela do teste de GPU foi fechada"; break; }
                    if ((int)sw.Elapsed.TotalSeconds % 5 == 0)
                    {
                        var errs = OcCheck.ErrorsSince(start);
                        if (errs.Total > 0) { fail = $"o Windows registrou {errs.Whea} erros WHEA e {errs.GpuResets} reinícios do driver de vídeo"; break; }
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            stress.Stop();
            burn.Stop();
            _gpuBenchActive = false;
        }

        _ocRunning = false;
        BtnOcCheck.Content = "INICIAR VERIFICAÇÃO";
        BtnOcCheck.Style = (Style)FindResource("PrimaryButton");
        OcTargetPanel.IsEnabled = OcDurPanel.IsEnabled = true;
        OcProgress.ScaleX = 0.0;

        if (ct.IsCancellationRequested && fail == null)
        {
            OcStatus.Text = "Verificação cancelada.";
            return;
        }

        var finalErrs = OcCheck.ErrorsSince(start);
        if (fail == null && finalErrs.Total > 0)
            fail = $"o Windows registrou {finalErrs.Whea} erros WHEA e {finalErrs.GpuResets} reinícios do driver de vídeo";

        bool ok = fail == null;
        OcStatus.Text = $"Concluído em {DateTime.Now - start:mm\\:ss}.";
        OcVerdict.Text = ok ? "ESTÁVEL" : "INSTÁVEL";
        OcVerdictWhy.Text = ok
            ? $"Nenhum erro em {duration.TotalMinutes:0} min de stress. Para Curve Optimizer, use o PC normalmente por um dia e confira os erros recentes."
            : $"Motivo: {fail}. Volte um passo no ajuste e teste de novo.";
        OcVerdictCard.Background = ok ? Card : Fg;
        OcVerdict.Foreground = OcVerdictWhy.Foreground = ok ? Fg : Ink;

        var list = new List<KeyValuePair<string, string>> { new("Configuração", label) };
        if (cpuScore is double cs) list.Add(new("Nota CPU (render)", $"{cs:N0} pts"));
        if (gpuScore is double gs) list.Add(new("Nota GPU (cena)", $"{gs:N0} pts"));
        if (maxCpu > 0f) list.Add(new("Temp. máx. CPU", $"{maxCpu:0} °C"));
        if (maxGpu > 0f) list.Add(new("Temp. máx. GPU", $"{maxGpu:0} °C"));
        if (nCpuW > 0) list.Add(new("Consumo médio CPU", $"{sumCpuW / nCpuW:0} W"));
        if (nGpuW > 0) list.Add(new("Consumo médio GPU", $"{sumGpuW / nGpuW:0} W"));
        list.Add(new("Erros WHEA / resets de vídeo", $"{finalErrs.Whea} / {finalErrs.GpuResets}"));

        OcResult.ItemsSource = list;
        OcResult.Foreground = ok ? Fg : Ink;
        OcVerdictCard.Visibility = Visibility.Visible;

        string summary = (ok ? "estável" : "INSTÁVEL") + " · " + label
                       + (cpuScore is double cs2 ? $" · CPU {cs2:N0}" : "")
                       + (gpuScore is double gs2 ? $" · GPU {gs2:N0}" : "");
        _settings.BenchRecords.Insert(0, new BenchRecord(DateTime.Now, "oc", ok ? 1.0 : 0.15, summary));
        _settings.Save();
        UpdateOcHistory();
        UpdateRecentErrors();
    }

    private void BuildOcGuide()
    {
        var (maker, model) = OcCheck.Motherboard();
        bool giga = maker.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase);
        bool asus = maker.Contains("ASUS", StringComparison.OrdinalIgnoreCase);
        bool msi = maker.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase) || maker.Contains("MSI", StringComparison.OrdinalIgnoreCase);
        string cpuShort = GameCatalog.ShortCpuName(_cpuName);
        bool ryzen = _cpuName.Contains("Ryzen", StringComparison.OrdinalIgnoreCase);
        string boardName = ((giga ? "Gigabyte" : (maker.Split(' ').FirstOrDefault() ?? "")) + " " + model).Trim();

        OcGuideTitle.Text = "GUIA: OVERCLOCK NA BIOS · " + boardName.ToUpperInvariant() + " · " + cpuShort.ToUpperInvariant();
        OcGuideIntro.Text = "Faça uma mudança por vez e rode o verificador depois de cada uma. Os menus podem mudar um pouco com a versão da BIOS.";

        string pboPath = giga ? "Tweaker → Advanced CPU Settings → Precision Boost Overdrive  (ou Settings → AMD Overclocking)"
                       : asus ? "Advanced → AMD Overclocking → Precision Boost Overdrive  (ou AI Tweaker → PBO)"
                       : msi  ? "OC → Advanced CPU Configuration → AMD Overclocking → Precision Boost Overdrive"
                              : "Advanced → AMD Overclocking → Precision Boost Overdrive";
        string expoPath = giga ? "Tweaker → Extreme Memory Profile (X.M.P.) → EXPO1"
                        : asus ? "AI Tweaker → Ai Overclock Tuner → EXPO I"
                        : msi  ? "OC → A-XMP/EXPO → Profile 1"
                               : "procure EXPO / XMP no menu de memória";

        var list = new List<GuideStep>
        {
            new("0", "Antes de começar", giga ? "Save & Exit → Save Profiles" : "Save & Exit → Save Profile",
                "Salve o perfil atual da BIOS. Se o PC não ligar depois de um ajuste, espere 3 tentativas ou limpe a CMOS."),
            new("1", "Ative o perfil da memória (EXPO / XMP)", expoPath,
                "É o maior ganho em jogos para a maioria dos PCs: a RAM sai do padrão lento e vai para a velocidade da etiqueta. Teste com o verificador.")
        };
        if (ryzen)
        {
            list.Add(new("2", "Precision Boost Overdrive (PBO)", pboPath,
                "Precision Boost Overdrive → Advanced. PBO Limits → Motherboard. Libera os limites de energia para o boost durar mais."));
            list.Add(new("3", "Curve Optimizer (undervolt da CPU)", pboPath + " → Curve Optimizer",
                "All Cores → Negative → 10. Teste. Se passar, tente 15, 20, 25… até falhar; volte 5 a partir de onde falhou."));
            list.Add(new("4", "Max CPU Boost Clock Override (opcional)", pboPath,
                "Depois do Curve Optimizer estável: +100 MHz, teste; depois +200 MHz."));
            list.Add(new("5", "Alternativa fria: Eco Mode", pboPath + " → Eco Mode",
                "Limita a CPU a 65 W (ou 105 W). Perde quase nada em jogos e roda bem mais frio e silencioso."));
        }
        else
        {
            list.Add(new("2", "Processador Intel", "Tweaker / Ai Tweaker / OC → CPU",
                "Para Intel, prefira undervolt (offset negativo de tensão) e limites de energia (PL1/PL2) em vez de aumentar multiplicadores."));
        }
        OcGuide.ItemsSource = list;
    }
}