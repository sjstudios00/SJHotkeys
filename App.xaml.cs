using System.Windows;
using SJHotkeys.Services;

namespace SJHotkeys;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private SingleInstanceService? _singleInstance;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.Acquired)
        {
            _singleInstance.SignalExisting();
            Shutdown();
            return;
        }
        _singleInstance.ActivateRequested += (_, _) => Dispatcher.Invoke(() => _mainWindow?.ShowFromTray());
        _mainWindow = new MainWindow();
        if (!_mainWindow.LaunchMinimized) _mainWindow.Show();
        _mainWindow.Closed += (_, _) => Shutdown();
    }
}
