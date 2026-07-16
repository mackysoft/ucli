using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;

/// <summary> Implements session-cleanup workflow for invalid or stale daemon sessions before new start attempts. </summary>
internal sealed class DaemonSessionCleanupService : IDaemonSessionCleanupService
{
    private readonly IDaemonProcessTerminationService processTerminationService;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    private readonly IDaemonInvalidSessionCleanupSafetyEvaluator invalidSessionCleanupSafetyEvaluator;

    private readonly DaemonCompensationOperationOwner compensationOperationOwner;

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionCleanupService" /> class. </summary>
    /// <param name="processTerminationService"> The process-termination service dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact-cleaner dependency. </param>
    /// <param name="invalidSessionCleanupSafetyEvaluator"> The invalid-session cleanup safety-evaluator dependency. </param>
    /// <param name="compensationOperationOwner"> The owner of cleanup mutations that outlive their caller. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonSessionCleanupService (
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner,
        IDaemonInvalidSessionCleanupSafetyEvaluator invalidSessionCleanupSafetyEvaluator,
        DaemonCompensationOperationOwner compensationOperationOwner)
    {
        this.processTerminationService = processTerminationService ?? throw new ArgumentNullException(nameof(processTerminationService));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
        this.invalidSessionCleanupSafetyEvaluator = invalidSessionCleanupSafetyEvaluator ?? throw new ArgumentNullException(nameof(invalidSessionCleanupSafetyEvaluator));
        this.compensationOperationOwner = compensationOperationOwner ?? throw new ArgumentNullException(nameof(compensationOperationOwner));
    }

    /// <summary> Cleans invalid-session artifacts from daemon-session read results. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="readResult"> The failed daemon-session read result. </param>
    /// <param name="deadline"> The deadline shared by process termination and artifact cleanup. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup operation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="readResult" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonSessionStoreOperationResult> CleanupInvalidSessionArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(deadline);

        if (invalidSessionCleanupSafetyEvaluator.RequiresUnsafeSkip(readResult.InvalidEvidence))
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError(
                "Daemon session is invalid and cannot be safely replaced because its process identity could not be proven inactive."));
        }

        var executionResult = await compensationOperationOwner.ExecuteAsync(
                unityProject,
                DaemonOperationLane.LifecycleCompensation,
                deadline,
                cancellationToken,
                "Timed out waiting to clean invalid daemon session artifacts.",
                "Timed out while cleaning invalid daemon session artifacts.",
                (_, ownedCancellationToken) => CleanupInvalidSessionArtifactsCoreAsync(
                    unityProject,
                    readResult,
                    deadline,
                    ownedCancellationToken))
            .ConfigureAwait(false);
        return executionResult.IsSuccess
            ? executionResult.Value!
            : DaemonSessionStoreOperationResult.Failure(executionResult.Error!);
    }

    /// <summary> Cleans stale-session artifacts from existing daemon session metadata. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session metadata. </param>
    /// <param name="deadline"> The deadline shared by process termination and artifact cleanup. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup operation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="session" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonSessionStoreOperationResult> CleanupStaleSessionArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(deadline);

        var executionResult = await compensationOperationOwner.ExecuteAsync(
                unityProject,
                DaemonOperationLane.LifecycleCompensation,
                deadline,
                cancellationToken,
                "Timed out waiting to clean stale daemon session artifacts.",
                "Timed out while cleaning stale daemon session artifacts.",
                (_, ownedCancellationToken) => CleanupStaleSessionArtifactsCoreAsync(
                    unityProject,
                    session,
                    deadline,
                    ownedCancellationToken))
            .ConfigureAwait(false);
        return executionResult.IsSuccess
            ? executionResult.Value!
            : DaemonSessionStoreOperationResult.Failure(executionResult.Error!);
    }

    private async ValueTask<DaemonSessionStoreOperationResult> CleanupInvalidSessionArtifactsCoreAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return CreateTimeoutFailure("Timed out before invalid daemon session artifact cleanup could begin.");
        }

        if (readResult.ArtifactIdentity is null)
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError(
                "Invalid daemon session cleanup requires an observed session artifact identity."));
        }

        var cleanupResult = await artifactCleaner.CleanupIfSessionArtifactMatchesAsync(
                unityProject,
                readResult.ArtifactIdentity,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);

        return ToSessionStoreResult(cleanupResult);
    }

    private async ValueTask<DaemonSessionStoreOperationResult> CleanupStaleSessionArtifactsCoreAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var hasStopTarget = TryGetSessionStopTarget(session, unityProject, out var target);
        if (hasStopTarget)
        {
            var stopResult = await EnsureStoppedAsync(target, deadline, cancellationToken).ConfigureAwait(false);
            if (!stopResult.IsSuccess)
            {
                return stopResult;
            }
        }

        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return CreateTimeoutFailure("Timed out before stale daemon session artifact cleanup could begin.");
        }

        var cleanupResult = hasStopTarget
            ? await artifactCleaner.CleanupIfStoppedProcessMatchesAsync(
                    unityProject,
                    target,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false)
            : await artifactCleaner.CleanupIfSessionMatchesAsync(
                    unityProject,
                    session,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        return ToSessionStoreResult(cleanupResult);
    }

    private async ValueTask<DaemonSessionStoreOperationResult> EnsureStoppedAsync (
        DaemonProcessTerminationTarget target,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return CreateTimeoutFailure("Timed out before invalid or stale daemon process termination could begin.");
        }

        return await processTerminationService.EnsureStoppedAsync(
                target,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static DaemonSessionStoreOperationResult ToSessionStoreResult (
        DaemonArtifactCleanupResult cleanupResult)
    {
        return cleanupResult.IsSuccess
            ? DaemonSessionStoreOperationResult.Success()
            : DaemonSessionStoreOperationResult.Failure(cleanupResult.Error!);
    }

    private static DaemonSessionStoreOperationResult CreateTimeoutFailure (string message)
    {
        return DaemonSessionStoreOperationResult.Failure(ExecutionError.Timeout(message));
    }

    private static bool TryGetSessionStopTarget (
        DaemonSession session,
        ResolvedUnityProjectContext unityProject,
        out DaemonProcessTerminationTarget target)
    {
        target = default;

        if (session.ProjectFingerprint != unityProject.ProjectFingerprint)
        {
            return false;
        }

        // NOTE:
        // Cleanup may only terminate processes owned by the current uCLI batchmode
        // contract. User-owned GUI sessions can be stale without granting process shutdown.
        return DaemonSessionTerminationPolicy.TryGetTerminationTarget(session, out target);
    }

}
