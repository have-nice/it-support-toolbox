using System.Text;

namespace ItSupportToolbox;

internal static class SelfTest
{
    public static async Task RunAsync(string[] args)
    {
        var shell = new Shell();
        var diagnostics = new Diagnostics(shell);
        var results = new List<ToolResult>();

        async Task Run(string name, Func<Task<ToolResult>> action)
        {
            try
            {
                results.Add(await action());
            }
            catch (Exception ex)
            {
                results.Add(new ToolResult(name, "EXCEPTION: " + ex, false, DateTime.Now, TimeSpan.Zero));
            }
        }

        await Run("Flush DNS cache", diagnostics.FlushDns);
        await Run("Adapter overview", diagnostics.AdapterSummary);
        await Run("Ping default gateway", diagnostics.PingGateway);
        await Run("Test internet connectivity", diagnostics.TestInternet);
        await Run("Check DNS resolution", () => diagnostics.CheckDns("www.google.com"));
        await Run("PC specifications", diagnostics.Specs);
        await Run("Disk health (SMART)", diagnostics.DiskHealth);
        await Run("Critical Windows services", diagnostics.CriticalServices);
        await Run("Security and firewall status", diagnostics.SecurityStatus);
        await Run("Event log and crash history", diagnostics.SystemEvents);
        await Run("Startup items and installed programs", diagnostics.StartupAndPrograms);

        var sb = new StringBuilder();
        sb.AppendLine("IT Support Toolbox self-test");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Admin: {Elevation.IsAdmin()}");
        sb.AppendLine($"Results: {results.Count}");
        sb.AppendLine(new string('=', 70));
        sb.AppendLine();

        foreach (var result in results)
        {
            sb.AppendLine($"[{result.StatusText}] {result.Title}  ({result.Duration.TotalSeconds:0.0}s)");
            sb.AppendLine(result.Output);
            sb.AppendLine(new string('-', 70));
            sb.AppendLine();
        }

        int index = Array.FindIndex(args, a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase));
        string path = index >= 0 && args.Length > index + 1
            ? args[index + 1]
            : Path.Combine(Path.GetTempPath(), "itsupporttoolbox-selftest.txt");

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
