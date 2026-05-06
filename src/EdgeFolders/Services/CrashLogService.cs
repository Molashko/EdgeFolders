using System.IO;

namespace EdgeFolders.Services;

public static class CrashLogService
{
    private static readonly object SyncRoot = new();

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EdgeFolders",
        "logs");

    public static void Write(Exception exception, string source)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var path = Path.Combine(LogDirectory, $"crash-{DateTime.Now:yyyyMMdd}.log");
            var message = $"""
                [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source}
                {exception}

                """;

            lock (SyncRoot)
            {
                File.AppendAllText(path, message);
            }
        }
        catch
        {
            // Logging must never become the reason the launcher exits.
        }
    }
}
