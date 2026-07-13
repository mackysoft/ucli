using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorProjectGatewayEnsureRunningTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenBootstrapConsumesBudget_PassesRemainingTimeoutToSupervisorClient ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "ensure-running-timeout");
        var timeProvider = new ManualTimeProvider();
        var scenario = await SupervisorProjectGatewayTestSupport.CreateManifestBackedScenarioAsync(
            scope.FullPath,
            timeProvider);
        var observedEnsureRunningTimeout = TimeSpan.Zero;
        var observedEditorMode = (string?)null;
        var observedOnStartupBlocked = (string?)null;
        scenario.TransportClient.SendHandler = (endpoint, request, timeout, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(request.Method, SupervisorIpcContracts.PingMethod, StringComparison.Ordinal))
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(180));
                return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    scenario.Manifest));
            }

            if (string.Equals(request.Method, SupervisorIpcContracts.EnsureRunningMethod, StringComparison.Ordinal))
            {
                var payload = SupervisorProjectGatewayTestSupport.ReadEnsureRunningRequest(request);
                observedEnsureRunningTimeout = TimeSpan.FromMilliseconds(payload.TimeoutMilliseconds);
                observedEditorMode = payload.EditorMode;
                observedOnStartupBlocked = payload.OnStartupBlocked;
                return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateStartedEnsureRunningResponse(request));
            }

            throw new InvalidOperationException($"Unexpected method: {request.Method}");
        };
        var progressSink = new CollectingCommandProgressSink();
        var progressEmitter = SupervisorProjectGatewayTestSupport.CreateStartProgressEmitter(
            progressSink,
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Keep);

        var result = await scenario.Gateway.EnsureRunningAsync(
            scenario.CreateUnityProject(),
            TimeSpan.FromMilliseconds(SupervisorProjectGatewayTestSupport.StartTimeoutMilliseconds),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Keep,
            progressObserver: progressEmitter,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.NotNull(result.Session);
        Assert.True(observedEnsureRunningTimeout > TimeSpan.Zero);
        Assert.True(observedEnsureRunningTimeout < TimeSpan.FromMilliseconds(SupervisorProjectGatewayTestSupport.StartTimeoutMilliseconds));
        Assert.Equal("gui", observedEditorMode);
        Assert.Equal("keep", observedOnStartupBlocked);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningCompleted));
        SupervisorProgressAssert.EnsureRunningCompletedSuccessfully(
            progressSink,
            expectedTimeoutMilliseconds: SupervisorProjectGatewayTestSupport.StartTimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WithSupervisorProgressSink_ForwardsSupervisorProgressThroughStreamClient ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "ensure-running-supervisor-progress");
        var scenario = await SupervisorProjectGatewayTestSupport.CreateManifestBackedScenarioAsync(scope.FullPath);
        scenario.TransportClient.SendHandler = (endpoint, request, timeout, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(SupervisorIpcContracts.PingMethod, request.Method);
            return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                request,
                scenario.Manifest));
        };
        scenario.TransportClient.StreamingHandler = async (endpoint, request, timeout, onProgressFrame, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(SupervisorIpcContracts.EnsureRunningMethod, request.Method);
            Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), request.ResponseMode);
            var payload = SupervisorProjectGatewayTestSupport.ReadEnsureRunningRequest(request);
            await onProgressFrame(
                    SupervisorClientTestSupport.CreateProgressFrame(
                        request,
                        ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint),
                        DaemonStartProgressEntryTestFactory.CreateStartupObservation(
                            projectFingerprint: payload.ProjectFingerprint,
                            timeoutMilliseconds: payload.TimeoutMilliseconds,
                            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                            startupStatus: DaemonStartupStatus.WaitingForEndpoint,
                            startupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration)),
                    cancellationToken)
                .ConfigureAwait(false);
            return SupervisorProjectGatewayTestSupport.CreateStartedEnsureRunningResponse(request);
        };
        var progressSink = new CollectingCommandProgressSink();

        var result = await scenario.Gateway.EnsureRunningAsync(
            scenario.CreateUnityProject(),
            TimeSpan.FromMilliseconds(SupervisorProjectGatewayTestSupport.StartTimeoutMilliseconds),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressObserver: null,
            supervisorProgressSink: progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        SupervisorProgressAssert.WaitingForEndpointProgressForwarded(
            progressSink,
            expectedProjectFingerprint: SupervisorProjectGatewayTestSupport.ProjectFingerprint);
        SupervisorTransportAssert.EnsureRunningRequestedWithUnboundedResponseWait(scenario.TransportClient);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenProgressObserverConsumesTime_DoesNotConsumeTimeoutBudget ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "ensure-running-progress-timeout");
        var timeProvider = new ManualTimeProvider();
        var scenario = await SupervisorProjectGatewayTestSupport.CreateManifestBackedScenarioAsync(
            scope.FullPath,
            timeProvider);
        var observedEnsureRunningTimeout = TimeSpan.Zero;
        scenario.TransportClient.SendHandler = (endpoint, request, timeout, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(request.Method, SupervisorIpcContracts.PingMethod, StringComparison.Ordinal))
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(180));
                return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    scenario.Manifest));
            }

            if (string.Equals(request.Method, SupervisorIpcContracts.EnsureRunningMethod, StringComparison.Ordinal))
            {
                var payload = SupervisorProjectGatewayTestSupport.ReadEnsureRunningRequest(request);
                observedEnsureRunningTimeout = TimeSpan.FromMilliseconds(payload.TimeoutMilliseconds);
                return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateStartedEnsureRunningResponse(request));
            }

            throw new InvalidOperationException($"Unexpected method: {request.Method}");
        };
        var progressSink = new CollectingCommandProgressSink(() => timeProvider.Advance(TimeSpan.FromMilliseconds(250)));
        var progressEmitter = SupervisorProjectGatewayTestSupport.CreateStartProgressEmitter(progressSink);

        var result = await scenario.Gateway.EnsureRunningAsync(
            scenario.CreateUnityProject(),
            TimeSpan.FromMilliseconds(SupervisorProjectGatewayTestSupport.StartTimeoutMilliseconds),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressObserver: progressEmitter,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(720), observedEnsureRunningTimeout);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningCompleted));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenSupervisorEnsureRunningFails_EmitsFailedProgressEntry ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "ensure-running-progress-failure");
        var scenario = await SupervisorProjectGatewayTestSupport.CreateManifestBackedScenarioAsync(scope.FullPath);
        scenario.TransportClient.SendHandler = (endpoint, request, timeout, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(request.Method, SupervisorIpcContracts.PingMethod, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    scenario.Manifest));
            }

            if (string.Equals(request.Method, SupervisorIpcContracts.EnsureRunningMethod, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(IpcResponseTestFactory.CreateError(
                    request,
                    ExecutionErrorCodes.IpcTimeout,
                    "ensureRunning timed out"));
            }

            throw new InvalidOperationException($"Unexpected method: {request.Method}");
        };
        var progressSink = new CollectingCommandProgressSink();
        var progressEmitter = SupervisorProjectGatewayTestSupport.CreateStartProgressEmitter(progressSink);

        var result = await scenario.Gateway.EnsureRunningAsync(
            scenario.CreateUnityProject(),
            TimeSpan.FromMilliseconds(SupervisorProjectGatewayTestSupport.StartTimeoutMilliseconds),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressObserver: progressEmitter,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningCompleted));
        SupervisorProgressAssert.EnsureRunningCompletedWithFailure(
            progressSink,
            expectedErrorCode: ExecutionErrorCodes.IpcTimeout.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenSupervisorBootstrapFails_EmitsFailedProgressEntry ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "bootstrap-progress-failure");
        var manifestStore = new SupervisorManifestStore();
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor IPC should not be used when bootstrap launch fails."),
        };
        var client = new SupervisorClient(transportClient);
        var launcher = new RecordingSupervisorProcessLauncher
        {
            LaunchError = ExecutionError.InternalError(
                "supervisor launch failed",
                UcliCoreErrorCodes.InternalError),
        };
        var gateway = SupervisorProjectGatewayTestSupport.CreateGateway(
            manifestStore,
            client,
            launcher);
        var progressSink = new CollectingCommandProgressSink();
        var progressEmitter = SupervisorProjectGatewayTestSupport.CreateStartProgressEmitter(progressSink);

        var result = await gateway.EnsureRunningAsync(
            SupervisorProjectGatewayTestSupport.CreateUnityProject(scope.FullPath),
            TimeSpan.FromMilliseconds(SupervisorProjectGatewayTestSupport.StartTimeoutMilliseconds),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressObserver: progressEmitter,
            cancellationToken: CancellationToken.None);

        SupervisorProjectGatewayAssert.BootstrapFailureProgressEmitted(
            result,
            launcher,
            progressSink,
            expectedStorageRoot: scope.FullPath,
            expectedErrorCode: UcliCoreErrorCodes.InternalError);
    }
}
