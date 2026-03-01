using System.Diagnostics;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements process termination checks and force-kill fallback by process identifier. </summary>
internal sealed class DaemonProcessTerminationService : IDaemonProcessTerminationService
{
    private const int ProbeRetryDelayMilliseconds = 100;

    /// <summary> Ensures daemon process is stopped before timeout expires. </summary>
    /// <param name="processId"> The daemon process identifier when available. </param>
    /// <param name="timeout"> The process termination timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The process termination result. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonSessionStoreOperationResult> EnsureStopped (
        int? processId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        if (processId is null)
        {
            return DaemonSessionStoreOperationResult.Success();
        }

        Process? process;
        try
        {
            process = Process.GetProcessById(processId.Value);
        }
        catch (ArgumentException)
        {
            return DaemonSessionStoreOperationResult.Success();
        }

        var deadlineUtc = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                return DaemonSessionStoreOperationResult.Success();
            }

            if (DateTimeOffset.UtcNow >= deadlineUtc)
            {
                break;
            }

            await Task.Delay(ProbeRetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            process.Refresh();
        }

        try
        {
            process.Kill(entireProcessTree: true);
            var exited = process.WaitForExit((int)Math.Max(1, timeout.TotalMilliseconds));
            if (!exited)
            {
                return DaemonSessionStoreOperationResult.Failure(ExecutionError.Timeout(
                    $"Timed out while force-stopping daemon process '{processId.Value}'."));
            }

            return DaemonSessionStoreOperationResult.Success();
        }
        catch (Exception exception)
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to force-stop daemon process '{processId.Value}'. {exception.Message}"));
        }
    }
}
