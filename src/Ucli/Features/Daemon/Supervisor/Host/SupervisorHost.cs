using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
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

    private readonly ISupervisorProcessManager processManager;

    private readonly SupervisorBootstrapLockProvider bootstrapLockProvider;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="SupervisorHost" /> class. </summary>
    public SupervisorHost (
        SupervisorManifestStore manifestStore,
        SupervisorEndpointResolver endpointResolver,
        IDaemonSessionTokenGenerator sessionTokenGenerator,
        SupervisorTransportServer transportServer,
        SupervisorRequestDispatcher requestDispatcher,
        SupervisorProjectCoordinator projectCoordinator,
        SupervisorActivityTracker activityTracker,
        SupervisorRuntimeLogger runtimeLogger,
        ISupervisorProcessManager processManager,
        SupervisorBootstrapLockProvider bootstrapLockProvider,
        TimeProvider timeProvider)
    {
        this.manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.sessionTokenGenerator = sessionTokenGenerator ?? throw new ArgumentNullException(nameof(sessionTokenGenerator));
        this.transportServer = transportServer ?? throw new ArgumentNullException(nameof(transportServer));
        this.requestDispatcher = requestDispatcher ?? throw new ArgumentNullException(nameof(requestDispatcher));
        this.projectCoordinator = projectCoordinator ?? throw new ArgumentNullException(nameof(projectCoordinator));
        this.activityTracker = activityTracker ?? throw new ArgumentNullException(nameof(activityTracker));
        this.runtimeLogger = runtimeLogger ?? throw new ArgumentNullException(nameof(runtimeLogger));
        this.processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        this.bootstrapLockProvider = bootstrapLockProvider ?? throw new ArgumentNullException(nameof(bootstrapLockProvider));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Runs the supervisor host for the specified storage root. </summary>
    /// <param name="repositoryRoot"> The repository root used as storage root. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the hosting environment. </param>
    /// <returns>The process exit code.</returns>
    public async Task<int> RunAsync (
        AbsolutePath repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);

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

        (int ExitCode, bool CanReleaseProcessRegistration) hostResult = (1, false);
        SupervisorManifestCleanupStatus? cleanupStatus = null;
        using (runtimeOwnership)
        {
            try
            {
                hostResult = await RunWhileOwningRuntimeAsync(runtimeContext, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                cleanupStatus = await CleanupManifestIfOwnedAsync(runtimeContext, CancellationToken.None).ConfigureAwait(false);
                transportServer.Release();
            }
        }

        if (hostResult.CanReleaseProcessRegistration
            && cleanupStatus is SupervisorManifestCleanupStatus.Missing or SupervisorManifestCleanupStatus.Removed)
        {
            await ReleaseProcessRegistrationIfUnclaimedAsync(runtimeContext.StorageRoot).ConfigureAwait(false);
        }

        return hostResult.ExitCode;
    }

    private async Task<(int ExitCode, bool CanReleaseProcessRegistration)> RunWhileOwningRuntimeAsync (
        SupervisorRuntimeContext runtimeContext,
        CancellationToken cancellationToken)
    {
        activityTracker.Touch();

        using var hostCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var hostCancellationToken = hostCancellationTokenSource.Token;
        var idleShutdownRequested = false;

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
                        runtimeContext.Manifest.TransportEndpoint,
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
                idleShutdownRequested = await idleMonitorTask.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            await runtimeLogger.WriteAsync(
                    runtimeContext.StorageRoot,
                    "info",
                    "Supervisor stopped normally.",
                    CancellationToken.None)
                .ConfigureAwait(false);
            return (0, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (1, false);
        }
        catch (OperationCanceledException) when (idleShutdownRequested)
        {
            await runtimeLogger.WriteAsync(
                    runtimeContext.StorageRoot,
                    "info",
                    "Supervisor stopped by cancellation.",
                    CancellationToken.None)
                .ConfigureAwait(false);
            return (0, true);
        }
        catch (Exception exception)
        {
            await runtimeLogger.WriteAsync(
                    runtimeContext.StorageRoot,
                    "error",
                    $"Supervisor crashed. {exception}",
                    CancellationToken.None)
                .ConfigureAwait(false);
            return (1, true);
        }
    }

    private SupervisorRuntimeContext CreateRuntimeContext (AbsolutePath repositoryRoot)
    {
        var storageRoot = repositoryRoot;
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

    private async Task<bool> RunIdleMonitorAsync (
        CancellationTokenSource hostCancellationTokenSource,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await TimeProviderDelay.DelayAsync(TimeSpan.FromSeconds(1), timeProvider, cancellationToken)
                    .ConfigureAwait(false);
                if (projectCoordinator.HasActiveProjectWork || activityTracker.HasActiveRequests)
                {
                    continue;
                }

                if (!activityTracker.IsIdle(SupervisorConstants.IdleShutdownDelay))
                {
                    continue;
                }

                hostCancellationTokenSource.Cancel();
                return true;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        return false;
    }

    private async Task<SupervisorManifestCleanupStatus?> CleanupManifestIfOwnedAsync (
        SupervisorRuntimeContext runtimeContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return await manifestStore.CleanupOwnedRuntimeIfManifestMatchesAsync(
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
            return null;
        }
    }

    private async Task ReleaseProcessRegistrationIfUnclaimedAsync (AbsolutePath storageRoot)
    {
        try
        {
            await using var bootstrapLock = await bootstrapLockProvider.AcquireAsync(
                    storageRoot,
                    SupervisorConstants.ManifestPublicationTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false);
            var currentManifest = await manifestStore.ReadAfterEndpointPublicationAsync(
                    storageRoot,
                    SupervisorConstants.ManifestMutationLockTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (currentManifest is not null)
            {
                return;
            }

            var releaseError = await processManager.ReleaseCurrentProcessRegistrationAsync(
                    storageRoot,
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (releaseError is not null)
            {
                await runtimeLogger.WriteAsync(
                        storageRoot,
                        "error",
                        $"Supervisor process registration release failed. {releaseError.Message}",
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            await runtimeLogger.WriteAsync(
                    storageRoot,
                    "error",
                    $"Supervisor process registration release crashed. {exception}",
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }
}
