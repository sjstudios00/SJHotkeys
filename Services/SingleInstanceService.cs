using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace SJHotkeys.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Local\\SJStudios.SJHotkeys.SingleInstance";
    private const string PipeName = "SJStudios.SJHotkeys.Activate";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();
    public bool Acquired { get; }
    public event EventHandler? ActivateRequested;

    public SingleInstanceService()
    {
        _mutex = new Mutex(true, MutexName, out var created);
        Acquired = created;
        if (Acquired) _ = ListenAsync(_stop.Token);
    }

    public void SignalExisting()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            using var writer = new StreamWriter(client);
            writer.WriteLine("activate");
        }
        catch { }
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(token);
                using var reader = new StreamReader(server);
                if (await reader.ReadLineAsync(token) == "activate") ActivateRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(250, token).ContinueWith(_ => { }); }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (Acquired) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _stop.Dispose();
    }
}
