using System.Diagnostics;
using System.Windows;
using System.Reflection;
using MessageBox = System.Windows.MessageBox;
using SJHotkeys.Models;
using SJHotkeys.Services;

namespace SJHotkeys.Dialogs;

public partial class SettingsDialog : Window
{
    private const string RepositoryUrl = "https://github.com/sjstudios00/SJHotkeys";
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly StartupService _startup = new();
    private readonly UpdateService _updates = new();
    private bool _initializing = true;
    public SettingsDialog(AppSettings settings, Action save)
    {
        InitializeComponent();
        _settings = settings;
        _save = save;
        var version = typeof(SettingsDialog).Assembly.GetName().Version;
        VersionText.Text = $"Version {version?.Major ?? 1}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
        try { settings.StartWithWindows = _startup.IsEnabled(); } catch { }
        StartWithWindowsBox.IsChecked = settings.StartWithWindows;
        MinimizeToTrayBox.IsChecked = settings.MinimizeToTray;
        LaunchMinimizedBox.IsChecked = settings.LaunchMinimized;
        _initializing = false;
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
