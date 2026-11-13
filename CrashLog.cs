namespace ItSupportToolbox;

internal static class CrashLog
{
    public static string FilePath =>
        Path.Combine(Path.GetTempPath(), "it-support-toolbox-crash.log");

    public static void Write(Exception? exception)
    {
        try
        {
            File.AppendAllText(FilePath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Unhandled exception{Environment.NewLine}" +
                $"{exception}{Environment.NewLine}{new string('-', 70)}{Environment.NewLine}");
        }
        catch
        {
            // logging must never throw
        }
    }
}
