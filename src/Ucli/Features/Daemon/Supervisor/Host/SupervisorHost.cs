using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Hosts the worktree-local supervisor runtime that owns Unity daemon process lifetime. </summary>
internal sealed class SupervisorHost
{
    private readonly SupervisorManifestStore manifestStore;

    private readonly SupervisorEndpointResolver endpointResolver;

    private readonly IDaemonSessionTokenGenerator sessionTokenGenerator;

    private readonly SupervisorTransportServer transportServer;

    private readonly SupervisorRequestDispatcher requestDispatcher;

    private readonly SupervisorProjectCoordinator projectCoordinator;

    private readonly SupervisorActivityTracker activityTracker;

    private readonly SupervisorRuntimeLogger runtimeLogger;

    /// <summary> Initializes a new instance of the <see cref="SupervisorHost" /> class. </summary>
    public SupervisorHost (
        SupervisorManifestStore manifestStore,
        SupervisorEndpointResolver endpointResolver,
        IDaemonSessionTokenGenerator sessionTokenGenerator,
        SupervisorTransportServer transportServer,
        SupervisorRequestDispatcher requestDispatcher,
        SupervisorProjectCoordinator projectCoordinator,
        SupervisorActivityTracker activityTracker,
        SupervisorRuntimeLogger runtimeLogger)
    {
        this.manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.sessionTokenGenerator = sessionTokenGenerator ?? throw new ArgumentNullException(nameof(sessionTokenGenerator));
        this.transportServer = transportServer ?? throw new ArgumentNullException(nameof(transportServer));
        this.requestDispatcher = requestDispatcher ?? throw new ArgumentNullException(nameof(requestDispatcher));
        this.projectCoordinator = projectCoordinator ?? throw new ArgumentNullException(nameof(projectCoordinator));
        this.activityTracker = activityTracker ?? throw new ArgumentNullException(nameof(activityTracker));
        this.runtimeLogger = runtimeLogger ?? throw new ArgumentNullException(nameof(runtimeLogger));
    }

    /// <summary> Runs the supervisor host for the specified storage root. </summary>
    /// <param name="repositoryRoot"> The repository root used as storage root. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the hosting environment. </param>
    /// <returns>The process exit code.</returns>
    public async Task<int> RunAsync (
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return 1;
        }

        var runtimeContext = CreateRuntimeContext(repositoryRoot);
        FileExclusiveLock runtimeOwnership;

        try
        {
            runtimeOwnership = await FileExclusiveLock.AcquireAsync(
                    UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(runtimeContext.StorageRoot),
                    SupervisorConstants.RuntimeOwnershipLockTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 1;
        }
        catch (Exception exception)
        {
            await runtimeLogger.WriteAsync(
                    runtimeContext.StorageRoot,
                    "error",
                    $"Supervisor failed to claim runtime ownership. {exception}",
                    CancellationToken.None)
                .ConfigureAwait(false);
            return 1;
        }

        using (runtimeOwnership)
        {
            return await RunWhileOwningRuntimeAsync(runtimeContext, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<int> RunWhileOwningRuntimeAsync (
        SupervisorRuntimeContext runtimeContext,
        CancellationToken cancellationToken)
    {
        activityTracker.Touch();

        using var hostCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var hostCancellationToken = hostCancellationTokenSource.Token;

        try
        {
            await runtimeLogger.WriteAsync(
                    runtimeContext.StorageRoot,
                    "info",
                    $"Supervisor starting. endpoint={runtimeContext.Manifest.Endpoint.Address}",
                    CancellationToken.None)
                .ConfigureAwait(false);

            var idleMonitorTask = RunIdleMonitorAsync(hostCancellationTokenSource, hostCancellationToken);
            try
            {
                using var endpointPublicationLease = await manifestStore.AcquireEndpointPublicationLeaseAsync(
                        runtimeContext.StorageRoot,
                        SupervisorConstants.ManifestMutationLockTimeout,
                        hostCancellationToken)
                    .ConfigureAwait(false);
                await transportServer.RunAsync(
                        runtimeContext.Manifest.Endpoint,
                        (stream, token) => requestDispatcher.HandleConnectionAsync(
                            stream,
                            runtimeContext,
                            SupervisorConstants.InitialFrameReadTimeout,
                            token),
                        async token =>
                        {
                            try
                            {
                                await endpointPublicationLease.PublishAsync(
                                        runtimeContext.Manifest,
                                        token)
                                    .ConfigureAwait(false);
                            }
                            finally
                            {
                                endpointPublicationLease.Dispose();
                            }
                        },
                        SupervisorConstants.MaximumActiveConnections,
                        SupervisorConstants.ConnectionDrainTimeout,
                        hostCancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                hostCancellationTokenSource.Cancel();
                await projectCoordinator.AwaitManagedProcessesAsync().ConfigureAwait(false);
                await idleMonitorTask.ConfigureAwait(false);
            }

            await runtimeLogger.WriteAsync(
                    runtimeContext.StorageRoot,
                    "info",
                    "Supervisor stopped normally.",
                    CancellationToken.None)
                .ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 1;
        }
        catch (OperationCanceledException) when (hostCancellationTokenSource.IsCancellationRequested)
        {
            await runtimeLogger.WriteAsync(
                    runtimeContext.StorageRoot,
                    "info",
                    "Supervisor stopped by cancellation.",
                    CancellationToken.None)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            await runtimeLogger.WriteAsync(
                    runtimeContext.StorageRoot,
                    "error",
                    $"Supervisor crashed. {exception}",
                    CancellationToken.None)
                .ConfigureAwait(false);
            return 1;
        }
        finally
        {
            await CleanupManifestIfOwnedAsync(runtimeContext, CancellationToken.None).ConfigureAwait(false);
            transportServer.Release();
        }
    }

    private SupervisorRuntimeContext CreateRuntimeContext (string repositoryRoot)
    {
        var storageRoot = UcliStoragePathResolver.NormalizeStorageRootPath(repositoryRoot);
        var sessionToken = sessionTokenGenerator.Create();
        var endpoint = endpointResolver.ResolveRuntimeEndpoint(storageRoot, sessionToken);
        return new SupervisorRuntimeContext(
            storageRoot,
            new SupervisorInstanceManifest(
                processId: Environment.ProcessId,
                sessionToken: sessionToken,
                endpoint: endpoint,
                issuedAtUtc: DateTimeOffset.UtcNow));
    }

    private async Task RunIdleMonitorAsync (
        CancellationTokenSource hostCancellationTokenSource,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            if (projectCoordinator.HasActiveProjectWork || activityTracker.HasActiveRequests)
            {
                continue;
            }

            if (!activityTracker.IsIdle(SupervisorConstants.IdleShutdownDelay))
            {
                continue;
            }

            hostCancellationTokenSource.Cancel();
            return;
        }
    }

    private async Task CleanupManifestIfOwnedAsync (
        SupervisorRuntimeContext runtimeContext,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await manifestStore.CleanupOwnedRuntimeIfManifestMatchesAsync(
                    runtimeContext.StorageRoot,
                    runtimeContext.Manifest,
                    endpointResolver.ResolveUnixSocketCleanupTargetOrNull(runtimeContext.StorageRoot),
                    SupervisorConstants.ManifestMutationLockTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // NOTE:
            // best-effort cleanup should not mask supervisor exit.
        }
    }

}
