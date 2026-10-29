namespace SJHotkeys.Models;

public sealed class AppSettings
{
    public List<HotkeyEntry> Hotkeys { get; set; } = [];
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool LaunchMinimized { get; set; }
}
