using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Reflection;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;
using SJHotkeys.Models;
using SJHotkeys.Services;

namespace SJHotkeys.Dialogs;

public partial class SettingsDialog : Window
{
    private const string RepositoryUrl = "https://github.com/sjstudios00/SJHotkeys";
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly Func<IReadOnlyList<HotkeyEntry>> _getHotkeys;
    private readonly Func<List<HotkeyEntry>, string?> _importHotkeys;
    private readonly StartupService _startup = new();
    private readonly UpdateService _updates = new();
    private bool _initializing = true;
    public SettingsDialog(AppSettings settings, Action save, Func<IReadOnlyList<HotkeyEntry>> getHotkeys, Func<List<HotkeyEntry>, string?> importHotkeys)
    {
        InitializeComponent();
        _settings = settings;
        _save = save;
        _getHotkeys = getHotkeys;
        _importHotkeys = importHotkeys;
        var version = typeof(SettingsDialog).Assembly.GetName().Version;
        VersionText.Text = $"Version {version?.Major ?? 1}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
        try { settings.StartWithWindows = _startup.IsEnabled(); } catch { }
        StartWithWindowsBox.IsChecked = settings.StartWithWindows;
        MinimizeToTrayBox.IsChecked = settings.MinimizeToTray;
        LaunchMinimizedBox.IsChecked = settings.LaunchMinimized;
        _initializing = false;
    }

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export hotkey profile",
            Filter = "SJ Hotkeys profile (*.sjhotkeys)|*.sjhotkeys|JSON file (*.json)|*.json",
            DefaultExt = ".sjhotkeys",
            AddExtension = true,
            FileName = "SJ-Hotkeys-Profile"
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            HotkeyProfileService.Export(picker.FileName, _getHotkeys());
            MessageBox.Show(this, $"Exported {_getHotkeys().Count} hotkeys to the profile file.", "Profile exported", MessageBoxButton.OK, MessageBoxImage.Information);
            ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Could not export the profile: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import hotkey profile",
            Filter = "SJ Hotkeys profile (*.sjhotkeys;*.json)|*.sjhotkeys;*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != true) return;

        if (MessageBox.Show(this, "Importing a profile replaces all hotkeys currently in SJ Hotkeys. Continue?", "Replace current hotkeys?", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            var imported = HotkeyProfileService.Import(picker.FileName);
            var error = _importHotkeys(imported);
            if (error is not null)
            {
                ErrorText.Text = error;
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            var missingPrograms = imported.Count(entry => !File.Exists(entry.ProgramPath));
            var message = $"Imported {imported.Count} hotkeys successfully.";
            if (missingPrograms > 0)
                message += $"\n\n{missingPrograms} program path(s) are not available on this PC. Their shortcuts are saved but cannot start until the files are available.";
            MessageBox.Show(this, message, "Profile imported", MessageBoxButton.OK, MessageBoxImage.Information);
            ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Could not import the profile: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        try
        {
            var start = StartWithWindowsBox.IsChecked == true;
            _startup.SetEnabled(start);
            _settings.StartWithWindows = start;
            _settings.MinimizeToTray = MinimizeToTrayBox.IsChecked == true;
            _settings.LaunchMinimized = LaunchMinimizedBox.IsChecked == true;
            ErrorText.Visibility = Visibility.Collapsed;
            _save();
        }
        catch (Exception ex)
        {
            _settings.StartWithWindows = !StartWithWindowsBox.IsChecked.GetValueOrDefault();
            StartWithWindowsBox.IsChecked = _settings.StartWithWindows;
            ErrorText.Text = $"Could not update startup setting: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
    private void Source_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var startInfo = new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true };
            Process.Start(startInfo);
        }
        catch (Exception ex) { ErrorText.Text = $"Could not open the source page: {ex.Message}"; ErrorText.Visibility = Visibility.Visible; }
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender;
        button.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;
        UpdateStatusText.Text = "Checking for updates…";
        UpdateStatusText.Visibility = Visibility.Visible;
        try
        {
            var update = await _updates.CheckAsync();
            if (update is null)
            {
                var current = Assembly.GetEntryAssembly()?.GetName().Version;
                UpdateStatusText.Text = _updates.LatestReleaseExists
                    ? $"You're up to date ({current?.Major}.{current?.Minor}.{current?.Build})."
                    : "No public release is available yet.";
                return;
            }
            var result = MessageBox.Show(this, $"SJ Hotkeys {update.Value.Tag} is available. Download and install it now?\n\nThe update will be verified before installation.", "Update available", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result != MessageBoxResult.Yes)
            {
                UpdateStatusText.Text = $"Version {update.Value.Tag} is available.";
                return;
            }
            UpdateStatusText.Text = "Downloading and verifying update…";
            var downloaded = await _updates.DownloadAndVerifyAsync(update.Value.AssetUrl, update.Value.AssetName);
            UpdateService.StartInstaller(downloaded);
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            UpdateStatusText.Text = "No public release is available yet.";
            UpdateStatusText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Update check failed: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
            UpdateStatusText.Visibility = Visibility.Collapsed;
        }
        finally { button.IsEnabled = true; }
    }
}
