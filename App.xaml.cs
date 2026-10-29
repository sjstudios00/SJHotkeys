using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using SJHotkeys.Services;
using MessageBox = System.Windows.MessageBox;

namespace SJHotkeys;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private SingleInstanceService? _singleInstance;
    private readonly Stopwatch _startupTimer = Stopwatch.StartNew();

    private void OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        try
        {
            _singleInstance = new SingleInstanceService();
            if (!_singleInstance.Acquired)
            {
                if (!_singleInstance.SignalExisting())
                {
                    var error = new TimeoutException("The existing SJ Hotkeys instance did not respond to the activation request.");
                    StartupLogger.Write("Second-instance activation failed", error);
                    MessageBox.Show($"SJ Hotkeys is already running, but its window could not be activated. You can open it from the system tray.\n\nLog: {StartupLogger.LogFilePath}",
                        "SJ Hotkeys is already running", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                Shutdown();
                return;
            }
            _singleInstance.ActivateRequested += (_, _) => Dispatcher.BeginInvoke(() => _mainWindow?.ShowFromTray());
            _mainWindow = new MainWindow();
            _mainWindow.Closed += (_, _) => Shutdown();
            _mainWindow.ShowInitially();
            StartupLogger.WriteStartupDuration(_startupTimer.Elapsed);
        }
        catch (Exception ex)
        {
            StartupLogger.Write("Application startup failed", ex);
            MessageBox.Show($"SJ Hotkeys could not start.\n\n{ex.Message}\n\nA diagnostic log was saved here:\n{StartupLogger.LogFilePath}",
                "SJ Hotkeys startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupLogger.Write("Unhandled UI exception", e.Exception);
        MessageBox.Show($"SJ Hotkeys encountered an unexpected error.\n\n{e.Exception.Message}\n\nDetails were saved to:\n{StartupLogger.LogFilePath}",
            "SJ Hotkeys error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception) StartupLogger.Write("Unhandled process exception", exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        StartupLogger.Write("Unobserved background task exception", e.Exception);
        e.SetObserved();
    }
}
