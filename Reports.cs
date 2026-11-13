namespace ItSupportToolbox;

internal sealed class ReportStore
{
    private readonly List<ToolResult> _items = new();

    public event Action? Changed;

    public IReadOnlyList<ToolResult> Items
    {
        get
        {
            lock (_items)
                return _items.ToArray();
        }
    }

    public void Add(ToolResult result)
    {
        lock (_items)
            _items.Add(result);
        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (_items)
            _items.Clear();
        Changed?.Invoke();
    }
}

internal static class ReportWriter
{
    public static string PlainText(IReadOnlyList<ToolResult> items, string machine, bool admin)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("IT SUPPORT TOOLBOX - DIAGNOSTIC REPORT");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine($"Generated : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Computer  : {machine}");
        sb.AppendLine($"Account   : {(admin ? "Administrator" : "Standard user")}");
        sb.AppendLine($"Checks    : {items.Count}");
        sb.AppendLine();

        int ok = items.Count(i => i.Success == true);
        int fail = items.Count(i => i.Success == false);
        int info = items.Count(i => i.Success is null);
        sb.AppendLine($"Summary   : {ok} passed, {fail} failed, {info} informational");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine();

        foreach (var item in items)
        {
            sb.AppendLine($"[{item.Time:HH:mm:ss}] [{item.StatusText}] {item.Title}");
            sb.AppendLine(item.Output);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string Html(IReadOnlyList<ToolResult> items, string machine, bool admin)
    {
        int ok = items.Count(i => i.Success == true);
        int fail = items.Count(i => i.Success == false);
        int info = items.Count(i => i.Success is null);

        var sb = new System.Text.StringBuilder();
        sb.Append("""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>IT Support Toolbox - Diagnostic Report</title>
<style>
  :root { color-scheme: light; }
  * { box-sizing: border-box; }
  body { margin: 0; padding: 32px; background: #eef1f6; color: #1a2230;
         font-family: "Segoe UI", system-ui, -apple-system, sans-serif; }
  .wrap { max-width: 1100px; margin: 0 auto; }
  header { background: #10141c; color: #eef2fa; border-radius: 16px; padding: 28px 32px; }
  header h1 { margin: 0 0 6px; font-size: 24px; letter-spacing: .3px; }
  header p { margin: 0; color: #9aa6bb; font-size: 14px; }
  .meta { display: flex; flex-wrap: wrap; gap: 10px 26px; margin-top: 18px; font-size: 13px; color: #c8d2e3; }
  .meta b { color: #fff; font-weight: 600; }
  .summary { display: flex; flex-wrap: wrap; gap: 14px; margin: 20px 0 8px; }
  .stat { background: #fff; border-radius: 12px; padding: 14px 20px; min-width: 120px;
          box-shadow: 0 1px 2px rgba(16,20,28,.08); border-top: 4px solid #4c8dff; }
  .stat .n { font-size: 24px; font-weight: 700; }
  .stat .l { font-size: 12px; color: #67728a; text-transform: uppercase; letter-spacing: .6px; }
  .stat.ok { border-top-color: #22a86e; }
  .stat.fail { border-top-color: #d84b4b; }
  .stat.info { border-top-color: #8a93a6; }
  .check { background: #fff; border-radius: 12px; margin: 14px 0; overflow: hidden;
           box-shadow: 0 1px 2px rgba(16,20,28,.08); border-left: 5px solid #8a93a6; }
  .check.ok { border-left-color: #22a86e; }
  .check.fail { border-left-color: #d84b4b; }
  .check.info { border-left-color: #4c8dff; }
  .check h2 { margin: 0; padding: 14px 20px 0; font-size: 15px; display: flex;
              justify-content: space-between; align-items: baseline; gap: 16px; }
  .check .badge { font-size: 11px; font-weight: 700; letter-spacing: .6px; color: #67728a; }
  .check .time { font-size: 11px; color: #98a2b6; font-weight: 400; }
  pre { margin: 10px 0 0; padding: 12px 20px 18px; font-family: Consolas, "Cascadia Mono", monospace;
        font-size: 12.5px; line-height: 1.45; white-space: pre-wrap; word-break: break-word; }
  footer { text-align: center; color: #8a93a6; font-size: 12px; margin: 30px 0 0; }
  @media print { body { background: #fff; padding: 0; } .check, .stat { box-shadow: none; } }
</style>
</head>
<body>
<div class="wrap">
<header>
  <h1>IT Support Toolbox</h1>
  <p>Diagnostic report</p>
  <div class="meta">
    <span>Generated: <b>
""");
        sb.Append(System.Net.WebUtility.HtmlEncode(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.Append("</b></span><span>Computer: <b>");
        sb.Append(System.Net.WebUtility.HtmlEncode(machine));
        sb.Append("</b></span><span>Account: <b>");
        sb.Append(admin ? "Administrator" : "Standard user");
        sb.Append("</b></span><span>Checks: <b>");
        sb.Append(items.Count);
        sb.Append("""
</b></span>
  </div>
</header>

<div class="summary">
  <div class="stat ok"><div class="n">
""");
        sb.Append(ok);
        sb.Append("""
</div><div class="l">Passed</div></div>
  <div class="stat fail"><div class="n">
""");
        sb.Append(fail);
        sb.Append("""
</div><div class="l">Failed</div></div>
  <div class="stat info"><div class="n">
""");
        sb.Append(info);
        sb.Append("""
</div><div class="l">Informational</div></div>
</div>
""");

        foreach (var item in items)
        {
            string cls = item.Success switch { true => "ok", false => "fail", _ => "info" };
            sb.Append($"<section class=\"check {cls}\">");
            sb.Append("<h2><span>");
            sb.Append(System.Net.WebUtility.HtmlEncode(item.Title));
            sb.Append("</span><span class=\"time\">");
            sb.Append(item.Time.ToString("HH:mm:ss"));
            sb.Append(" &middot; ");
            sb.Append(item.StatusText);
            sb.Append("</span></h2><pre>");
            sb.Append(System.Net.WebUtility.HtmlEncode(item.Output));
            sb.Append("</pre></section>\n");
        }

        sb.Append("""
<footer>Generated by IT Support Toolbox &middot; portable Windows diagnostics</footer>
</div>
</body>
</html>
""");

        return sb.ToString();
    }

    public static string SuggestedFileName(string extension)
        => $"IT-Support-Report-{Environment.MachineName}-{DateTime.Now:yyyyMMdd-HHmm}.{extension}";
}
