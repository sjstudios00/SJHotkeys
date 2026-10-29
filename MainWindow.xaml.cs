using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Forms;
using SJHotkeys.Dialogs;
using SJHotkeys.Models;
using SJHotkeys.Services;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace SJHotkeys;

public partial class MainWindow : Window
{
    private readonly SettingsStore _store = new();
    private readonly StartupService _startup = new();
    private readonly AppSettings _settings;
    private readonly HotkeyService _hotkeys;
    private readonly NotifyIcon _tray;
    private bool _exiting;
    private bool _trayVisible;
    public bool LaunchMinimized { get; }

    public MainWindow()
    {
        InitializeComponent();
        var args = Environment.GetCommandLineArgs();
        _settings = _store.Load();
        LaunchMinimized = _settings.LaunchMinimized || args.Any(x => x.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        if (args.Any(x => x.Equals("--minimized", StringComparison.OrdinalIgnoreCase))) _settings.LaunchMinimized = true;
        _settings.Hotkeys ??= [];
        _hotkeys = new HotkeyService(this);
        _hotkeys.LaunchFailed += (_, ex) => Dispatcher.Invoke(() => MessageBox.Show(this, ex.Message, "Could not launch program", MessageBoxButton.OK, MessageBoxImage.Error));
        _tray = CreateTrayIcon();
        HotkeyList.ItemsSource = _settings.Hotkeys;
        RefreshState();
        RegisterSavedHotkeys();
        SourceInitialized += (_, _) => { if (LaunchMinimized) HideToTray(); };
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
    }

    private NotifyIcon CreateTrayIcon()
    {
        var trayIcon = LoadTrayIcon();
        var icon = new NotifyIcon { Text = "SJ Hotkeys", Icon = trayIcon, Visible = true, ContextMenuStrip = new ContextMenuStrip() };
        icon.ContextMenuStrip.Items.Add("Open SJ Hotkeys", null, (_, _) => ShowFromTray());
        icon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => ExitApplication());
        icon.DoubleClick += (_, _) => ShowFromTray();
        icon.Disposed += (_, _) => trayIcon.Dispose();
        return icon;
    }

    private static Icon LoadTrayIcon()
    {
        var resource = Application.GetResourceStream(new Uri("/SJHotkeys;component/Assets/sj-studios-logo-cropped.png", UriKind.Relative));
        if (resource is null) return SystemIcons.Application;
        using var bitmap = new Bitmap(resource.Stream);
        using var scaled = new Bitmap(bitmap, new System.Drawing.Size(32, 32));
        var handle = scaled.GetHicon();
        try { return (Icon)System.Drawing.Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    private void RegisterSavedHotkeys()
    {
        var valid = new List<HotkeyEntry>();
        foreach (var entry in _settings.Hotkeys.ToArray())
        {
            if (!File.Exists(entry.ProgramPath))
            {
                MessageBox.Show(this, $"The program for {entry.ShortcutText} could not be found:\n{entry.ProgramPath}", "Program unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                continue;
            }
            if (!_hotkeys.Register(entry, out var error))
            {
                MessageBox.Show(this, $"Could not activate {entry.ShortcutText}: {error}", "Shortcut unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                continue;
            }
            valid.Add(entry);
        }
        if (valid.Count != _settings.Hotkeys.Count)
        {
            // Preserve entries that could not be registered so they can be corrected later.
            HotkeyList.ItemsSource = null;
            HotkeyList.ItemsSource = _settings.Hotkeys;
            RefreshState();
        }
    }

    private void AddHotkey_Click(object sender, RoutedEventArgs e) => EditHotkey(null);
    private void EditHotkey_Click(object sender, RoutedEventArgs e) => EditHotkey((sender as System.Windows.Controls.Button)?.Tag as HotkeyEntry);
    private void EditHotkey(HotkeyEntry? old)
    {
        var dialog = new HotkeyDialog(old) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (_settings.Hotkeys.Any(x => x.Id != old?.Id && x.Modifiers == dialog.Modifiers && x.VirtualKey == dialog.VirtualKey))
        {
            MessageBox.Show(this, "This shortcut is already assigned in SJ Hotkeys.", "Shortcut already used", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var updated = new HotkeyEntry { Id = old?.Id ?? Guid.NewGuid(), ProgramPath = dialog.ProgramPath, Modifiers = dialog.Modifiers, VirtualKey = dialog.VirtualKey };
        if (old is not null) _hotkeys.Unregister(old);
        if (!_hotkeys.Register(updated, out var error))
        {
            if (old is not null) _hotkeys.Register(old, out _);
            MessageBox.Show(this, error, "Shortcut unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (old is not null) _settings.Hotkeys.Remove(old);
        _settings.Hotkeys.Add(updated);
        SaveSettings();
        HotkeyList.ItemsSource = null;
        HotkeyList.ItemsSource = _settings.Hotkeys;
        RefreshState();
    }

    private void DeleteHotkey_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as System.Windows.Controls.Button)?.Tag is not HotkeyEntry entry) return;
        var result = MessageBox.Show(this, $"Remove the shortcut for {entry.DisplayName}?", "Delete hotkey", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        _hotkeys.Unregister(entry);
        _settings.Hotkeys.Remove(entry);
        SaveSettings();
        HotkeyList.ItemsSource = null;
        HotkeyList.ItemsSource = _settings.Hotkeys;
        RefreshState();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsDialog(_settings, SaveSettings) { Owner = this };
        settings.ShowDialog();
    }

    private void RefreshState()
    {
        EmptyState.Visibility = _settings.Hotkeys.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = _settings.Hotkeys.Count == 1 ? "1 shortcut active" : $"{_settings.Hotkeys.Count} shortcuts active";
    }

    private void SaveSettings() => _store.Save(_settings);

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray) HideToTray();
    }
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_exiting && _settings.MinimizeToTray)
        {
            e.Cancel = true;
            HideToTray();
        }
        else if (!_exiting) ExitApplication();
    }
    private void HideToTray()
    {
        Hide();
        WindowState = WindowState.Minimized;
        if (!_trayVisible) { _tray.Visible = true; _trayVisible = true; }
    }
    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
    private void ExitApplication()
    {
        _exiting = true;
        _tray.Visible = false;
        _tray.Dispose();
        _hotkeys.Dispose();
        SaveSettings();
        Close();
        Application.Current.Shutdown();
    }
}
