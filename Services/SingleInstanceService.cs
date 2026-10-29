using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SJHotkeys.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Local\\SJStudios.SJHotkeys.SingleInstance";
    private const string PipeName = "SJStudios.SJHotkeys.Activate";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();
    private int _listenerFailureLogged;
    public bool Acquired { get; }
    public event EventHandler? ActivateRequested;

    public SingleInstanceService()
    {
        _mutex = new Mutex(true, MutexName, out var created);
        Acquired = created;
        if (Acquired) _ = ListenAsync(_stop.Token);
    }

    public bool SignalExisting()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.None, System.Security.Principal.TokenImpersonationLevel.Identification);
                client.Connect(300);
                if (GetNamedPipeServerProcessId(client.SafePipeHandle, out var processId))
                    AllowSetForegroundWindow(processId);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine("activate");
                writer.Flush();
                return true;
            }
            catch (TimeoutException) when (attempt < 4) { Thread.Sleep(100); }
            catch (IOException) when (attempt < 4) { Thread.Sleep(100); }
            catch { return false; }
        }
        return false;
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token);
                using var reader = new StreamReader(server);
                if (await reader.ReadLineAsync(token) == "activate")
                    ActivateRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref _listenerFailureLogged, 1) == 0)
                    StartupLogger.Write("Named-pipe activation listener failed", ex);
                try { await Task.Delay(250, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (Acquired) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _stop.Dispose();
    }

    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);
}
