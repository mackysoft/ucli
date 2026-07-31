using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Recovery;

/// <summary>
/// Recovers only a provider-persisted Lifecycle Execution start that still matches the pending
/// application registration.
/// </summary>
internal static class LifecycleExecutionStartRecordRecovery
{
    private static readonly TimeSpan ObservationRetryDelay =
        TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Reads the guarded project-local Start Record without constructing a reference from request
    /// inputs when the provider response was lost.
    /// </summary>
    public static async ValueTask<LifecycleExecutionStartBinding?> TryReadAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        var registration = dispatchRequest.Registration;
        if (registration is null)
        {
            return null;
        }

        try
        {
            var store = FileLifecycleExecutionStore.CreateForProject(
                unityProject.UnityProjectRoot,
                unityProject.ProjectFingerprint);
            var stored = await store.ReadAsync(
                    registration.Definition.Kind,
                    registration.ExecutionId,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (stored is null)
            {
                return null;
            }

            var start = stored.Start;
            if (!registration.HasSameIdentity(
                    start.LifecycleExecutionRef)
                || start.DeadlineUtc != registration.DeadlineUtc
                || start.StartedAtUtc != registration.StartedAtUtc
                || !ProjectIdentityInfo.TryFromHost(
                    unityProject,
                    start.Project,
                    out _,
                    out _))
            {
                return null;
            }

            return start;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Waits until the provider's Start Record becomes observable independently
    /// from delivery of the Lifecycle Start response.
    /// </summary>
    public static async Task<LifecycleExecutionStartBinding?> WaitUntilAvailableAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(deadline);
        while (deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = await TryReadAsync(
                    unityProject,
                    dispatchRequest)
                .ConfigureAwait(false);
            if (start is not null)
            {
                return start;
            }

            var retryDelay = remainingTimeout < ObservationRetryDelay
                ? remainingTimeout
                : ObservationRetryDelay;
            await Task.Delay(
                    retryDelay,
                    deadline.Clock,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await TryReadAsync(
                unityProject,
                dispatchRequest)
            .ConfigureAwait(false);
    }
}
