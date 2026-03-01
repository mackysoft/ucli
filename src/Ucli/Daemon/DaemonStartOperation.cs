using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements daemon start workflow orchestration for one project fingerprint. </summary>
internal sealed class DaemonStartOperation : IDaemonStartOperation
{
    private readonly IDaemonLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IIpcEndpointResolver endpointResolver;

    private readonly IDaemonPingClient daemonPingClient;

    private readonly IUnityDaemonProcessLauncher unityDaemonProcessLauncher;

    private readonly IDaemonStartupReadinessProbe startupReadinessProbe;

    private readonly IDaemonProcessTerminationService processTerminationService;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    private readonly IDaemonSessionTokenGenerator sessionTokenGenerator;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="endpointResolver"> The endpoint resolver dependency. </param>
    /// <param name="daemonPingClient"> The daemon ping client dependency. </param>
    /// <param name="unityDaemonProcessLauncher"> The Unity daemon process launcher dependency. </param>
    /// <param name="startupReadinessProbe"> The startup readiness probe dependency. </param>
    /// <param name="processTerminationService"> The process termination service dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact cleaner dependency. </param>
    /// <param name="sessionTokenGenerator"> The daemon session token generator dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability classifier dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartOperation (
        IDaemonLifecycleLockProvider lifecycleLockProvider,
        IDaemonSessionStore daemonSessionStore,
        IIpcEndpointResolver endpointResolver,
        IDaemonPingClient daemonPingClient,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IDaemonStartupReadinessProbe startupReadinessProbe,
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner,
        IDaemonSessionTokenGenerator sessionTokenGenerator,
        IDaemonReachabilityClassifier reachabilityClassifier)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.unityDaemonProcessLauncher = unityDaemonProcessLauncher ?? throw new ArgumentNullException(nameof(unityDaemonProcessLauncher));
        this.startupReadinessProbe = startupReadinessProbe ?? throw new ArgumentNullException(nameof(startupReadinessProbe));
        this.processTerminationService = processTerminationService ?? throw new ArgumentNullException(nameof(processTerminationService));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
        this.sessionTokenGenerator = sessionTokenGenerator ?? throw new ArgumentNullException(nameof(sessionTokenGenerator));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
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
            return DaemonStartResult.Failure(readResult.Error!);
        }

        if (readResult.Exists)
        {
            try
            {
                await daemonPingClient.Ping(unityProject, timeout, cancellationToken).ConfigureAwait(false);
                return DaemonStartResult.AlreadyRunning(readResult.Session!);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
            {
                var cleanupResult = await artifactCleaner.Cleanup(unityProject, cancellationToken).ConfigureAwait(false);
                if (!cleanupResult.IsSuccess)
                {
                    return DaemonStartResult.Failure(cleanupResult.Error!);
                }
            }
            catch (Exception exception)
            {
                return DaemonStartResult.Failure(ExecutionError.InternalError(
                    $"Failed to probe existing daemon session. {exception.Message}"));
            }
        }

        var endpoint = endpointResolver.Resolve(unityProject.RepositoryRoot, unityProject.ProjectFingerprint);
        var session = new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: sessionTokenGenerator.Create(),
            ProjectFingerprint: unityProject.ProjectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindCli,
            CanShutdownProcess: true,
            EndpointTransportKind: DaemonSessionTransportKindCodec.ToValue(endpoint.TransportKind),
            EndpointAddress: endpoint.Address,
            ProcessId: null);

        var writeSessionResult = await daemonSessionStore.Write(
                unityProject.RepositoryRoot,
                session,
                cancellationToken)
            .ConfigureAwait(false);
        if (!writeSessionResult.IsSuccess)
        {
            return DaemonStartResult.Failure(writeSessionResult.Error!);
        }

        var daemonLogPath = DaemonStoragePathResolver.ResolveDaemonLogPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        var launchResult = await unityDaemonProcessLauncher.Launch(
                unityProject,
                daemonLogPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!launchResult.IsSuccess)
        {
            await CleanupAfterFailedStart(unityProject, launchResult.ProcessId, cancellationToken).ConfigureAwait(false);
            return DaemonStartResult.Failure(launchResult.Error!);
        }

        if (launchResult.ProcessId is int processId)
        {
            session = session with { ProcessId = processId };
            var updateSessionResult = await daemonSessionStore.Write(
                    unityProject.RepositoryRoot,
                    session,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!updateSessionResult.IsSuccess)
            {
                await CleanupAfterFailedStart(unityProject, processId, cancellationToken).ConfigureAwait(false);
                return DaemonStartResult.Failure(updateSessionResult.Error!);
            }
        }

        var probeResult = await startupReadinessProbe.WaitUntilReady(unityProject, timeout, cancellationToken).ConfigureAwait(false);
        if (probeResult.IsReady)
        {
            return DaemonStartResult.Started(session);
        }

        await CleanupAfterFailedStart(unityProject, launchResult.ProcessId, cancellationToken).ConfigureAwait(false);
        return DaemonStartResult.Failure(probeResult.Error!);
    }

    /// <summary> Cleans stale artifacts and stops process after start operation fails. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="processId"> The launched process identifier when available. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A completed task. </returns>
    private async ValueTask CleanupAfterFailedStart (
        ResolvedUnityProjectContext unityProject,
        int? processId,
        CancellationToken cancellationToken)
    {
        await processTerminationService.EnsureStopped(processId, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        await artifactCleaner.Cleanup(unityProject, cancellationToken).ConfigureAwait(false);
    }
}