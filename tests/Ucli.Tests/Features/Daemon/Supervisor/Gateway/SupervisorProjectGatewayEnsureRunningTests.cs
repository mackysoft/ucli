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
    public async Task EnsureRunning_WhenSupervisorTokenRotates_ReloadsManifestAndReplaysSameRequestOnce ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "supervisor-project-gateway",
            "ensure-running-token-rotation");
        var timeProvider = new ManualTimeProvider();
        var scenario = await SupervisorProjectGatewayTestSupport.CreateManifestBackedScenarioAsync(
            scope.FullPath,
            timeProvider);
        var successorManifest = SupervisorClientTestSupport.CreateSuccessorManifest(
            scenario.Manifest,
            sessionTokenDiscriminator: 2);
        var ensureRunningAttempt = 0;
        scenario.TransportClient.SendHandler = async (_, request, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(request.Method, TextVocabulary.GetText(SupervisorIpcMethod.Ping), StringComparison.Ordinal))
            {
                return SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    scenario.Manifest);
            }

            Assert.Equal(TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning), request.Method);
            if (Interlocked.Increment(ref ensureRunningAttempt) == 1)
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                await scenario.ManifestStore.WriteAsync(
                    scope.FullPath,
                    successorManifest,
                    cancellationToken);
                return IpcResponseTestFactory.CreateError(
                    request,
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "Initial supervisor token is invalid.");
            }

            return SupervisorProjectGatewayTestSupport.CreateStartedEnsureRunningResponse(request);
        };

        var result = await scenario.Gateway.EnsureRunningAsync(
            scenario.CreateUnityProject(),
            TimeSpan.FromSeconds(1),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var requests = scenario.TransportClient.Invocations
            .Select(static invocation => invocation.Request)
            .Where(static request => request.Method == TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning))
            .ToArray();
        IpcRequestAssert.SessionTokens(
            requests,
            scenario.Manifest.SessionToken.GetEncodedValue(),
            successorManifest.SessionToken.GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
        var firstPayload = SupervisorProjectGatewayTestSupport.ReadEnsureRunningRequest(requests[0]);
        var replayPayload = SupervisorProjectGatewayTestSupport.ReadEnsureRunningRequest(requests[1]);
        Assert.Equal(requests[0].RequestDeadlineUtc, requests[1].RequestDeadlineUtc);
        Assert.True(
            requests[0].RequestDeadlineRemainingMilliseconds
            > requests[1].RequestDeadlineRemainingMilliseconds);
        Assert.Equal(firstPayload, replayPayload);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenSuccessorTokenIsAlsoRejected_DoesNotDispatchThirdRequest ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "supervisor-project-gateway",
            "ensure-running-successor-token-rejected");
        var scenario = await SupervisorProjectGatewayTestSupport.CreateManifestBackedScenarioAsync(scope.FullPath);
        var successorManifest = SupervisorClientTestSupport.CreateSuccessorManifest(
            scenario.Manifest,
            sessionTokenDiscriminator: 2);
        var ensureRunningAttempt = 0;
        scenario.TransportClient.SendHandler = async (_, request, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(request.Method, TextVocabulary.GetText(SupervisorIpcMethod.Ping), StringComparison.Ordinal))
            {
                return SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    scenario.Manifest);
            }

            Assert.Equal(TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning), request.Method);
            if (Interlocked.Increment(ref ensureRunningAttempt) == 1)
            {
                await scenario.ManifestStore.WriteAsync(
                    scope.FullPath,
                    successorManifest,
                    cancellationToken);
            }

            return IpcResponseTestFactory.CreateError(
                request,
                IpcSessionErrorCodes.SessionTokenInvalid,
                "Supervisor token is invalid.");
        };

        var result = await scenario.Gateway.EnsureRunningAsync(
            scenario.CreateUnityProject(),
            TimeSpan.FromSeconds(1),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcSessionErrorCodes.SessionTokenInvalid, result.Error!.Code);
        var requests = scenario.TransportClient.Invocations
            .Select(static invocation => invocation.Request)
            .Where(static request => request.Method == TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning))
            .ToArray();
        IpcRequestAssert.SessionTokens(
            requests,
            scenario.Manifest.SessionToken.GetEncodedValue(),
            successorManifest.SessionToken.GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

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
        var observedEditorMode = (DaemonEditorMode?)null;
        var observedOnStartupBlocked = DaemonStartupBlockedProcessPolicy.Auto;
        scenario.TransportClient.SendHandler = (endpoint, request, timeout, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.Equals(request.Method, TextVocabulary.GetText(SupervisorIpcMethod.Ping), StringComparison.Ordinal))
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(180));
                return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    scenario.Manifest));
            }

            if (string.Equals(request.Method, TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning), StringComparison.Ordinal))
            {
                var payload = SupervisorProjectGatewayTestSupport.ReadEnsureRunningRequest(request);
                observedEnsureRunningTimeout = TimeSpan.FromMilliseconds(request.RequestDeadlineRemainingMilliseconds);
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
        Assert.Equal(DaemonEditorMode.Gui, observedEditorMode);
        Assert.Equal(DaemonStartupBlockedProcessPolicy.Keep, observedOnStartupBlocked);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            TextVocabulary.GetText(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            TextVocabulary.GetText(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
            TextVocabulary.GetText(DaemonStartProgressEvent.EnsureRunningStarted),
            TextVocabulary.GetText(DaemonStartProgressEvent.EnsureRunningCompleted));
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
            Assert.Equal(TextVocabulary.GetText(SupervisorIpcMethod.Ping), request.Method);
            return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                request,
                scenario.Manifest));
        };
        scenario.TransportClient.StreamingHandler = async (endpoint, request, timeout, onProgressFrame, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning), request.Method);
            Assert.Equal(TextVocabulary.GetText(IpcResponseMode.Stream), request.ResponseMode);
            var payload = SupervisorProjectGatewayTestSupport.ReadEnsureRunningRequest(request);
            await onProgressFrame(
                    SupervisorClientTestSupport.CreateProgressFrame(
                        request,
                        TextVocabulary.GetText(DaemonStartProgressEvent.WaitingForEndpoint),
                        DaemonStartProgressEntryTestFactory.CreateStartupObservation(
                            projectFingerprint: payload.ProjectFingerprint,
                            timeoutMilliseconds: request.RequestDeadlineRemainingMilliseconds,
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

            if (string.Equals(request.Method, TextVocabulary.GetText(SupervisorIpcMethod.Ping), StringComparison.Ordinal))
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(180));
                return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    scenario.Manifest));
            }

            if (string.Equals(request.Method, TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning), StringComparison.Ordinal))
            {
                _ = SupervisorProjectGatewayTestSupport.ReadEnsureRunningRequest(request);
                observedEnsureRunningTimeout = TimeSpan.FromMilliseconds(request.RequestDeadlineRemainingMilliseconds);
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
            TextVocabulary.GetText(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            TextVocabulary.GetText(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
            TextVocabulary.GetText(DaemonStartProgressEvent.EnsureRunningStarted),
            TextVocabulary.GetText(DaemonStartProgressEvent.EnsureRunningCompleted));
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

            if (string.Equals(request.Method, TextVocabulary.GetText(SupervisorIpcMethod.Ping), StringComparison.Ordinal))
            {
                return ValueTask.FromResult(SupervisorProjectGatewayTestSupport.CreateSupervisorPingResponse(
                    request,
                    scenario.Manifest));
            }

            if (string.Equals(request.Method, TextVocabulary.GetText(SupervisorIpcMethod.EnsureRunning), StringComparison.Ordinal))
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
            TextVocabulary.GetText(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            TextVocabulary.GetText(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
            TextVocabulary.GetText(DaemonStartProgressEvent.EnsureRunningStarted),
            TextVocabulary.GetText(DaemonStartProgressEvent.EnsureRunningCompleted));
        SupervisorProgressAssert.EnsureRunningCompletedWithFailure(
            progressSink,
            expectedErrorCode: ExecutionErrorCodes.IpcTimeout.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureRunning_WhenSupervisorBootstrapFails_EmitsFailedProgressEntry ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-project-gateway", "bootstrap-progress-failure");
        var timeProvider = new ManualTimeProvider();
        var manifestStore = SupervisorManifestStoreTestSupport.CreateFileBacked(timeProvider);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor IPC should not be used when bootstrap launch fails."),
        };
        var client = new SupervisorClient(transportClient, timeProvider);
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchError = ExecutionError.InternalError(
                "supervisor launch failed",
                UcliCoreErrorCodes.InternalError),
        };
        var gateway = SupervisorProjectGatewayTestSupport.CreateGateway(
            manifestStore,
            client,
            processManager,
            timeProvider);
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
            processManager,
            progressSink,
            expectedStorageRoot: scope.FullPath,
            expectedErrorCode: UcliCoreErrorCodes.InternalError);
    }
}
