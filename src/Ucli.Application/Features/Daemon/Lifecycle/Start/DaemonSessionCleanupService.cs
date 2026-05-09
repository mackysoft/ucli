using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Implements session-cleanup workflow for invalid or stale daemon sessions before new start attempts. </summary>
internal sealed class DaemonSessionCleanupService : IDaemonSessionCleanupService
{
    private readonly IDaemonProcessTerminationService processTerminationService;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionCleanupService" /> class. </summary>
    /// <param name="processTerminationService"> The process-termination service dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact-cleaner dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonSessionCleanupService (
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner)
    {
        this.processTerminationService = processTerminationService ?? throw new ArgumentNullException(nameof(processTerminationService));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
    }

    /// <summary> Cleans invalid-session artifacts from daemon-session read results. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="readResult"> The failed daemon-session read result. </param>
    /// <param name="timeout"> The timeout used for process-termination attempts. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup operation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="readResult" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonSessionStoreOperationResult> CleanupInvalidSessionArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        if (TryCreateUnsafeInvalidSessionRelaunchError(readResult, unityProject, out var unsafeRelaunchError))
        {
            return DaemonSessionStoreOperationResult.Failure(unsafeRelaunchError!);
        }

        if (TryGetInvalidSessionStopTarget(readResult, unityProject, out var processId, out var issuedAtUtc))
        {
            var stopResult = await processTerminationService.EnsureStoppedAsync(
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

        return await artifactCleaner.CleanupAsync(unityProject, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Cleans stale-session artifacts from existing daemon session metadata. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session metadata. </param>
    /// <param name="timeout"> The timeout used for process-termination attempts. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup operation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="session" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonSessionStoreOperationResult> CleanupStaleSessionArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        if (TryGetSessionStopTarget(session, unityProject, out var processId, out var issuedAtUtc))
        {
            var stopResult = await processTerminationService.EnsureStoppedAsync(
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

        return await artifactCleaner.CleanupAsync(unityProject, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryCreateUnsafeInvalidSessionRelaunchError (
        DaemonSessionReadResult readResult,
        ResolvedUnityProjectContext unityProject,
        out ExecutionError? error)
    {
        error = null;

        var session = readResult.Session;
        if (session == null)
        {
            return false;
        }

        if (!string.Equals(session.ProjectFingerprint, unityProject.ProjectFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        if (session.ProcessId is not int processId || processId <= 0 || session.IssuedAtUtc == default)
        {
            return false;
        }

        if (TryGetInvalidSessionStopTarget(readResult, unityProject, out _, out _))
        {
            return false;
        }

        error = ExecutionError.InternalError(
            $"Daemon session is invalid and cannot be safely replaced because the previously launched daemon may still be running. fingerprint={unityProject.ProjectFingerprint} pid={processId}");
        return true;
    }

    /// <summary> Gets process stop target from invalid session snapshot when identity can be validated safely. </summary>
    /// <param name="readResult"> The daemon session read result. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="processId"> The process identifier when stop target can be determined. </param>
    /// <param name="issuedAtUtc"> The issued-at timestamp when stop target can be determined. </param>
    /// <returns> <see langword="true" /> when stop target can be determined; otherwise <see langword="false" />. </returns>
    private static bool TryGetInvalidSessionStopTarget (
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

        return TryGetSessionStopTarget(session, unityProject, out processId, out issuedAtUtc);
    }

    private static bool TryGetSessionStopTarget (
        DaemonSession session,
        ResolvedUnityProjectContext unityProject,
        out int processId,
        out DateTimeOffset issuedAtUtc)
    {
        processId = default;
        issuedAtUtc = default;

        if (!string.Equals(session.ProjectFingerprint, unityProject.ProjectFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        // NOTE:
        // Cleanup may only terminate processes owned by the current uCLI batchmode
        // contract. User-owned GUI sessions can be stale without granting process shutdown.
        return DaemonSessionTerminationPolicy.TryGetTerminationTarget(session, out processId, out issuedAtUtc);
    }
}
