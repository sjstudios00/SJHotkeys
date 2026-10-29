using System.Diagnostics;
using System.Windows;
using SJHotkeys.Models;
using SJHotkeys.Services;

namespace SJHotkeys.Dialogs;

public partial class SettingsDialog : Window
{
    private const string RepositoryUrl = "https://github.com/sjstudios00/SJHotkeys";
    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly StartupService _startup = new();
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
}
