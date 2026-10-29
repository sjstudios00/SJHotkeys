using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.IO;
using SJHotkeys.Models;

namespace SJHotkeys.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private readonly HwndSource _source;
    private readonly Dictionary<int, HotkeyEntry> _entries = [];
    private int _nextId = 0x1000;
    public int ActiveCount => _entries.Count;
    public event EventHandler<Exception>? LaunchFailed;
    public HotkeyService(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle)!;
        _source.AddHook(WndProc);
    }
    public bool Register(HotkeyEntry entry, out string? error)
    {
        var duplicate = _entries.Values.FirstOrDefault(x => x.Modifiers == entry.Modifiers && x.VirtualKey == entry.VirtualKey);
        if (duplicate is not null)
        {
            error = "This shortcut is already assigned in SJ Hotkeys.";
            return false;
        }
        var id = _nextId++;
        if (!RegisterHotKey(_source.Handle, id, entry.Modifiers | ModNoRepeat, entry.VirtualKey))
        {
            error = Marshal.GetLastWin32Error() == 1409 ? "This shortcut is already in use by Windows or another app." : "Windows could not register this shortcut.";
            return false;
        }
        _entries[id] = entry;
        error = null;
        return true;
    }
    public void UnregisterAll()
    {
        foreach (var id in _entries.Keys.ToArray()) UnregisterHotKey(_source.Handle, id);
        _entries.Clear();
    }
    public void Unregister(HotkeyEntry entry)
    {
        var item = _entries.FirstOrDefault(x => ReferenceEquals(x.Value, entry) || x.Value.Id == entry.Id);
        if (item.Key == 0) return;
        UnregisterHotKey(_source.Handle, item.Key);
        _entries.Remove(item.Key);
    }
    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmHotkey && _entries.TryGetValue(wParam.ToInt32(), out var entry))
        {
            handled = true;
            _ = LaunchAsync(entry.ProgramPath);
        }
        return nint.Zero;
    }
    private async Task LaunchAsync(string path)
    {
        try
        {
            if (!File.Exists(path)) throw new FileNotFoundException("The selected program could not be found.", path);
            await Task.Run(() => Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }));
        }
        catch (Exception ex) { LaunchFailed?.Invoke(this, ex); }
    }
    public void Dispose() { UnregisterAll(); _source.RemoveHook(WndProc); }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(nint hWnd, int id);
}
