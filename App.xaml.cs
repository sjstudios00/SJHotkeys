using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using SJHotkeys.Services;
using MessageBox = System.Windows.MessageBox;
using System.IO;

namespace SJHotkeys;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private SingleInstanceService? _singleInstance;
    private readonly Stopwatch _startupTimer = Stopwatch.StartNew();

    private void OnStartup(object sender, StartupEventArgs e)
    {
        if (e.Args.Length == 4 && e.Args[0] == "--apply-update")
        {
            ApplyUpdateAndExit(e.Args[1], e.Args[2], e.Args[3]);
            return;
        }
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

    private void ApplyUpdateAndExit(string targetPath, string downloadedPath, string processIdText)
    {
        try
        {
            var runningPath = Path.GetFullPath(Environment.ProcessPath ?? throw new InvalidOperationException("The application path is unavailable."));
            var requestedTarget = Path.GetFullPath(targetPath);
            if (!string.Equals(runningPath, requestedTarget, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The update target does not match the running application.");
            var updateDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SJ Hotkeys", "updates")) + Path.DirectorySeparatorChar;
            var requestedDownload = Path.GetFullPath(downloadedPath);
            if (!requestedDownload.StartsWith(updateDirectory, StringComparison.OrdinalIgnoreCase) || !requestedDownload.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The downloaded update is outside the expected update folder.");
            if (!int.TryParse(processIdText, out var processId)) throw new ArgumentException("Invalid update process id.");
            using (var oldProcess = Process.GetProcessById(processId))
            {
                if (!oldProcess.WaitForExit(30_000)) throw new TimeoutException("SJ Hotkeys did not close in time to install the update.");
            }
            var replaced = false;
            for (var attempt = 0; attempt < 30; attempt++)
            {
                try
                {
                    File.Copy(requestedDownload, runningPath, overwrite: true);
                    replaced = true;
                    break;
                }
                catch (IOException) { Thread.Sleep(250); }
            }
            if (!replaced) throw new IOException("The application file could not be replaced after it closed.");
            Process.Start(new ProcessStartInfo(runningPath) { UseShellExecute = true });
            try { File.Delete(requestedDownload); } catch { }
            Shutdown();
        }
        catch (Exception ex)
        {
            StartupLogger.Write("Update installation failed", ex);
            MessageBox.Show($"The update could not be installed. Please close SJ Hotkeys and try again.\n\n{ex.Message}", "SJ Hotkeys update", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindow?.PrepareForShutdown();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupLogger.Write("Unhandled UI exception", e.Exception);
        if (_mainWindow?.IsClosing == true || Current?.Dispatcher.HasShutdownStarted == true)
        {
            e.Handled = true;
            return;
        }
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
