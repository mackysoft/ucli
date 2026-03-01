using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements daemon stop workflow orchestration for one project fingerprint. </summary>
internal sealed class DaemonStopOperation : IDaemonStopOperation
{
    private readonly IDaemonLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonShutdownClient shutdownClient;

    private readonly IDaemonProcessTerminationService processTerminationService;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    /// <summary> Initializes a new instance of the <see cref="DaemonStopOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="shutdownClient"> The shutdown client dependency. </param>
    /// <param name="processTerminationService"> The process termination service dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact cleaner dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStopOperation (
        IDaemonLifecycleLockProvider lifecycleLockProvider,
        IDaemonSessionStore daemonSessionStore,
        IDaemonShutdownClient shutdownClient,
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.shutdownClient = shutdownClient ?? throw new ArgumentNullException(nameof(shutdownClient));
        this.processTerminationService = processTerminationService ?? throw new ArgumentNullException(nameof(processTerminationService));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
    }

    /// <summary> Stops daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon stop timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon stop result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStopResult> Stop (
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
            return DaemonStopResult.Failure(readResult.Error!);
        }

        if (!readResult.Exists)
        {
            return DaemonStopResult.NotRunning();
        }

        var session = readResult.Session!;
        if (!session.CanShutdownProcess)
        {
            return DaemonStopResult.Failure(ExecutionError.InvalidArgument(
                "Daemon session does not allow process shutdown."));
        }

        var shutdownResult = await shutdownClient.SendShutdown(unityProject, session, timeout, cancellationToken).ConfigureAwait(false);

        if (shutdownResult.IsNotRunning)
        {
            var notRunningCleanupResult = await artifactCleaner.Cleanup(unityProject, cancellationToken).ConfigureAwait(false);
            return notRunningCleanupResult.IsSuccess
                ? DaemonStopResult.Stopped()
                : DaemonStopResult.Failure(notRunningCleanupResult.Error!);
        }

        var stopProcessResult = await processTerminationService.EnsureStopped(
                session.ProcessId,
                session.IssuedAtUtc,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!stopProcessResult.IsSuccess)
        {
            return DaemonStopResult.Failure(stopProcessResult.Error!);
        }

        var cleanupResult = await artifactCleaner.Cleanup(unityProject, cancellationToken).ConfigureAwait(false);
        if (!cleanupResult.IsSuccess)
        {
            return DaemonStopResult.Failure(cleanupResult.Error!);
        }

        if (!shutdownResult.IsSuccess)
        {
            return DaemonStopResult.Failure(shutdownResult.Error!);
        }

        return DaemonStopResult.Stopped();
    }
}