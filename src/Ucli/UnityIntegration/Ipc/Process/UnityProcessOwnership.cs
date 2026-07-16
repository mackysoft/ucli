using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Launch;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Resolves ownership of started Unity processes before their handles leave a launch boundary. </summary>
internal static class UnityProcessOwnership
{
    /// <summary>
    /// Resolves daemon-launch metadata from one started process, or reclaims the process when metadata or caller
    /// cancellation prevents ownership transfer. A successful result leaves the handle owned by the caller.
    /// </summary>
    /// <param name="processHandle"> The started process handle reclaimed on failure or retained by the caller on success. </param>
    /// <param name="cancellationToken"> The launch cancellation token checked before ownership transfer. </param>
    /// <returns> The daemon launch result. </returns>
    public static async ValueTask<UnityDaemonLaunchResult> ResolveDaemonLaunchAsync (
        IUnityProcessHandle processHandle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processHandle);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processId = processHandle.ProcessId;
            var processStartedAtUtc = processHandle.StartTimeUtc;
            cancellationToken.ThrowIfCancellationRequested();
            if (processStartedAtUtc is null)
            {
                var error = ExecutionError.InternalError(
                    $"Unity process start time could not be read. processId={processId}.");
                await TerminateAndDisposeBestEffortAsync(processHandle).ConfigureAwait(false);
                return UnityDaemonLaunchResult.Failure(error);
            }

            return UnityDaemonLaunchResult.Success(processId, processStartedAtUtc.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TerminateAndDisposeBestEffortAsync(processHandle).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await TerminateAndDisposeBestEffortAsync(processHandle).ConfigureAwait(false);
            return UnityDaemonLaunchResult.Failure(ExecutionError.InternalError(
                $"Failed to read started Unity process metadata. {exception.Message}"));
        }
    }

    /// <summary> Reclaims and releases one started process without allowing cleanup failure to replace the primary failure. </summary>
    /// <param name="processHandle"> The started process handle to reclaim. </param>
    public static async ValueTask TerminateAndDisposeBestEffortAsync (IUnityProcessHandle processHandle)
    {
        ArgumentNullException.ThrowIfNull(processHandle);

        try
        {
            _ = await processHandle.TerminateAsync(
                    ProcessTerminationPolicy.ForceKill,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Cleanup failure must not replace the launch failure or cancellation that triggered reclamation.
        }

        await DisposeBestEffortAsync(processHandle).ConfigureAwait(false);
    }

    /// <summary> Releases one process handle without changing its already-transferred child-process lifetime. </summary>
    /// <param name="processHandle"> The local process handle to release. </param>
    public static async ValueTask DisposeBestEffortAsync (IUnityProcessHandle processHandle)
    {
        ArgumentNullException.ThrowIfNull(processHandle);

        try
        {
            await processHandle.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A detached process or the primary launch failure remains authoritative when local handle release fails.
        }
    }
}
