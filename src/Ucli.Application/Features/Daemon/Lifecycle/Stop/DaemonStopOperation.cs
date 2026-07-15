using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

/// <summary> Implements daemon stop workflow orchestration for one project fingerprint. </summary>
internal sealed class DaemonStopOperation : IDaemonStopOperation
{
    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonShutdownClient shutdownClient;

    private readonly IDaemonProcessTerminationService processTerminationService;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    private readonly DaemonCompensationOperationOwner compensationOperationOwner;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonStopOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="shutdownClient"> The shutdown client dependency. </param>
    /// <param name="processTerminationService"> The process termination service dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact cleaner dependency. </param>
    /// <param name="compensationOperationOwner"> The owner for compensation that outlives a caller deadline. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStopOperation (
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IDaemonSessionStore daemonSessionStore,
        IDaemonShutdownClient shutdownClient,
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner,
        DaemonCompensationOperationOwner compensationOperationOwner,
        TimeProvider timeProvider)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.shutdownClient = shutdownClient ?? throw new ArgumentNullException(nameof(shutdownClient));
        this.processTerminationService = processTerminationService ?? throw new ArgumentNullException(nameof(processTerminationService));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
        this.compensationOperationOwner = compensationOperationOwner ?? throw new ArgumentNullException(nameof(compensationOperationOwner));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Stops daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The deadline shared by all normal daemon-stop phases. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon stop result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonStopResult> StopAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);

        if (!deadline.TryGetRemainingTimeout(out var lockAcquireTimeout))
        {
            return DaemonStopResult.Failure(CreateTimeoutError("Timed out before daemon stop workflow began."));
        }

        IAsyncDisposable lockHandle;
        try
        {
            lockHandle = await lifecycleLockProvider.AcquireAsync(
                    new ProjectLifecycleLockRequest(unityProject.UnityProjectRoot),
                    lockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return DaemonStopResult.Failure(CreateTimeoutError(
                $"Timed out while waiting for project lifecycle lock. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return DaemonStopResult.Failure(ExecutionError.InternalError(
                $"Failed to acquire project lifecycle lock. {exception.Message}"));
        }

        var acquiredLock = lockHandle;
        try
        {
            var admissionError = await compensationOperationOwner.WaitForQuiescenceAsync(
                unityProject,
                deadline,
                cancellationToken,
                "Timed out waiting for prior daemon compensation to quiesce.")
            .ConfigureAwait(false);
            if (admissionError is not null)
            {
                return DaemonStopResult.Failure(admissionError);
            }

            var sessionReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                    deadline,
                    cancellationToken,
                    "Timed out before daemon session read could begin.",
                    "Timed out while reading daemon session before stop.",
                    token => daemonSessionStore.ReadAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        token))
                .ConfigureAwait(false);
            if (!sessionReadOperation.IsSuccess)
            {
                return DaemonStopResult.Failure(sessionReadOperation.Error!);
            }

            var readResult = sessionReadOperation.Value!;
            if (!readResult.IsSuccess)
            {
                return DaemonStopResult.Failure(readResult.Error!);
            }

            if (!readResult.Exists)
            {
                return DaemonStopResult.NotRunning();
            }

            var session = readResult.Session!;
            if (!session.CanShutdownProcess)
            {
                return await StopEndpointOnlySessionAsync(
                        unityProject,
                        session,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return DaemonStopResult.Failure(CreateTimeoutError(
                    "Timed out before daemon shutdown request could be sent."));
            }

            var shutdownResult = await shutdownClient.SendShutdownAsync(unityProject, session, deadline, cancellationToken).ConfigureAwait(false);

            if (shutdownResult.IsNotRunning)
            {
                var notRunningStopAndCleanupResult = await ExecuteStopCompensationAsync(
                        unityProject,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!notRunningStopAndCleanupResult.IsSuccess)
                {
                    return DaemonStopResult.Failure(notRunningStopAndCleanupResult.Error!);
                }

                return deadline.TryGetRemainingTimeout(out _)
                    ? DaemonStopResult.Stopped()
                    : DaemonStopResult.Failure(CreateTimeoutError(
                        "Timed out before daemon process termination could be completed."));
            }

            if (!shutdownResult.IsSuccess
                && !DaemonSessionTerminationPolicy.TryGetTerminationTarget(session, out _))
            {
                return DaemonStopResult.Failure(shutdownResult.Error!);
            }

            var stopAndCleanupResult = await ExecuteStopCompensationAsync(
                    unityProject,
                    session,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!stopAndCleanupResult.IsSuccess)
            {
                return DaemonStopResult.Failure(stopAndCleanupResult.Error!);
            }

            if (!shutdownResult.IsSuccess)
            {
                return DaemonStopResult.Failure(shutdownResult.Error!);
            }

            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return DaemonStopResult.Failure(CreateTimeoutError(
                    "Timed out before daemon process termination could be completed."));
            }

            return DaemonStopResult.Stopped();
        }
        finally
        {
            if (!compensationOperationOwner.TryTransferLifecycleLease(unityProject, acquiredLock))
            {
                await acquiredLock.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<DaemonStopResult> StopEndpointOnlySessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return DaemonStopResult.Failure(CreateTimeoutError(
                "Timed out before daemon endpoint shutdown request could be sent."));
        }

        var shutdownResult = await shutdownClient.SendShutdownAsync(
                unityProject,
                session,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!shutdownResult.IsSuccess && !shutdownResult.IsNotRunning)
        {
            return DaemonStopResult.Failure(shutdownResult.Error!);
        }

        var cleanupResult = await ExecuteStopCompensationAsync(
                unityProject,
                session,
                cancellationToken)
            .ConfigureAwait(false);
        if (!cleanupResult.IsSuccess)
        {
            return DaemonStopResult.Failure(cleanupResult.Error!);
        }

        return deadline.TryGetRemainingTimeout(out _)
            ? DaemonStopResult.Stopped()
            : DaemonStopResult.Failure(CreateTimeoutError(
                "Timed out before daemon endpoint cleanup could be completed."));
    }

    private static ExecutionError CreateTimeoutError (string message)
    {
        return ExecutionError.Timeout(message);
    }

    private async ValueTask<DaemonSessionStoreOperationResult> ExecuteStopCompensationAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        CancellationToken cancellationToken)
    {
        var compensationDeadline = ExecutionDeadline.Start(
            DaemonTimeouts.StopCompensationTimeout,
            timeProvider);
        var executionResult = await compensationOperationOwner.ExecuteAsync(
                unityProject,
                DaemonOperationLane.LifecycleCompensation,
                compensationDeadline,
                cancellationToken,
                "Timed out waiting for prior daemon compensation to quiesce.",
                "Timed out before daemon stop compensation could complete.",
                (_, ownedCancellationToken) => EnsureStoppedAndCleanupCoreAsync(
                    unityProject,
                    session,
                    compensationDeadline,
                    ownedCancellationToken))
            .ConfigureAwait(false);
        return executionResult.IsSuccess
            ? executionResult.Value!
            : DaemonSessionStoreOperationResult.Failure(executionResult.Error!);
    }

    private async ValueTask<DaemonSessionStoreOperationResult> EnsureStoppedAndCleanupCoreAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline compensationDeadline,
        CancellationToken cancellationToken)
    {
        var hasTerminationTarget = DaemonSessionTerminationPolicy.TryGetTerminationTarget(session, out var target);
        if (hasTerminationTarget)
        {
            if (!compensationDeadline.TryGetRemainingTimeout(out _))
            {
                return DaemonSessionStoreOperationResult.Failure(CreateTimeoutError(
                    "Timed out before daemon process termination could begin."));
            }

            var stopProcessResult = await processTerminationService.EnsureStoppedAsync(
                    target,
                    compensationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!stopProcessResult.IsSuccess)
            {
                return stopProcessResult;
            }
        }

        if (!compensationDeadline.TryGetRemainingTimeout(out _))
        {
            return DaemonSessionStoreOperationResult.Failure(CreateTimeoutError(
                "Timed out before daemon artifact cleanup could begin."));
        }

        var cleanupResult = hasTerminationTarget
            ? await artifactCleaner.CleanupIfStoppedProcessMatchesAsync(
                    unityProject,
                    target,
                    compensationDeadline,
                    cancellationToken)
                .ConfigureAwait(false)
            : await artifactCleaner.CleanupIfSessionMatchesAsync(
                    unityProject,
                    session,
                    compensationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
        if (!cleanupResult.IsSuccess)
        {
            return DaemonSessionStoreOperationResult.Failure(cleanupResult.Error!);
        }

        return compensationDeadline.TryGetRemainingTimeout(out _)
            ? DaemonSessionStoreOperationResult.Success()
            : DaemonSessionStoreOperationResult.Failure(CreateTimeoutError(
                "Timed out before daemon artifact cleanup could complete."));
    }
}
