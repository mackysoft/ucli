using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;

/// <summary> Implements cleanup compensation for failed daemon launch attempts. </summary>
internal sealed class DaemonLaunchCompensationService : IDaemonLaunchCompensationService
{
    private readonly IDaemonProcessTerminationService processTerminationService;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonLaunchCompensationService" /> class. </summary>
    /// <param name="processTerminationService"> The process-termination service dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact-cleaner dependency. </param>
    /// <param name="timeProvider"> The time provider used for compensation deadline accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonLaunchCompensationService (
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner,
        TimeProvider timeProvider)
    {
        this.processTerminationService = processTerminationService ?? throw new ArgumentNullException(nameof(processTerminationService));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Stops the launched process snapshot and cleans daemon artifacts after launch failure. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="expectedSession"> The failed launch session generation when it was initialized; otherwise <see langword="null" />. </param>
    /// <param name="target"> The launched process termination target when available. </param>
    /// <param name="timeout"> The remaining timeout budget for launch-failure compensation. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The compensation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonSessionStoreOperationResult> CleanupFailedLaunchAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession? expectedSession,
        DaemonProcessTerminationTarget? target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        if (expectedSession is not null
            && expectedSession.ProjectFingerprint != unityProject.ProjectFingerprint)
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                "Expected failed-launch session projectFingerprint does not match the compensation target."));
        }

        var compensationTimeout = timeout > DaemonTimeouts.LaunchCompensationTimeout
            ? DaemonTimeouts.LaunchCompensationTimeout
            : timeout;
        var deadline = ExecutionDeadline.Start(compensationTimeout, timeProvider);
        using var deadlineCancellationTokenSource = new CancellationTokenSource(
            compensationTimeout,
            timeProvider);
        using var operationCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            deadlineCancellationTokenSource.Token);
        var operationCancellationToken = operationCancellationTokenSource.Token;

        try
        {
            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return CreateTimeoutFailure();
            }

            var stopResult = await processTerminationService.EnsureStoppedAsync(
                    target,
                    deadline,
                    operationCancellationToken)
                .ConfigureAwait(false);
            if (!stopResult.IsSuccess)
            {
                return stopResult;
            }

            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return CreateTimeoutFailure();
            }

            DaemonArtifactCleanupResult cleanupResult;
            if (expectedSession is not null)
            {
                cleanupResult = await artifactCleaner.CleanupIfSessionMatchesAsync(
                        unityProject,
                        expectedSession,
                        deadline,
                        operationCancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                if (target is { } stoppedProcess)
                {
                    cleanupResult = await artifactCleaner.CleanupIfStoppedProcessMatchesAsync(
                            unityProject,
                            stoppedProcess,
                            deadline,
                            operationCancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    cleanupResult = await artifactCleaner.CleanupIfSessionMissingAsync(
                            unityProject,
                            deadline,
                            operationCancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return CreateTimeoutFailure();
            }

            return cleanupResult.IsSuccess
                ? DaemonSessionStoreOperationResult.Success()
                : DaemonSessionStoreOperationResult.Failure(cleanupResult.Error!);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested
            && deadline.IsExpired)
        {
            return CreateTimeoutFailure();
        }
    }

    private static DaemonSessionStoreOperationResult CreateTimeoutFailure ()
    {
        return DaemonSessionStoreOperationResult.Failure(ExecutionError.Timeout(
            "Timed out before failed daemon launch compensation could complete."));
    }
}
