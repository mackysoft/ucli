using System.Collections.Concurrent;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Owns detached batchmode process handles until their redirected streams and process lifetime end. </summary>
internal sealed class UnityBatchmodeProcessLifetimeOwner
{
    private readonly ConcurrentDictionary<IUnityBatchmodeProcessHandle, byte> ownedProcesses =
        new(ReferenceEqualityComparer.Instance);

    /// <summary> Transfers one successfully launched batchmode process to exit-bound lifetime ownership. </summary>
    /// <param name="processHandle"> The process handle to retain and release after process exit. </param>
    public void Transfer (IUnityBatchmodeProcessHandle processHandle)
    {
        ArgumentNullException.ThrowIfNull(processHandle);
        if (!ownedProcesses.TryAdd(processHandle, 0))
        {
            throw new InvalidOperationException("Unity batchmode process handle ownership was already transferred.");
        }

        _ = ObserveExitAndReleaseAsync(processHandle);
    }

    private async Task ObserveExitAndReleaseAsync (IUnityBatchmodeProcessHandle processHandle)
    {
        try
        {
            try
            {
                await processHandle.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await UnityProcessOwnership.TerminateAndDisposeBestEffortAsync(processHandle).ConfigureAwait(false);
                return;
            }

            await UnityProcessOwnership.DisposeBestEffortAsync(processHandle).ConfigureAwait(false);
        }
        finally
        {
            _ = ownedProcesses.TryRemove(processHandle, out _);
        }
    }
}
