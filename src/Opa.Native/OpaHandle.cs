using System.Diagnostics;
using System.Threading.Tasks;

namespace Opa.Native;

public sealed class OpaServerHandle : IDisposable, IAsyncDisposable
{
    private readonly Task<int> _processTask;
    private readonly CancellationTokenSource _cts;

    public OpaServerHandle(Task<int> processTask, CancellationTokenSource cts)
    {
        _processTask = processTask;
        _cts = cts;
    }

    public async Task<int> WaitForExitAsync() => await _processTask;

    public void Dispose()
    {
        _cts.Cancel();
        _cts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            Dispose();
            await _processTask;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Cancellation caught");
        }
    }
}
