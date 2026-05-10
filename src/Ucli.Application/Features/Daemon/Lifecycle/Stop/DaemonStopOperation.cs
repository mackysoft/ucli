using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
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

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonStopOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="shutdownClient"> The shutdown client dependency. </param>
    /// <param name="processTerminationService"> The process termination service dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact cleaner dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStopOperation (
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IDaemonSessionStore daemonSessionStore,
        IDaemonShutdownClient shutdownClient,
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner,
        TimeProvider? timeProvider = null)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.shutdownClient = shutdownClient ?? throw new ArgumentNullException(nameof(shutdownClient));
        this.processTerminationService = processTerminationService ?? throw new ArgumentNullException(nameof(processTerminationService));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Stops daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon stop timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon stop result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStopResult> StopAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
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

        await using var acquiredLock = lockHandle;
        var readResult = await daemonSessionStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return DaemonStopResult.Failure(readResult.Error!);
        }

        if (!readResult.Exists)
        {
            return DaemonStopResult.NotRunning();
        }

        var session = readResult.Session!;
        var stopCapability = DaemonSessionTerminationPolicy.ResolveStopCapability(session);
        if (stopCapability == DaemonSessionTerminationPolicy.StopCapability.EndpointOnly)
        {
            return await StopEndpointOnlySessionAsync(
                    unityProject,
                    session,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (stopCapability != DaemonSessionTerminationPolicy.StopCapability.ProcessShutdown)
        {
            return DaemonStopResult.Failure(ExecutionError.InvalidArgument(
                "Daemon session does not allow process shutdown."));
        }

        if (!deadline.TryGetRemainingTimeout(out var shutdownTimeout))
        {
            return DaemonStopResult.Failure(CreateTimeoutError(
                "Timed out before daemon shutdown request could be sent."));
        }

        var shutdownResult = await shutdownClient.SendShutdownAsync(unityProject, session, shutdownTimeout, cancellationToken).ConfigureAwait(false);

        if (shutdownResult.IsNotRunning)
        {
            var notRunningTerminationTimeout = deadline.TryGetRemainingTimeout(out var remainingTerminationTimeout)
                ? remainingTerminationTimeout
                : DaemonTimeouts.StopCompensationTimeout;
            var notRunningStopAndCleanupResult = await EnsureStoppedAndCleanupAsync(
                    unityProject,
                    session,
                    notRunningTerminationTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return notRunningStopAndCleanupResult.IsSuccess
                ? DaemonStopResult.Stopped()
                : DaemonStopResult.Failure(notRunningStopAndCleanupResult.Error!);
        }

        if (!shutdownResult.IsSuccess
            && !DaemonSessionTerminationPolicy.TryGetTerminationTarget(session, out _))
        {
            return DaemonStopResult.Failure(shutdownResult.Error!);
        }

        if (!deadline.TryGetRemainingTimeout(out var processTerminationTimeout))
        {
            var fallbackStopResult = await EnsureStoppedAndCleanupAsync(
                    unityProject,
                    session,
                    DaemonTimeouts.StopCompensationTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!fallbackStopResult.IsSuccess)
            {
                return DaemonStopResult.Failure(fallbackStopResult.Error!);
            }

            return DaemonStopResult.Failure(CreateTimeoutError(
                "Timed out before daemon process termination could be completed."));
        }

        var stopAndCleanupResult = await EnsureStoppedAndCleanupAsync(
                unityProject,
                session,
                processTerminationTimeout,
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

        return DaemonStopResult.Stopped();
    }

    private async ValueTask<DaemonStopResult> StopEndpointOnlySessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var shutdownTimeout))
        {
            return DaemonStopResult.Failure(CreateTimeoutError(
                "Timed out before daemon endpoint shutdown request could be sent."));
        }

        var shutdownResult = await shutdownClient.SendShutdownAsync(
                unityProject,
                session,
                shutdownTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!shutdownResult.IsSuccess && !shutdownResult.IsNotRunning)
        {
            return DaemonStopResult.Failure(shutdownResult.Error!);
        }

        var cleanupResult = await artifactCleaner.CleanupAsync(unityProject, cancellationToken).ConfigureAwait(false);
        return cleanupResult.IsSuccess
            ? DaemonStopResult.Stopped()
            : DaemonStopResult.Failure(cleanupResult.Error!);
    }

    private static ExecutionError CreateTimeoutError (string message)
    {
        return ExecutionError.Timeout(message);
    }

    private async ValueTask<DaemonSessionStoreOperationResult> EnsureStoppedAndCleanupAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (DaemonSessionTerminationPolicy.TryGetTerminationTarget(session, out var target))
        {
            var stopProcessResult = await processTerminationService.EnsureStoppedAsync(
                    target,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!stopProcessResult.IsSuccess)
            {
                return stopProcessResult;
            }
        }

        return await artifactCleaner.CleanupAsync(unityProject, cancellationToken).ConfigureAwait(false);
    }
}
