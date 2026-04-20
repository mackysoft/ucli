using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.UnityIntegration.Ipc;

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
    public async Task<int> Run (
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return 1;
        }

        var runtimeContext = CreateRuntimeContext(repositoryRoot);
        activityTracker.Touch();

        using var hostCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var hostCancellationToken = hostCancellationTokenSource.Token;

        try
        {
            await runtimeLogger.Write(
                    runtimeContext.StorageRoot,
                    "info",
                    $"Supervisor starting. endpoint={runtimeContext.Manifest.EndpointAddress}",
                    CancellationToken.None)
                .ConfigureAwait(false);

            var idleMonitorTask = RunIdleMonitor(hostCancellationTokenSource, hostCancellationToken);
            try
            {
                var endpoint = ResolveEndpoint(runtimeContext.Manifest);
                await transportServer.Run(
                        endpoint,
                        (stream, token) => requestDispatcher.HandleConnection(stream, runtimeContext, token),
                        token => manifestStore.Write(runtimeContext.StorageRoot, runtimeContext.Manifest, token).AsTask(),
                        hostCancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                hostCancellationTokenSource.Cancel();
                await projectCoordinator.AwaitManagedProcesses().ConfigureAwait(false);
                await idleMonitorTask.ConfigureAwait(false);
            }

            await runtimeLogger.Write(
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
            await runtimeLogger.Write(
                    runtimeContext.StorageRoot,
                    "info",
                    "Supervisor stopped by cancellation.",
                    CancellationToken.None)
                .ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            await runtimeLogger.Write(
                    runtimeContext.StorageRoot,
                    "error",
                    $"Supervisor crashed. {exception}",
                    CancellationToken.None)
                .ConfigureAwait(false);
            return 1;
        }
        finally
        {
            await CleanupManifestIfOwned(runtimeContext, CancellationToken.None).ConfigureAwait(false);
            transportServer.Release();
        }
    }

    private SupervisorRuntimeContext CreateRuntimeContext (string repositoryRoot)
    {
        var storageRoot = Path.GetFullPath(repositoryRoot);
        var endpoint = endpointResolver.Resolve(storageRoot);
        return new SupervisorRuntimeContext(
            storageRoot,
            new SupervisorInstanceManifest(
                ProcessId: Environment.ProcessId,
                SessionToken: sessionTokenGenerator.Create(),
                EndpointTransportKind: IpcTransportKindCodec.ToValue(endpoint.TransportKind),
                EndpointAddress: endpoint.Address,
                IssuedAtUtc: DateTimeOffset.UtcNow));
    }

    private async Task RunIdleMonitor (
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

    private async Task CleanupManifestIfOwned (
        SupervisorRuntimeContext runtimeContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await manifestStore.ReadOrNull(runtimeContext.StorageRoot, cancellationToken).ConfigureAwait(false);
            if (manifest != null && manifest.ProcessId == Environment.ProcessId)
            {
                manifestStore.DeleteIfExists(runtimeContext.StorageRoot);
            }
        }
        catch (Exception)
        {
            // NOTE:
            // best-effort cleanup should not mask supervisor exit.
        }
    }

    private static IpcEndpoint ResolveEndpoint (SupervisorInstanceManifest manifest)
    {
        if (!IpcTransportKindCodec.TryParse(manifest.EndpointTransportKind, out var transportKind))
        {
            throw new InvalidOperationException(
                $"Supervisor manifest endpointTransportKind is invalid: {manifest.EndpointTransportKind}.");
        }

        return new IpcEndpoint(transportKind, manifest.EndpointAddress);
    }
}