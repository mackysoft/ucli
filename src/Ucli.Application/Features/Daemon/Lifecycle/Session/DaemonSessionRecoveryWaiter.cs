using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Waits through an endpoint gap only while lifecycle state proves that the same GUI session is recovering. </summary>
internal sealed class DaemonSessionRecoveryWaiter
{
    private readonly IDaemonLifecycleStore daemonLifecycleStore;

    private readonly IDaemonProcessIdentityAssessor processIdentityAssessor;

    /// <summary> Initializes the recovery evidence dependencies. </summary>
    public DaemonSessionRecoveryWaiter (
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonProcessIdentityAssessor processIdentityAssessor)
    {
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
    }

    /// <summary> Delays one retry interval when persisted lifecycle state proves that the known session is recovering. </summary>
    public async ValueTask<bool> DelayIfRecoveringAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession knownSession,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(knownSession);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();

        var recoveryEvidenceDeadline = deadline.CreateCappedDeadline(
            DaemonTimeouts.ProbeAttemptTimeoutCap);
        var recoveryReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                recoveryEvidenceDeadline,
                cancellationToken,
                "Timed out before daemon recovery state could be read.",
                "Timed out while reading daemon recovery state.",
                token => IsRecoveringSessionAsync(
                    unityProject,
                    knownSession,
                    deadline.Clock,
                    token))
            .ConfigureAwait(false);
        if (!recoveryReadOperation.IsSuccess
            || !recoveryReadOperation.Value
            || !deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return false;
        }

        await TimeProviderDelay.DelayAsync(
                GetRetryDelay(remainingTimeout),
                deadline.Clock,
                cancellationToken)
            .ConfigureAwait(false);
        return deadline.TryGetRemainingTimeout(out _);
    }

    private async ValueTask<bool> IsRecoveringSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession knownSession,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (knownSession.EditorMode != DaemonEditorMode.Gui)
        {
            return false;
        }

        var lifecycleReadResult = await daemonLifecycleStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!lifecycleReadResult.IsSuccess || !lifecycleReadResult.Exists)
        {
            return false;
        }

        return DaemonLifecycleObservationAvailability.IsUsableForRecovery(
            lifecycleReadResult.Observation!,
            knownSession,
            processIdentityAssessor,
            timeProvider);
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }
}
