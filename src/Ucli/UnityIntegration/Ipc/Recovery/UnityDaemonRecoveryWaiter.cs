using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Recovery;

/// <summary> Waits through a GUI daemon endpoint gap when lifecycle sidecar proves domain-reload recovery. </summary>
internal sealed class UnityDaemonRecoveryWaiter
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonLifecycleStore daemonLifecycleStore;

    private readonly IDaemonProcessIdentityAssessor processIdentityAssessor;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityDaemonRecoveryWaiter" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="daemonLifecycleStore"> The daemon lifecycle sidecar store dependency. </param>
    /// <param name="processIdentityAssessor"> The daemon process identity assessor dependency. </param>
    /// <param name="timeProvider"> The time provider used for retry delays. </param>
    public UnityDaemonRecoveryWaiter (
        IDaemonSessionStore daemonSessionStore,
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonProcessIdentityAssessor processIdentityAssessor,
        TimeProvider? timeProvider = null)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Delays one retry interval when the persisted lifecycle proves daemon endpoint recovery is in progress. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The shared command deadline. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> <see langword="true" /> when a recovery retry delay was consumed; otherwise <see langword="false" />. </returns>
    public async ValueTask<bool> DelayIfRecoveringAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        if (!await IsRecoveringSessionAsync(unityProject, cancellationToken).ConfigureAwait(false)
            || !deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return false;
        }

        await TimeProviderDelay.DelayAsync(
                GetRetryDelay(remainingTimeout),
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private async ValueTask<bool> IsRecoveringSessionAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        var sessionReadResult = await daemonSessionStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!sessionReadResult.IsSuccess || !sessionReadResult.Exists)
        {
            return false;
        }

        var session = sessionReadResult.Session!;
        if (session.EditorMode != DaemonEditorMode.Gui)
        {
            return false;
        }

        // NOTE: A missing daemon endpoint is recoverable only when the lifecycle sidecar
        // proves the same GUI session is inside domain-reload recovery. Other gaps remain
        // ordinary DAEMON_NOT_RUNNING failures.
        var lifecycleReadResult = await daemonLifecycleStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!lifecycleReadResult.IsSuccess
            || !lifecycleReadResult.Exists
            || !lifecycleReadResult.Observation!.IsRecovering
            || !DaemonLifecycleObservationMatcher.MatchesSessionByEditorInstance(lifecycleReadResult.Observation, sessionReadResult.Session!))
        {
            return false;
        }

        if (!session.ProcessId.HasValue)
        {
            return false;
        }

        return processIdentityAssessor.AssessByProcessId(
                session.ProcessId.Value,
                session.ProcessStartedAtUtc)
            .Status == DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess;
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }
}
