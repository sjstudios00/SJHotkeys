using System.Text.Json.Serialization;
using System.Windows.Input;
using System.IO;

namespace SJHotkeys.Models;

public sealed class HotkeyEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProgramPath { get; set; } = "";
    public uint VirtualKey { get; set; }
    public uint Modifiers { get; set; }
    public string DisplayName => Path.GetFileNameWithoutExtension(ProgramPath);
    [JsonIgnore]
    public string ShortcutText => ShortcutFormatter.Format(Modifiers, VirtualKey);
}

public static class ShortcutFormatter
{
    public static string Format(uint modifiers, uint key)
    {
        var parts = new List<string>();
        if ((modifiers & 2) != 0) parts.Add("Ctrl");
        if ((modifiers & 1) != 0) parts.Add("Alt");
        if ((modifiers & 4) != 0) parts.Add("Shift");
        if ((modifiers & 8) != 0) parts.Add("Win");
        var keyName = KeyInterop.KeyFromVirtualKey((int)key).ToString();
        if (keyName.StartsWith("D") && keyName.Length == 2 && char.IsDigit(keyName[1])) keyName = keyName[1..];
        else if (keyName.StartsWith("NumPad")) keyName = keyName[6..];
        else if (keyName.StartsWith("Oem")) keyName = ((System.Windows.Forms.Keys)key).ToString();
        parts.Add(keyName);
        return string.Join(" + ", parts);
    }
}
