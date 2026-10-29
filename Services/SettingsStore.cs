using System.Text.Json;
using System.IO;
using SJHotkeys.Models;

namespace SJHotkeys.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string FilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SJ Studios", "SJ Hotkeys", "settings.json");
    public AppSettings Load(out bool isFirstRun)
    {
        isFirstRun = !File.Exists(FilePath);
        try
        {
            if (!isFirstRun) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
            return new AppSettings { StartWithWindows = true };
        }
        catch (Exception ex)
        {
            StartupLogger.Write("Could not read local settings", ex);
            return new();
        }
    }
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, Options));
        File.Move(tempPath, FilePath, true);
    }
}
