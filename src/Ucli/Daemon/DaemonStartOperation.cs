using MackySoft.Ucli.Daemon.Start;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements daemon start workflow orchestration for one project fingerprint. </summary>
internal sealed class DaemonStartOperation : IDaemonStartOperation
{
    private readonly IDaemonLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonSessionCleanupService daemonSessionCleanupService;

    private readonly IDaemonExistingSessionGateService daemonExistingSessionGateService;

    private readonly IDaemonLaunchService daemonLaunchService;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="daemonSessionCleanupService"> The daemon session-cleanup service dependency. </param>
    /// <param name="daemonExistingSessionGateService"> The daemon existing-session gate service dependency. </param>
    /// <param name="daemonLaunchService"> The daemon launch service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartOperation (
        IDaemonLifecycleLockProvider lifecycleLockProvider,
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

        await using var lockHandle = await lifecycleLockProvider.Acquire(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);

        var readResult = await daemonSessionStore.Read(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return await HandleInvalidSessionRead(unityProject, readResult, timeout, cancellationToken).ConfigureAwait(false);
        }

        if (readResult.Exists)
        {
            var existingSessionGateResult = await daemonExistingSessionGateService.TryHandleExistingSession(
                    unityProject,
                    readResult.Session!,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existingSessionGateResult is not null)
            {
                return existingSessionGateResult;
            }
        }

        return await daemonLaunchService.Launch(unityProject, timeout, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DaemonStartResult> HandleInvalidSessionRead (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (readResult.FailureKind != DaemonSessionReadFailureKind.InvalidSession)
        {
            return DaemonStartResult.Failure(readResult.Error!);
        }

        var cleanupResult = await daemonSessionCleanupService.CleanupInvalidSessionArtifacts(
                unityProject,
                readResult,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!cleanupResult.IsSuccess)
        {
            return DaemonStartResult.Failure(cleanupResult.Error!);
        }

        return await daemonLaunchService.Launch(unityProject, timeout, cancellationToken).ConfigureAwait(false);
    }
}