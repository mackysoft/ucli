using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
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
        await manifestStore.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient();
        var observedEnsureRunningTimeout = TimeSpan.Zero;
        var observedEditorMode = (string?)null;
        var observedOnStartupBlocked = (string?)null;
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
                observedOnStartupBlocked = payload.OnStartupBlocked;
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
        var progressSink = new CollectingProgressSink();
        var progressEmitter = new DaemonStartProgressEmitter(
            progressSink,
            "fingerprint",
            900,
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Keep);

        var result = await gateway.EnsureRunningAsync(
            CreateUnityProject(scope.FullPath),
            TimeSpan.FromMilliseconds(900),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Keep,
            progressObserver: progressEmitter,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.NotNull(result.Session);
        Assert.True(observedEnsureRunningTimeout > TimeSpan.Zero);
        Assert.True(observedEnsureRunningTimeout < TimeSpan.FromMilliseconds(900));
        Assert.Equal("gui", observedEditorMode);
        Assert.Equal("keep", observedOnStartupBlocked);
        AssertProgressEvents(
            progressSink,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningCompleted));
        var completedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[^1].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(CommandProgressResult.Succeeded), completedEntry.Result);
        Assert.Equal("started", completedEntry.StartStatus);
        Assert.Equal("running", completedEntry.DaemonStatus);
        Assert.Equal(900, completedEntry.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithSupervisorProgressSink_ForwardsSupervisorProgressThroughStreamClient ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "ensure-running-supervisor-progress");
        var manifest = CreateManifest();
        var manifestStore = new SupervisorManifestStore();
        await manifestStore.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient();
        transportClient.SendHandler = (endpoint, request, timeout, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(SupervisorIpcContracts.PingMethod, request.Method);
            return ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                request,
                new SupervisorIpcContracts.PingResponse(
                    manifest.ProcessId,
                    manifest.IssuedAtUtc)));
        };
        transportClient.StreamingHandler = async (endpoint, request, timeout, onProgressFrame, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(SupervisorIpcContracts.EnsureRunningMethod, request.Method);
            Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), request.ResponseMode);
            Assert.True(IpcPayloadCodec.TryDeserialize(
                request.Payload,
                out SupervisorIpcContracts.EnsureRunningRequest payload,
                out _));
            await onProgressFrame(
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcStreamFrameKinds.Progress,
                        ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint),
                        IpcPayloadCodec.SerializeToElement(
                            new DaemonStartStartupObservationProgressEntry(
                                ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation),
                                payload.ProjectFingerprint,
                                payload.TimeoutMilliseconds,
                                "batchmode",
                                payload.OnStartupBlocked,
                                "attempt-1",
                                "cli",
                                true,
                                1234,
                                null,
                                ContractLiteralCodec.ToValue(DaemonStartupStatus.WaitingForEndpoint),
                                null,
                                ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.EndpointRegistration),
                                null,
                                null,
                                null)),
                        Response: null),
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonServiceTestContext.CreateSuccessResponse(
                request,
                new SupervisorIpcContracts.EnsureRunningResponse(
                    StartStatus: "started",
                    DaemonStatus: "running",
                    Session: CreateSession()));
        };

        var client = new SupervisorClient(transportClient);
        var gateway = new SupervisorProjectGateway(
            new SupervisorBootstrapper(
                manifestStore,
                client,
                new StubSupervisorProcessLauncher(),
                new SupervisorBootstrapLockProvider(),
                new SupervisorEndpointResolver()),
            manifestStore,
            client);
        var progressSink = new CollectingProgressSink();

        var result = await gateway.EnsureRunningAsync(
            CreateUnityProject(scope.FullPath),
            TimeSpan.FromMilliseconds(900),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressObserver: null,
            supervisorProgressSink: progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var progress = Assert.Single(progressSink.Entries);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint), progress.EventName);
        var progressPayload = Assert.IsType<DaemonStartStartupObservationProgressEntry>(progress.Payload);
        Assert.Equal("fingerprint", progressPayload.ProjectFingerprint);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupStatus.WaitingForEndpoint), progressPayload.StartupStatus);
        Assert.Equal(2, transportClient.Calls.Count);
        Assert.True(transportClient.Calls[1].UsesUnboundedResponseWait);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenProgressObserverConsumesTime_DoesNotConsumeTimeoutBudget ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "ensure-running-progress-timeout");
        var timeProvider = new ManualTimeProvider();
        var manifest = CreateManifest();
        var manifestStore = new SupervisorManifestStore();
        await manifestStore.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient();
        var observedEnsureRunningTimeout = TimeSpan.Zero;
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
        var progressSink = new CollectingProgressSink(() => timeProvider.Advance(TimeSpan.FromMilliseconds(250)));
        var progressEmitter = new DaemonStartProgressEmitter(
            progressSink,
            "fingerprint",
            900,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto);

        var result = await gateway.EnsureRunningAsync(
            CreateUnityProject(scope.FullPath),
            TimeSpan.FromMilliseconds(900),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressObserver: progressEmitter,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(720), observedEnsureRunningTimeout);
        AssertProgressEvents(
            progressSink,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningCompleted));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenSupervisorEnsureRunningFails_EmitsFailedProgressEntry ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "ensure-running-progress-failure");
        var manifest = CreateManifest();
        var manifestStore = new SupervisorManifestStore();
        await manifestStore.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient();
        transportClient.SendHandler = (endpoint, request, timeout, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(request.Method, SupervisorIpcContracts.PingMethod, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                    request,
                    new SupervisorIpcContracts.PingResponse(
                        manifest.ProcessId,
                        manifest.IssuedAtUtc)));
            }

            if (string.Equals(request.Method, SupervisorIpcContracts.EnsureRunningMethod, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(DaemonServiceTestContext.CreateErrorResponse(
                    request,
                    ExecutionErrorCodes.IpcTimeout,
                    "ensureRunning timed out"));
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
                new SupervisorEndpointResolver()),
            manifestStore,
            client);
        var progressSink = new CollectingProgressSink();
        var progressEmitter = new DaemonStartProgressEmitter(
            progressSink,
            "fingerprint",
            900,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto);

        var result = await gateway.EnsureRunningAsync(
            CreateUnityProject(scope.FullPath),
            TimeSpan.FromMilliseconds(900),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressObserver: progressEmitter,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        AssertProgressEvents(
            progressSink,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningCompleted));
        var completedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[^1].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(CommandProgressResult.Failed), completedEntry.Result);
        Assert.Equal("failed", completedEntry.StartStatus);
        Assert.Equal("notRunning", completedEntry.DaemonStatus);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout.Value, completedEntry.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenSupervisorBootstrapFails_EmitsFailedProgressEntry ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "bootstrap-progress-failure");
        var manifestStore = new SupervisorManifestStore();
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor IPC should not be used when bootstrap launch fails."),
        };
        var client = new SupervisorClient(transportClient);
        var launcher = new StubSupervisorProcessLauncher
        {
            LaunchError = ExecutionError.InternalError(
                "supervisor launch failed",
                UcliCoreErrorCodes.InternalError),
        };
        var gateway = new SupervisorProjectGateway(
            new SupervisorBootstrapper(
                manifestStore,
                client,
                launcher,
                new SupervisorBootstrapLockProvider(),
                new SupervisorEndpointResolver()),
            manifestStore,
            client);
        var progressSink = new CollectingProgressSink();
        var progressEmitter = new DaemonStartProgressEmitter(
            progressSink,
            "fingerprint",
            900,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto);

        var result = await gateway.EnsureRunningAsync(
            CreateUnityProject(scope.FullPath),
            TimeSpan.FromMilliseconds(900),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressObserver: progressEmitter,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, launcher.LaunchCallCount);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Error!.Code);
        AssertProgressEvents(
            progressSink,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapCompleted));
        var completedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[^1].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(CommandProgressResult.Failed), completedEntry.Result);
        Assert.Null(completedEntry.StartStatus);
        Assert.Null(completedEntry.DaemonStatus);
        Assert.Equal(UcliCoreErrorCodes.InternalError.Value, completedEntry.ErrorCode);
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

        var result = await gateway.TryStopProjectAsync(
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
        await manifestStore.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

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

        var result = await gateway.TryStopProjectAsync(
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
            EditorMode: "batchmode",
            OwnerKind: "cli",
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 5678);
    }

    private static void AssertProgressEvents (
        CollectingProgressSink progressSink,
        params string[] eventNames)
    {
        Assert.Equal(eventNames.Length, progressSink.Entries.Count);
        for (var i = 0; i < eventNames.Length; i++)
        {
            Assert.Equal(eventNames[i], progressSink.Entries[i].EventName);
        }
    }

    private sealed class CollectingProgressSink : ICommandProgressSink
    {
        private readonly List<ProgressEntry> entries = [];
        private readonly Action? onEntry;

        public CollectingProgressSink (Action? onEntry = null)
        {
            this.onEntry = onEntry;
        }

        public IReadOnlyList<ProgressEntry> Entries => entries;

        public ValueTask OnEntryAsync<TPayload> (
            string eventName,
            TPayload payload,
            CancellationToken cancellationToken = default)
            where TPayload : notnull
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new ProgressEntry(eventName, payload));
            onEntry?.Invoke();
            return ValueTask.CompletedTask;
        }
    }

    private sealed record ProgressEntry (
        string EventName,
        object Payload);

    private sealed class StubSupervisorProcessLauncher : ISupervisorProcessLauncher
    {
        public ExecutionError? LaunchError { get; init; }

        public int LaunchCallCount { get; private set; }

        public ValueTask<ExecutionError?> LaunchAsync (
            string storageRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LaunchCallCount++;
            if (LaunchError != null)
            {
                return ValueTask.FromResult<ExecutionError?>(LaunchError);
            }

            throw new InvalidOperationException("Supervisor launch should not be used by this test.");
        }
    }
}
