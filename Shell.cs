using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ItSupportToolbox;

internal sealed record ToolResult(string Title, string Output, bool? Success, DateTime Time, TimeSpan Duration)
{
    public string StatusText => Success switch
    {
        true => "OK",
        false => "FAIL",
        _ => "INFO"
    };

    public string PlainText =>
        $"[{Time:HH:mm:ss}] [{StatusText}] {Title}{Environment.NewLine}{Output}{Environment.NewLine}{Environment.NewLine}";
}

internal sealed class Shell
{
    private static readonly Encoding NativeEncoding = CreateNativeEncoding();
    private const int DefaultTimeoutMs = 120_000;

    static Shell()
    {
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
        catch { /* ignore */ }
    }

    private static Encoding CreateNativeEncoding()
    {
        try
        {
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch
        {
            return Encoding.Latin1;
        }
    }

    public async Task<(int Code, string Output)> ExecNative(string exe, IReadOnlyList<string> args, int timeoutMs = DefaultTimeoutMs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = NativeEncoding,
            StandardErrorEncoding = NativeEncoding,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (string arg in args)
            psi.ArgumentList.Add(arg);

        return await ExecAsync(psi, timeoutMs);
    }

    public async Task<(int Code, string Output)> ExecPs(string script, int timeoutMs = DefaultTimeoutMs)
    {
        string full = BuildPowerShellScript(script);

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        psi.ArgumentList.Add("-NoLogo");
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(full);

        return await ExecAsync(psi, timeoutMs);
    }

    private static async Task<(int Code, string Output)> ExecAsync(ProcessStartInfo psi, int timeoutMs)
    {
        using var process = new Process { StartInfo = psi };

        try
        {
            if (!process.Start())
                return (1, "Failed to start process.");
        }
        catch (Exception ex)
        {
            return (1, "Failed to start process: " + ex.Message);
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return (124, $"Timed out after {timeoutMs / 1000} seconds.");
        }

        string output = (await stdout).TrimEnd();
        string error = (await stderr).TrimEnd();

        if (!string.IsNullOrWhiteSpace(error))
        {
            output = string.IsNullOrWhiteSpace(output)
                ? error
                : output + Environment.NewLine + Environment.NewLine + "[stderr]" + Environment.NewLine + error;
        }

        return (process.ExitCode, output);
    }

    public async Task<ToolResult> Native(string title, string exe, params string[] args)
        => await Native(title, DefaultTimeoutMs, exe, args);

    public async Task<ToolResult> Native(string title, int timeoutMs, string exe, params string[] args)
    {
        var sw = Stopwatch.StartNew();
        var (code, output) = await ExecNative(exe, args, timeoutMs);
        sw.Stop();

        bool ok = code == 0;
        if (string.IsNullOrWhiteSpace(output))
            output = ok ? "(no output)" : $"Command exited with code {code}.";

        return new ToolResult(title, output, ok, DateTime.Now, sw.Elapsed);
    }

    public async Task<ToolResult> Ps(string title, string script, int timeoutMs = DefaultTimeoutMs)
    {
        var sw = Stopwatch.StartNew();
        var (code, output) = await ExecPs(script, timeoutMs);
        sw.Stop();

        bool failed = output.StartsWith("ERROR:", StringComparison.Ordinal) || code != 0;
        if (string.IsNullOrWhiteSpace(output))
            output = failed ? $"Command exited with code {code}." : "(no output)";

        return new ToolResult(title, output, !failed, DateTime.Now, sw.Elapsed);
    }

    private static string BuildPowerShellScript(string body)
    {
        return
"""
$ProgressPreference = 'SilentlyContinue'
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$__out = [Console]::OpenStandardOutput()
$__enc = New-Object System.Text.UTF8Encoding($false)
$__w = New-Object System.IO.StreamWriter($__out, $__enc)
$__w.AutoFlush = $true
try {
    $__result = & {
""" + "\n" + body + "\n" +
"""
    }
    $__text = $__result | Out-String -Width 4096
    $__w.Write($__text)
} catch {
    $__w.Write('ERROR: ' + $_.Exception.Message)
} finally {
    $__w.Flush()
}
""";
    }
}
