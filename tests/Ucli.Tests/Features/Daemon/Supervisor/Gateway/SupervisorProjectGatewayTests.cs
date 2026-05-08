using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Daemon;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorProjectGatewayTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenBootstrapConsumesBudget_PassesRemainingTimeoutToSupervisorClient ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "ensure-running-timeout");
        var timeProvider = new ManualTimeProvider();
        var manifest = CreateManifest();
        var manifestStore = new SupervisorManifestStore();
        await manifestStore.Write(scope.FullPath, manifest, CancellationToken.None);

        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient();
        var observedEnsureRunningTimeout = TimeSpan.Zero;
        var observedEditorMode = (string?)null;
        transportClient.SendHandler = (endpoint, request, timeout, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(request.Method, SupervisorIpcContracts.PingMethod, StringComparison.Ordinal))
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(180));
                return ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                    request,
                    new SupervisorIpcContracts.PingResponse(
                        manifest.ProcessId,
                        manifest.IssuedAtUtc)));
            }

            if (string.Equals(request.Method, SupervisorIpcContracts.EnsureRunningMethod, StringComparison.Ordinal))
            {
                Assert.True(IpcPayloadCodec.TryDeserialize(
                    request.Payload,
                    out SupervisorIpcContracts.EnsureRunningRequest payload,
                    out _));
                observedEnsureRunningTimeout = TimeSpan.FromMilliseconds(payload.TimeoutMilliseconds);
                observedEditorMode = payload.EditorMode;
                return ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                    request,
                    new SupervisorIpcContracts.EnsureRunningResponse(
                        StartStatus: "started",
                        DaemonStatus: "running",
                        Session: CreateSession())));
            }

            throw new InvalidOperationException($"Unexpected method: {request.Method}");
        };

        var client = new SupervisorClient(transportClient);
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            client,
            new StubSupervisorProcessLauncher(),
            new SupervisorBootstrapLockProvider(),
            new SupervisorEndpointResolver(),
            timeProvider);
        var gateway = new SupervisorProjectGateway(
            bootstrapper,
            manifestStore,
            client,
            timeProvider);

        var result = await gateway.EnsureRunning(
            CreateUnityProject(scope.FullPath),
            TimeSpan.FromMilliseconds(900),
            editorMode: DaemonEditorMode.Gui,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.NotNull(result.Session);
        Assert.True(observedEnsureRunningTimeout > TimeSpan.Zero);
        Assert.True(observedEnsureRunningTimeout < TimeSpan.FromMilliseconds(900));
        Assert.Equal(DaemonEditorModeValues.Gui, observedEditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryStopProject_WhenManifestIsMalformed_DeletesManifestAndReturnsNull ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "malformed-manifest");
        var timeProvider = new ManualTimeProvider();
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(manifestPath, "{ malformed json", CancellationToken.None);

        var manifestStore = new SupervisorManifestStore();
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Transport should not be used when manifest read fails."),
        };
        var client = new SupervisorClient(transportClient);
        var gateway = new SupervisorProjectGateway(
            new SupervisorBootstrapper(
                manifestStore,
                client,
                new StubSupervisorProcessLauncher(),
                new SupervisorBootstrapLockProvider(),
                new SupervisorEndpointResolver(),
                timeProvider),
            manifestStore,
            client,
            timeProvider);

        var result = await gateway.TryStopProject(
            CreateUnityProject(scope.FullPath),
            TimeSpan.FromMilliseconds(600),
            CancellationToken.None);

        Assert.Null(result);
        Assert.False(File.Exists(manifestPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryStopProject_WhenProbeConsumesBudget_PassesRemainingTimeoutToStopProject ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "stop-project-timeout");
        var timeProvider = new ManualTimeProvider();
        var manifest = CreateManifest();
        var manifestStore = new SupervisorManifestStore();
        await manifestStore.Write(scope.FullPath, manifest, CancellationToken.None);

        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient();
        var observedStopTimeout = TimeSpan.Zero;
        transportClient.SendHandler = (endpoint, request, timeout, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(request.Method, SupervisorIpcContracts.PingMethod, StringComparison.Ordinal))
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(220));
                return ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                    request,
                    new SupervisorIpcContracts.PingResponse(
                        manifest.ProcessId,
                        manifest.IssuedAtUtc)));
            }

            if (string.Equals(request.Method, SupervisorIpcContracts.StopProjectMethod, StringComparison.Ordinal))
            {
                Assert.True(IpcPayloadCodec.TryDeserialize(
                    request.Payload,
                    out SupervisorIpcContracts.StopProjectRequest payload,
                    out _));
                observedStopTimeout = TimeSpan.FromMilliseconds(payload.TimeoutMilliseconds);
                return ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                    request,
                    new SupervisorIpcContracts.StopProjectResponse(
                        StopStatus: "stopped",
                        DaemonStatus: "notRunning")));
            }

            throw new InvalidOperationException($"Unexpected method: {request.Method}");
        };

        var client = new SupervisorClient(transportClient);
        var gateway = new SupervisorProjectGateway(
            new SupervisorBootstrapper(
                manifestStore,
                client,
                new StubSupervisorProcessLauncher(),
                new SupervisorBootstrapLockProvider(),
                new SupervisorEndpointResolver(),
                timeProvider),
            manifestStore,
            client,
            timeProvider);

        var result = await gateway.TryStopProject(
            CreateUnityProject(scope.FullPath),
            TimeSpan.FromMilliseconds(850),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStopStatus.Stopped, result.Status);
        Assert.True(observedStopTimeout > TimeSpan.Zero);
        Assert.True(observedStopTimeout < TimeSpan.FromMilliseconds(850));
    }

    private static ResolvedUnityProjectContext CreateUnityProject (string repositoryRoot)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: Path.Combine(repositoryRoot, "UnityProject"),
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static SupervisorInstanceManifest CreateManifest ()
    {
        return new SupervisorInstanceManifest(
            ProcessId: Environment.ProcessId,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-supervisor-test",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));
    }

    private static DaemonSession CreateSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "daemon-session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 1, 0, 0, TimeSpan.Zero),
            EditorMode: DaemonSession.EditorModeBatchmode,
            OwnerKind: DaemonSession.OwnerKindCli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 1234,
            OwnerProcessId: 5678);
    }

    private sealed class StubSupervisorProcessLauncher : ISupervisorProcessLauncher
    {
        public ValueTask<ExecutionError?> Launch (
            string storageRoot,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Supervisor launch should not be used by this test.");
        }
    }
}
