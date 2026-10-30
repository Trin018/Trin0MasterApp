using System.Net;
using System.Text;
using System.Text.Json;

namespace ResetExplode;

public sealed record ReportNode(string Name, string? Value, List<ReportNode>? Children);
public sealed record ReportSensor(string Hardware, string Category, string Name, string Value, string Min, string Max);
public sealed record ReportBench(DateTime Date, string Test, string Summary);
public sealed record ReportDisk(string Name, string Details, string Verdict, string Reasons, List<KeyValuePair<string, string>> Metrics);
public sealed record ReportGpu(string Name, string Subtitle, List<ReportNode> Sections);

public sealed class ReportData
{
    public DateTime Date { get; init; } = DateTime.Now;
    public string Machine { get; init; } = Environment.MachineName;
    public string AppVersion { get; init; } = "";
    public string? LogoBase64 { get; init; }
    public List<ReportNode> System { get; init; } = new();
    public List<ReportGpu> Gpus { get; init; } = new();
    public List<ReportDisk> Disks { get; init; } = new();
    public List<ReportDisk> Power { get; init; } = new();
    public List<ReportDisk> Network { get; init; } = new();
    public List<ReportSensor> Sensors { get; init; } = new();
    public List<ReportBench> Benchmarks { get; init; } = new();
}

public static class ReportBuilder
{
    public static ReportNode? ToNode(InfoNode n)
    {
        string h = n.Header ?? "";
        if (h.Contains("série", StringComparison.OrdinalIgnoreCase) || h.Contains("serial", StringComparison.OrdinalIgnoreCase)) return null;
        var kids = n.Children.Select(ToNode).Where(k => k != null).Cast<ReportNode>().ToList();
        return new ReportNode(h, string.IsNullOrEmpty(n.Value) ? null : n.Value, kids.Count > 0 ? kids : null);
    }

    public static string Csv(ReportData d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Tipo;Grupo;Componente / Nome;Valor;Detalhes / Min;Max");

        foreach (var s in d.Sensors)
            sb.AppendLine($"Sensor;{EscCsv(s.Hardware)};{EscCsv(s.Name)};{EscCsv(s.Value)};{EscCsv(s.Min)};{EscCsv(s.Max)}");

        foreach (var disk in d.Disks)
            sb.AppendLine($"Disco;{EscCsv(disk.Verdict)};{EscCsv(disk.Name)};{EscCsv(disk.Details)};{EscCsv(disk.Reasons)};");

        foreach (var g in d.Gpus)
            sb.AppendLine($"GPU;Placa Gráfica;{EscCsv(g.Name)};{EscCsv(g.Subtitle)};;");

        foreach (var b in d.Benchmarks)
            sb.AppendLine($"Benchmark;{b.Date:yyyy-MM-dd HH:mm};{EscCsv(b.Test)};{EscCsv(b.Summary)};;");

        return sb.ToString();
    }

    private static string EscCsv(string s) => $"\"{s.Replace("\"", "\"\"")}\"";

    public static string Json(ReportData d) => JsonSerializer.Serialize(new
    {
        gerado_em = d.Date.ToString("s"),
        computador = d.Machine,
        versao_do_programa = d.AppVersion,
        sistema = d.System,
        placas_de_video = d.Gpus,
        discos = d.Disks.Select(x => new
        {
            x.Name, x.Details, saude = x.Verdict, x.Reasons,
            metricas = x.Metrics.ToDictionary(m => m.Key, m => m.Value)
        }),
        energia = d.Power.Select(x => new { x.Name, x.Details, estado = x.Verdict, x.Reasons, metricas = x.Metrics.ToDictionary(m => m.Key, m => m.Value) }),
        rede = d.Network.Select(x => new { x.Name, x.Details, estado = x.Verdict, x.Reasons, metricas = x.Metrics.ToDictionary(m => m.Key, m => m.Value) }),
        sensores = d.Sensors,
        benchmarks = d.Benchmarks,
    }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static void Section(StringBuilder sb, string title, List<ReportDisk> items)
    {
        if (items.Count == 0) return;
        sb.Append($"<section><h2>{E(title)}</h2>");
        foreach (var disk in items)
        {
            string cls = disk.Verdict switch { "SAUDÁVEL" or "ESTÁVEL" or "EM USO" => "ok", "SEM DADOS" => "na", _ => "bad" };
            sb.Append($"<div class=\"card\"><div class=\"row\"><b>{E(disk.Name)}</b><span class=\"badge {cls}\">{E(disk.Verdict)}</span></div>");
            if (disk.Details.Length > 0) sb.Append($"<p class=\"muted\">{E(disk.Details)}</p>");
            sb.Append($"<p>{E(disk.Reasons)}</p>");
            if (disk.Metrics.Count > 0)
            {
                sb.Append("<table>");
                foreach (var m in disk.Metrics) sb.Append($"<tr><td class=\"k\">{E(m.Key)}</td><td>{E(m.Value)}</td></tr>");
                sb.Append("</table>");
            }
            sb.Append("</div>");
        }
        sb.Append("</section>");
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    public static string Html(ReportData d)
    {
        var sb = new StringBuilder(64 * 1024);
        sb.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append($"<title>Trin0 Master APP · Relatório · {E(d.Machine)}</title><style>{Css}</style></head><body><div class=\"wrap\">");

        sb.Append("<header>");
        if (!string.IsNullOrEmpty(d.LogoBase64)) sb.Append($"<img class=\"logo\" alt=\"\" src=\"data:image/png;base64,{d.LogoBase64}\">");
        sb.Append($"<div><h1>Trin0 Master APP</h1><p class=\"sub\">Relatório de hardware · {E(d.Machine)} · {d.Date:dd/MM/yyyy HH:mm}");
        if (d.AppVersion.Length > 0) sb.Append($" · v{E(d.AppVersion)}");
        sb.Append("</p></div></header>");
        sb.Append("<p class=\"note\">Números de série foram omitidos deste relatório para privacidade.</p>");

        Section(sb, "Saúde dos discos", d.Disks);
        Section(sb, "Bateria e energia", d.Power);
        Section(sb, "Teste de rede", d.Network);

        if (d.System.Count > 0)
        {
            sb.Append("<section><h2>Sistema</h2>");
            foreach (var n in d.System) { sb.Append($"<div class=\"card\"><h3>{E(n.Name)}</h3>"); Tree(sb, n.Children, 0); sb.Append("</div>"); }
            sb.Append("</section>");
        }

        foreach (var g in d.Gpus)
        {
            sb.Append($"<section><h2>Placa de vídeo · {E(g.Name)}</h2>");
            if (g.Subtitle.Length > 0) sb.Append($"<p class=\"muted\">{E(g.Subtitle)}</p>");
            foreach (var s in g.Sections) { sb.Append($"<div class=\"card\"><h3>{E(s.Name)}</h3>"); Tree(sb, s.Children, 0); sb.Append("</div>"); }
            sb.Append("</section>");
        }

        if (d.Sensors.Count > 0)
        {
            sb.Append("<section><h2>Sensores (leitura no momento da exportação)</h2>");
            foreach (var grp in d.Sensors.GroupBy(s => s.Hardware))
            {
                sb.Append($"<div class=\"card\"><h3>{E(grp.Key)}</h3><table><tr><th>Categoria</th><th>Sensor</th><th>Valor</th><th>Mín.</th><th>Máx.</th></tr>");
                foreach (var s in grp)
                    sb.Append($"<tr><td class=\"muted\">{E(s.Category)}</td><td>{E(s.Name)}</td><td class=\"num\">{E(s.Value)}</td><td class=\"num muted\">{E(s.Min)}</td><td class=\"num muted\">{E(s.Max)}</td></tr>");
                sb.Append("</table></div>");
            }
            sb.Append("</section>");
        }

        if (d.Benchmarks.Count > 0)
        {
            sb.Append("<section><h2>Benchmarks e testes recentes</h2><div class=\"card\"><table><tr><th>Data</th><th>Teste</th><th>Resultado</th></tr>");
            foreach (var b in d.Benchmarks)
                sb.Append($"<tr><td class=\"muted\">{b.Date:dd/MM/yyyy HH:mm}</td><td>{E(b.Test)}</td><td>{E(b.Summary)}</td></tr>");
            sb.Append("</table></div></section>");
        }

        sb.Append("<footer>Gerado pelo Trin0 Master APP.</footer>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static void Tree(StringBuilder sb, List<ReportNode>? nodes, int depth)
    {
        if (nodes == null || nodes.Count == 0) return;
        sb.Append(depth == 0 ? "<table>" : "<table class=\"nested\">");
        foreach (var n in nodes)
        {
            if (n.Children != null)
            {
                sb.Append($"<tr><td colspan=\"2\" class=\"grp\">{E(n.Name)}{(n.Value != null ? " · " + E(n.Value) : "")}</td></tr><tr><td colspan=\"2\" class=\"inner\">");
                Tree(sb, n.Children, depth + 1);
                sb.Append("</td></tr>");
            }
            else sb.Append($"<tr><td class=\"k\">{E(n.Name)}</td><td>{E(n.Value)}</td></tr>");
        }
        sb.Append("</table>");
    }

    private const string Css = @"
:root{--bg:#fff;--fg:#111;--muted:#666;--line:#e3e3e3;--card:#fafafa;--ok:#0284c7;--badbg:#e11d48;--badfg:#fff}
@media (prefers-color-scheme:dark){:root{--bg:#07090e;--fg:#f8fafc;--muted:#94a3b8;--line:#1e293b;--card:#111622;--ok:#00e5ff;--badbg:#e11d48;--badfg:#fff}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:14px/1.5 'Segoe UI',system-ui,sans-serif}
.wrap{max-width:980px;margin:0 auto;padding:32px 20px 60px}
header{display:flex;gap:16px;align-items:center;margin-bottom:6px}.logo{width:44px;height:44px}
h1{margin:0;font-size:26px;letter-spacing:.5px}h2{margin:34px 0 12px;font-size:17px;letter-spacing:.4px}h3{margin:0 0 8px;font-size:14px}
.sub,.muted{color:var(--muted)}.sub{margin:2px 0 0}.note{color:var(--muted);font-size:12px;margin:0 0 4px}
.card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:14px 16px;margin:0 0 12px}
.row{display:flex;justify-content:space-between;gap:12px;align-items:center}
.badge{font-size:11px;font-weight:700;letter-spacing:.6px;padding:3px 10px;border-radius:4px;border:1px solid var(--ok);color:var(--ok)}
.badge.bad,.badge.na{background:var(--badbg);color:var(--badfg);border-color:var(--badbg)}.badge.na{opacity:.6}
table{width:100%;border-collapse:collapse}td,th{padding:6px 8px;border-bottom:1px solid var(--line);vertical-align:top;text-align:left}
th{color:var(--ok);font-weight:700;font-size:11px}.k{color:var(--muted);width:38%}.num{font-family:Consolas,monospace;text-align:right;white-space:nowrap}
.grp{font-weight:700;padding-top:12px}.inner{padding:0 0 0 14px;border-bottom:0}.nested td{font-size:13px}
footer{margin-top:40px;color:var(--muted);font-size:12px}
@media print{body{background:#fff;color:#000}.card{break-inside:avoid}}
";
}