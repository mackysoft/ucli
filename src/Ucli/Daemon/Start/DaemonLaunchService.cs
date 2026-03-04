using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon.Start;

/// <summary> Implements daemon launch workflow with failure-compensation handling. </summary>
internal sealed class DaemonLaunchService : IDaemonLaunchService
{
    private readonly IIpcEndpointResolver endpointResolver;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonSessionTokenGenerator sessionTokenGenerator;

    private readonly IUnityDaemonProcessLauncher unityDaemonProcessLauncher;

    private readonly IDaemonStartupReadinessProbe startupReadinessProbe;

    private readonly IDaemonProcessTerminationService processTerminationService;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    /// <summary> Initializes a new instance of the <see cref="DaemonLaunchService" /> class. </summary>
    /// <param name="endpointResolver"> The IPC endpoint resolver dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    /// <param name="sessionTokenGenerator"> The daemon session-token generator dependency. </param>
    /// <param name="unityDaemonProcessLauncher"> The Unity daemon process-launcher dependency. </param>
    /// <param name="startupReadinessProbe"> The daemon startup-readiness probe dependency. </param>
    /// <param name="processTerminationService"> The process-termination service dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact-cleaner dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonLaunchService (
        IIpcEndpointResolver endpointResolver,
        IDaemonSessionStore daemonSessionStore,
        IDaemonSessionTokenGenerator sessionTokenGenerator,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IDaemonStartupReadinessProbe startupReadinessProbe,
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner)
    {
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.sessionTokenGenerator = sessionTokenGenerator ?? throw new ArgumentNullException(nameof(sessionTokenGenerator));
        this.unityDaemonProcessLauncher = unityDaemonProcessLauncher ?? throw new ArgumentNullException(nameof(unityDaemonProcessLauncher));
        this.startupReadinessProbe = startupReadinessProbe ?? throw new ArgumentNullException(nameof(startupReadinessProbe));
        this.processTerminationService = processTerminationService ?? throw new ArgumentNullException(nameof(processTerminationService));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
    }

    /// <summary> Launches daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon startup timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartResult> Launch (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var endpoint = endpointResolver.Resolve(unityProject.RepositoryRoot, unityProject.ProjectFingerprint);
        var session = new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: sessionTokenGenerator.Create(),
            ProjectFingerprint: unityProject.ProjectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindCli,
            CanShutdownProcess: true,
            EndpointTransportKind: IpcTransportKindCodec.ToValue(endpoint.TransportKind),
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

        var daemonLogPath = UcliStoragePathResolver.ResolveDaemonLogPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        var launchResult = await unityDaemonProcessLauncher.Launch(
                unityProject,
                daemonLogPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!launchResult.IsSuccess)
        {
            var cleanupResult = await CleanupAfterFailedStart(
                    unityProject,
                    launchResult.ProcessId,
                    session.IssuedAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!cleanupResult.IsSuccess)
            {
                return DaemonStartResult.Failure(ExecutionError.InternalError(
                    $"Daemon launch failed and cleanup failed. LaunchError={launchResult.Error!.Message} CleanupError={cleanupResult.Error!.Message}"));
            }

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
                var cleanupResult = await CleanupAfterFailedStart(
                        unityProject,
                        processId,
                        session.IssuedAtUtc,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!cleanupResult.IsSuccess)
                {
                    return DaemonStartResult.Failure(ExecutionError.InternalError(
                        $"Daemon session update failed and cleanup failed. SessionError={updateSessionResult.Error!.Message} CleanupError={cleanupResult.Error!.Message}"));
                }

                return DaemonStartResult.Failure(updateSessionResult.Error!);
            }
        }

        var probeResult = await startupReadinessProbe.WaitUntilReady(unityProject, timeout, cancellationToken).ConfigureAwait(false);
        if (probeResult.IsReady)
        {
            return DaemonStartResult.Started(session);
        }

        var finalCleanupResult = await CleanupAfterFailedStart(
                unityProject,
                launchResult.ProcessId,
                session.IssuedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        if (!finalCleanupResult.IsSuccess)
        {
            return DaemonStartResult.Failure(ExecutionError.InternalError(
                $"Daemon startup readiness probe failed and cleanup failed. ProbeError={probeResult.Error!.Message} CleanupError={finalCleanupResult.Error!.Message}"));
        }

        return DaemonStartResult.Failure(probeResult.Error!);
    }

    /// <summary> Cleans stale artifacts and stops process after launch workflow fails. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="processId"> The launched process identifier when available. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon-session issuance timestamp used for identity validation. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup result. </returns>
    private async ValueTask<DaemonSessionStoreOperationResult> CleanupAfterFailedStart (
        ResolvedUnityProjectContext unityProject,
        int? processId,
        DateTimeOffset? expectedIssuedAtUtc,
        CancellationToken cancellationToken)
    {
        var stopResult = await processTerminationService.EnsureStopped(
                processId,
                expectedIssuedAtUtc,
                TimeSpan.FromSeconds(1),
                cancellationToken)
            .ConfigureAwait(false);
        if (!stopResult.IsSuccess)
        {
            return stopResult;
        }

        return await artifactCleaner.Cleanup(unityProject, cancellationToken).ConfigureAwait(false);
    }
}