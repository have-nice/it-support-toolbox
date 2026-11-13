using System.Diagnostics;
using System.Security.Principal;

namespace ItSupportToolbox;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            SelfTest.RunAsync(args).GetAwaiter().GetResult();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => CrashLog.Write(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashLog.Write(e.ExceptionObject as Exception);

        bool noElevate = args.Any(a => a.Equals("--no-elevate", StringComparison.OrdinalIgnoreCase));
        if (!Elevation.IsAdmin() && !noElevate && Elevation.TryRelaunchElevated(args))
            return;

        Application.Run(new MainForm(Elevation.IsAdmin()));
    }
}

internal static class Elevation
{
    public static bool IsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryRelaunchElevated(string[] args)
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
                return false;

            if (Path.GetFileName(exe).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                return false;

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas"
            };

            foreach (string arg in args)
                psi.ArgumentList.Add(arg);

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
