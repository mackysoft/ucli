using MackySoft.Ucli.Daemon.Start;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements daemon start workflow orchestration for one project fingerprint. </summary>
internal sealed class DaemonStartOperation : IDaemonStartOperation
{
    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonSessionCleanupService daemonSessionCleanupService;

    private readonly IDaemonExistingSessionGateService daemonExistingSessionGateService;

    private readonly IDaemonLaunchService daemonLaunchService;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="daemonSessionCleanupService"> The daemon session-cleanup service dependency. </param>
    /// <param name="daemonExistingSessionGateService"> The daemon existing-session gate service dependency. </param>
    /// <param name="daemonLaunchService"> The daemon launch service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartOperation (
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IDaemonSessionStore daemonSessionStore,
        IDaemonSessionCleanupService daemonSessionCleanupService,
        IDaemonExistingSessionGateService daemonExistingSessionGateService,
        IDaemonLaunchService daemonLaunchService)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonSessionCleanupService = daemonSessionCleanupService ?? throw new ArgumentNullException(nameof(daemonSessionCleanupService));
        this.daemonExistingSessionGateService = daemonExistingSessionGateService ?? throw new ArgumentNullException(nameof(daemonExistingSessionGateService));
        this.daemonLaunchService = daemonLaunchService ?? throw new ArgumentNullException(nameof(daemonLaunchService));
    }

    /// <summary> Starts daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon startup timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartResult> Start (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout);
        if (!deadline.TryGetRemainingTimeout(out var lockAcquireTimeout))
        {
            return DaemonStartResult.Failure(CreateTimeoutError("Timed out before daemon start workflow began."));
        }

        IAsyncDisposable lockHandle;
        try
        {
            lockHandle = await lifecycleLockProvider.Acquire(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    lockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            return DaemonStartResult.Failure(CreateTimeoutError(
                $"Timed out while waiting for project lifecycle lock. {exception.Message}"));
        }

        await using var acquiredLock = lockHandle;
        var readResult = await daemonSessionStore.Read(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return await HandleInvalidSessionRead(unityProject, readResult, deadline, cancellationToken).ConfigureAwait(false);
        }

        if (readResult.Exists)
        {
            if (!deadline.TryGetRemainingTimeout(out var existingSessionGateTimeout))
            {
                return DaemonStartResult.Failure(CreateTimeoutError(
                    "Timed out while probing existing daemon session."));
            }

            var existingSessionGateResult = await daemonExistingSessionGateService.TryHandleExistingSession(
                    unityProject,
                    readResult.Session!,
                    existingSessionGateTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existingSessionGateResult is not null)
            {
                return existingSessionGateResult;
            }
        }

        if (!deadline.TryGetRemainingTimeout(out var launchTimeout))
        {
            return DaemonStartResult.Failure(CreateTimeoutError(
                "Timed out before daemon launch could start."));
        }

        return await daemonLaunchService.Launch(unityProject, launchTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DaemonStartResult> HandleInvalidSessionRead (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (readResult.FailureKind != DaemonSessionReadFailureKind.InvalidSession)
        {
            return DaemonStartResult.Failure(readResult.Error!);
        }

        if (!deadline.TryGetRemainingTimeout(out var invalidSessionCleanupTimeout))
        {
            return DaemonStartResult.Failure(CreateTimeoutError(
                "Timed out while preparing invalid daemon-session cleanup."));
        }

        var cleanupResult = await daemonSessionCleanupService.CleanupInvalidSessionArtifacts(
                unityProject,
                readResult,
                invalidSessionCleanupTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!cleanupResult.IsSuccess)
        {
            return DaemonStartResult.Failure(cleanupResult.Error!);
        }

        if (!deadline.TryGetRemainingTimeout(out var launchTimeout))
        {
            return DaemonStartResult.Failure(CreateTimeoutError(
                "Timed out before daemon launch could start."));
        }

        return await daemonLaunchService.Launch(unityProject, launchTimeout, cancellationToken).ConfigureAwait(false);
    }
    private static ExecutionError CreateTimeoutError (string message)
    {
        return ExecutionError.Timeout(message);
    }
}