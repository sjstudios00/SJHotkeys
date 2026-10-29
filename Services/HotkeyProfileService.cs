using System.IO;
using System.Text.Json;
using SJHotkeys.Models;

namespace SJHotkeys.Services;

public static class HotkeyProfileService
{
    private const int CurrentFormatVersion = 1;
    private const int MaxHotkeys = 500;
    private const long MaxProfileBytes = 1_048_576;
    private const uint AllowedModifiers = 0x0F;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Export(string path, IEnumerable<HotkeyEntry> hotkeys)
    {
        var profile = new HotkeyProfile
        {
            FormatVersion = CurrentFormatVersion,
            Hotkeys = hotkeys.Select(entry => new HotkeyProfileEntry
            {
                ProgramPath = entry.ProgramPath,
                Modifiers = entry.Modifiers,
                VirtualKey = entry.VirtualKey
            }).ToList()
        };
        File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
    }

    public static List<HotkeyEntry> Import(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException("The selected profile could not be found.", path);
        if (file.Length > MaxProfileBytes) throw new InvalidDataException("The profile file is too large.");

        var profile = JsonSerializer.Deserialize<HotkeyProfile>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The profile file is empty or invalid.");
        if (profile.FormatVersion != CurrentFormatVersion)
            throw new InvalidDataException($"This profile format version is not supported (version {profile.FormatVersion}).");
        if (profile.Hotkeys is null || profile.Hotkeys.Count > MaxHotkeys)
            throw new InvalidDataException($"A profile can contain at most {MaxHotkeys} hotkeys.");

        var combinations = new HashSet<(uint Modifiers, uint VirtualKey)>();
        var result = new List<HotkeyEntry>(profile.Hotkeys.Count);
        foreach (var item in profile.Hotkeys)
        {
            if (string.IsNullOrWhiteSpace(item.ProgramPath) || item.ProgramPath.Length > 32767)
                throw new InvalidDataException("A profile contains an invalid program path.");
            var extension = Path.GetExtension(item.ProgramPath);
            if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Profiles may only contain .exe programs and .lnk shortcuts.");
            if (item.Modifiers == 0 || (item.Modifiers & ~AllowedModifiers) != 0 || item.VirtualKey is 0 or > 0xFE)
                throw new InvalidDataException("A profile contains an invalid keyboard shortcut.");
            if (!combinations.Add((item.Modifiers, item.VirtualKey)))
                throw new InvalidDataException("The profile assigns the same keyboard shortcut more than once.");

            result.Add(new HotkeyEntry
            {
                ProgramPath = item.ProgramPath,
                Modifiers = item.Modifiers,
                VirtualKey = item.VirtualKey
            });
        }

        return result;
    }

    private sealed class HotkeyProfile
    {
        public HotkeyProfile() { }
        public int FormatVersion { get; set; }
        public List<HotkeyProfileEntry>? Hotkeys { get; set; }
    }

    private sealed class HotkeyProfileEntry
    {
        public HotkeyProfileEntry() { }
        public string ProgramPath { get; set; } = "";
        public uint Modifiers { get; set; }
        public uint VirtualKey { get; set; }
    }
}
