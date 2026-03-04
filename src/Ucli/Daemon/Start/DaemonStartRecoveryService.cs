using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon.Start;

/// <summary> Implements recovery workflow for invalid or stale daemon sessions before new start attempts. </summary>
internal sealed class DaemonStartRecoveryService : IDaemonStartRecoveryService
{
    private readonly IDaemonProcessTerminationService processTerminationService;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartRecoveryService" /> class. </summary>
    /// <param name="processTerminationService"> The process-termination service dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact-cleaner dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartRecoveryService (
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner)
    {
        this.processTerminationService = processTerminationService ?? throw new ArgumentNullException(nameof(processTerminationService));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
    }

    /// <summary> Recovers stale artifacts from invalid daemon-session read results. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="readResult"> The failed daemon-session read result. </param>
    /// <param name="timeout"> The timeout used for process-termination attempts. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The recovery operation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="readResult" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonSessionStoreOperationResult> RecoverInvalidSession (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        if (TryGetRecoverableInvalidSessionStopTarget(readResult, unityProject, out var processId, out var issuedAtUtc))
        {
            var stopResult = await processTerminationService.EnsureStopped(
                    processId,
                    issuedAtUtc,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!stopResult.IsSuccess)
            {
                return stopResult;
            }
        }

        return await artifactCleaner.Cleanup(unityProject, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Recovers stale artifacts from existing daemon session metadata. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session metadata. </param>
    /// <param name="timeout"> The timeout used for process-termination attempts. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The recovery operation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="session" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonSessionStoreOperationResult> RecoverStaleSession (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var stopResult = await processTerminationService.EnsureStopped(
                session.ProcessId,
                session.IssuedAtUtc,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!stopResult.IsSuccess)
        {
            return stopResult;
        }

        return await artifactCleaner.Cleanup(unityProject, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Gets process stop target from invalid session snapshot when identity can be validated safely. </summary>
    /// <param name="readResult"> The daemon session read result. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="processId"> The process identifier when stop target can be recovered. </param>
    /// <param name="issuedAtUtc"> The issued-at timestamp when stop target can be recovered. </param>
    /// <returns> <see langword="true" /> when stop target is recoverable; otherwise <see langword="false" />. </returns>
    private static bool TryGetRecoverableInvalidSessionStopTarget (
        DaemonSessionReadResult readResult,
        ResolvedUnityProjectContext unityProject,
        out int processId,
        out DateTimeOffset issuedAtUtc)
    {
        processId = default;
        issuedAtUtc = default;

        var session = readResult.Session;
        if (session == null)
        {
            return false;
        }

        if (!string.Equals(session.ProjectFingerprint, unityProject.ProjectFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        if (session.ProcessId is not int candidateProcessId || candidateProcessId <= 0)
        {
            return false;
        }

        if (session.IssuedAtUtc == default)
        {
            return false;
        }

        processId = candidateProcessId;
        issuedAtUtc = session.IssuedAtUtc;
        return true;
    }
}