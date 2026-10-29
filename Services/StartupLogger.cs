using System.IO;

namespace SJHotkeys.Services;

public static class StartupLogger
{
    private static readonly object Sync = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SJ Studios", "SJ Hotkeys", "logs", "startup.log");
    private const long MaxLogSize = 512 * 1024;

    public static void Write(string context, Exception exception)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > MaxLogSize)
                    File.Move(LogPath, LogPath + ".1", overwrite: true);
                File.AppendAllText(LogPath,
                    $"[{DateTimeOffset.Now:O}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never prevent startup or shutdown.
        }
    }

    public static void WriteStartupDuration(TimeSpan elapsed)
    {
        // A single diagnostic line is written only when startup is unusually slow.
        if (elapsed < TimeSpan.FromSeconds(2)) return;
        Write("Slow startup", new TimeoutException($"Application initialization took {elapsed.TotalMilliseconds:F0} ms."));
    }

    public static string LogFilePath => LogPath;
}
