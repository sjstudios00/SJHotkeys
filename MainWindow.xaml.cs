using System.Drawing;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
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
    private readonly NotifyIcon? _tray;
    private readonly UpdateService _updates = new();
    private readonly CancellationTokenSource _lifetime = new();
    private (Version Version, string Tag, string AssetUrl, string AssetName)? _availableUpdate;
    private bool _exiting;
    private bool _resourcesDisposed;
    public bool IsClosing { get; private set; }
    private bool _trayVisible;
    public bool LaunchMinimized { get; }

    public MainWindow()
    {
        InitializeComponent();
        var args = Environment.GetCommandLineArgs();
        _settings = _store.Load(out var isFirstRun);
        if (isFirstRun)
        {
            try
            {
                _startup.SetEnabled(true);
                SaveSettings();
            }
            catch (Exception ex) { StartupLogger.Write("Could not enable default first-run startup", ex); }
        }
        LaunchMinimized = _settings.LaunchMinimized || args.Any(x => x.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        _settings.Hotkeys ??= [];
        _hotkeys = new HotkeyService(this);
        _hotkeys.LaunchFailed += (_, ex) => Dispatcher.BeginInvoke(() =>
        {
            StartupLogger.Write("Could not launch a hotkey target", ex);
            MessageBox.Show(this, ex.Message, "Could not launch program", MessageBoxButton.OK, MessageBoxImage.Error);
        });
        _tray = CreateTrayIcon();
        HotkeyList.ItemsSource = _settings.Hotkeys;
        RefreshState();
        RegisterSavedHotkeys();
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        Loaded += (_, _) => _ = CheckForUpdatesInBackgroundAsync();
    }

    private NotifyIcon? CreateTrayIcon()
    {
        Icon? trayIcon = null;
        try
        {
            trayIcon = LoadTrayIcon();
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open SJ Hotkeys", null, (_, _) => ShowFromTray());
            menu.Items.Add("Exit", null, (_, _) => ExitApplication());
            var icon = new NotifyIcon { Text = "SJ Hotkeys", Icon = trayIcon, Visible = true, ContextMenuStrip = menu };
            icon.DoubleClick += (_, _) => ShowFromTray();
            icon.BalloonTipClicked += (_, _) => Dispatcher.BeginInvoke(() => OfferAvailableUpdate());
            icon.Disposed += (_, _) => { trayIcon.Dispose(); menu.Dispose(); };
            return icon;
        }
        catch (Exception ex)
        {
            trayIcon?.Dispose();
            StartupLogger.Write("Could not initialize the system tray icon", ex);
            return null;
        }
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/sj-studios-logo-cropped.png", UriKind.Absolute));
            if (resource is null) return (Icon)SystemIcons.Application.Clone();
            using var bitmap = new Bitmap(resource.Stream);
            using var scaled = new Bitmap(bitmap, new System.Drawing.Size(32, 32));
            var handle = scaled.GetHicon();
            try { return (Icon)System.Drawing.Icon.FromHandle(handle).Clone(); }
            finally { DestroyIcon(handle); }
        }
        catch (Exception ex)
        {
            StartupLogger.Write("Could not load the SJ Studios tray icon; using the Windows fallback icon", ex);
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    private void RegisterSavedHotkeys()
    {
        var active = 0;
        foreach (var entry in _settings.Hotkeys.ToArray())
        {
            if (!File.Exists(entry.ProgramPath))
            {
                StartupLogger.Write("A saved hotkey target no longer exists", new FileNotFoundException(entry.ProgramPath));
                continue;
            }
            if (!_hotkeys.Register(entry, out var error))
            {
                StartupLogger.Write($"Could not activate saved shortcut {entry.ShortcutText}", new InvalidOperationException(error));
                continue;
            }
            active++;
        }
        RefreshState(active);
    }

    private void AddHotkey_Click(object sender, RoutedEventArgs e) => EditHotkey(null);
    private void EditHotkey_Click(object sender, RoutedEventArgs e) => EditHotkey((sender as System.Windows.Controls.Button)?.Tag as HotkeyEntry);
    private bool EditHotkey(HotkeyEntry? old, string? initialProgramPath = null)
    {
        var dialog = new HotkeyDialog(old, initialProgramPath) { Owner = this };
        if (dialog.ShowDialog() != true) return false;
        if (_settings.Hotkeys.Any(x => x.Id != old?.Id && x.Modifiers == dialog.Modifiers && x.VirtualKey == dialog.VirtualKey))
        {
            MessageBox.Show(this, "This shortcut is already assigned in SJ Hotkeys.", "Shortcut already used", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        var updated = new HotkeyEntry { Id = old?.Id ?? Guid.NewGuid(), ProgramPath = dialog.ProgramPath, Modifiers = dialog.Modifiers, VirtualKey = dialog.VirtualKey };
        if (old is not null) _hotkeys.Unregister(old);
        if (!_hotkeys.Register(updated, out var error))
        {
            if (old is not null) _hotkeys.Register(old, out _);
            MessageBox.Show(this, error, "Shortcut unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (old is not null) _settings.Hotkeys.Remove(old);
        _settings.Hotkeys.Add(updated);
        SaveSettings();
        HotkeyList.ItemsSource = null;
        HotkeyList.ItemsSource = _settings.Hotkeys;
        RefreshState();
        return true;
    }

    private void MainWindow_DragEnter(object sender, System.Windows.DragEventArgs e) => UpdateDropFeedback(e);
    private void MainWindow_DragOver(object sender, System.Windows.DragEventArgs e) => UpdateDropFeedback(e);

    private void MainWindow_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private void UpdateDropFeedback(System.Windows.DragEventArgs e)
    {
        var paths = GetSupportedDropPaths(e);
        var canDrop = paths.Length > 0;
        DropOverlay.Visibility = canDrop ? Visibility.Visible : Visibility.Collapsed;
        e.Effects = canDrop ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void MainWindow_Drop(object sender, System.Windows.DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
        var paths = GetSupportedDropPaths(e);
        if (paths.Length == 0)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                MessageBox.Show(this, "Drop an .exe program or a Windows .lnk shortcut.", "Unsupported file", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var path in paths)
        {
            if (!EditHotkey(null, path)) break;
        }
    }

    private static string[] GetSupportedDropPaths(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) || e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths)
            return [];
        return paths.Where(path => File.Exists(path) && (Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))).ToArray();
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
        var settings = new SettingsDialog(_settings, SaveSettings, () => _settings.Hotkeys, ReplaceHotkeysFromProfile) { Owner = this };
        settings.ShowDialog();
    }

    private string? ReplaceHotkeysFromProfile(List<HotkeyEntry> imported)
    {
        var previous = _settings.Hotkeys;
        _hotkeys.UnregisterAll();

        foreach (var entry in imported)
        {
            if (!File.Exists(entry.ProgramPath)) continue;
            if (_hotkeys.Register(entry, out var error)) continue;

            _hotkeys.UnregisterAll();
            RestoreHotkeys(previous);
            return $"The profile could not be activated. {entry.ShortcutText}: {error} Your current hotkeys were kept.";
        }

        _settings.Hotkeys = imported;
        try
        {
            SaveSettings();
        }
        catch (Exception ex)
        {
            _settings.Hotkeys = previous;
            _hotkeys.UnregisterAll();
            RestoreHotkeys(previous);
            return $"The profile could not be saved: {ex.Message} Your current hotkeys were kept.";
        }

        HotkeyList.ItemsSource = null;
        HotkeyList.ItemsSource = _settings.Hotkeys;
        RefreshState();
        return null;
    }

    private void RestoreHotkeys(IEnumerable<HotkeyEntry> entries)
    {
        foreach (var entry in entries)
            if (File.Exists(entry.ProgramPath)) _hotkeys.Register(entry, out _);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private async Task CheckForUpdatesInBackgroundAsync()
    {
        if (_settings.LastUpdateCheckUtc is { } lastCheck && DateTimeOffset.UtcNow - lastCheck < TimeSpan.FromHours(24)) return;
        _settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
        try { SaveSettings(); } catch (Exception ex) { StartupLogger.Write("Could not save update check time", ex); }
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), _lifetime.Token);
            var update = await _updates.CheckAsync(_lifetime.Token);
            if (update is null) return;
            _availableUpdate = update;
            await Dispatcher.InvokeAsync(() =>
            {
                if (IsVisible) OfferAvailableUpdate();
                else _tray?.ShowBalloonTip(5000, "SJ Hotkeys update available", $"Version {update.Value.Tag} is ready. Click to review and install.", ToolTipIcon.Info);
            });
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            StartupLogger.Write("Background update check failed", ex);
        }
    }

    private async void OfferAvailableUpdate()
    {
        if (_availableUpdate is not { } update) return;
        var result = MessageBox.Show(this, $"SJ Hotkeys {update.Tag} is available. Download and install it now?\n\nThe update will be verified before installation.", "Update available", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            var downloaded = await _updates.DownloadAndVerifyAsync(update.AssetUrl, update.AssetName);
            UpdateService.StartInstaller(downloaded);
        }
        catch (Exception ex)
        {
            StartupLogger.Write("Update download or installation preparation failed", ex);
            MessageBox.Show(this, $"The update could not be prepared.\n\n{ex.Message}", "SJ Hotkeys update", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshState()
    {
        EmptyState.Visibility = _settings.Hotkeys.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var active = _hotkeys.ActiveCount;
        StatusText.Text = active == _settings.Hotkeys.Count
            ? active == 1 ? "1 shortcut active" : $"{active} shortcuts active"
            : $"{active} active · {_settings.Hotkeys.Count - active} need attention";
    }

    private void RefreshState(int activeCount)
    {
        EmptyState.Visibility = _settings.Hotkeys.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = activeCount == _settings.Hotkeys.Count
            ? activeCount == 1 ? "1 shortcut active" : $"{activeCount} shortcuts active"
            : $"{activeCount} active · {_settings.Hotkeys.Count - activeCount} need attention";
    }

    private void SaveSettings() => _store.Save(_settings);

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray && _tray is not null) HideToTray();
    }
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        IsClosing = true;
        if (!_exiting && _settings.MinimizeToTray && _tray is not null)
        {
            e.Cancel = true;
            IsClosing = false;
            HideToTray();
        }
        else if (!_exiting)
        {
            PrepareForShutdown();
        }
    }
    private void HideToTray()
    {
        if (_tray is null) return;
        Hide();
        WindowState = WindowState.Minimized;
        if (!_trayVisible) { _tray.Visible = true; _trayVisible = true; }
    }

    public void ShowInitially()
    {
        Show();
        if (LaunchMinimized && _tray is not null) HideToTray();
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Topmost = true;
        Topmost = false;
        Activate();
        Focus();
    }
    private void ExitApplication()
    {
        PrepareForShutdown();
        Application.Current.Shutdown();
    }

    public void PrepareForShutdown()
    {
        if (_resourcesDisposed) return;
        _resourcesDisposed = true;
        _exiting = true;
        _lifetime.Cancel();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _hotkeys.Dispose();
        try { SaveSettings(); }
        catch (Exception ex) { StartupLogger.Write("Could not save settings during shutdown", ex); }
        _lifetime.Dispose();
    }
}
