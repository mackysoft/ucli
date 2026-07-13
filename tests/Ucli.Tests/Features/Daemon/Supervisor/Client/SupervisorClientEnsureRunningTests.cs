using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorClientEnsureRunningTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_UsesSharedOperationDeadlineAndTerminalResponseGrace ()
    {
        var timeProvider = new ManualTimeProvider();
        var observedDeadlineUtc = default(DateTimeOffset);
        var observedAttemptTimeoutMilliseconds = 0;
        var observedOnStartupBlocked = (string?)null;
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (endpoint, request, timeout, cancellationToken) =>
            {
                Assert.True(IpcPayloadCodec.TryDeserialize(
                    request.Payload,
                    out SupervisorIpcContracts.EnsureRunningRequest payload,
                    out _));
                observedDeadlineUtc = payload.DeadlineUtc;
                observedAttemptTimeoutMilliseconds = payload.AttemptTimeoutMilliseconds;
                observedOnStartupBlocked = payload.OnStartupBlocked;

                return ValueTask.FromResult(SupervisorClientTestSupport.CreateEnsureRunningResponse(
                    request,
                    lifecycleObservation: SupervisorClientTestSupport.CreateCompilingLifecycleObservation()));
            },
        };
        var client = new SupervisorClient(transportClient, timeProvider);
        var requestedTimeout = TimeSpan.FromSeconds(5);

        var result = await client.EnsureRunningAsync(
            SupervisorClientTestSupport.CreateManifest(),
            Guid.NewGuid(),
            SupervisorClientTestSupport.CreateUnityProject(),
            timeProvider.GetUtcNow().Add(requestedTimeout),
            attemptTimeout: requestedTimeout,
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Terminate,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IpcEditorLifecycleState.Compiling, result.LifecycleObservation!.State.LifecycleState);
        Assert.Equal(
            IpcEditorBlockingReason.Compile,
            IpcEditorLifecycleSemantics.ResolveBlockingReason(result.LifecycleObservation.State.LifecycleState));
        Assert.False(IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(result.LifecycleObservation.State.LifecycleState));
        SupervisorTransportAssert.EnsureRunningRequestedWithUnboundedResponseWait(
            transportClient,
            requestedTimeout);
        Assert.Equal(timeProvider.GetUtcNow().Add(requestedTimeout), observedDeadlineUtc);
        Assert.Equal(checked((int)requestedTimeout.TotalMilliseconds), observedAttemptTimeoutMilliseconds);
        Assert.Equal("terminate", observedOnStartupBlocked);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenSupervisorReturnsAttached_ReturnsAttachedResult ()
    {
        var session = SupervisorClientTestSupport.CreateGuiDaemonSession();
        var lifecycleObservation = SupervisorClientTestSupport.CreateReadyLifecycleObservation();
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (endpoint, request, timeout, cancellationToken) => ValueTask.FromResult(
                SupervisorClientTestSupport.CreateEnsureRunningResponse(
                    request,
                    startStatus: "attached",
                    session: session,
                    lifecycleObservation: lifecycleObservation)),
        };
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var result = await client.EnsureRunningAsync(
            SupervisorClientTestSupport.CreateManifest(),
            Guid.NewGuid(),
            SupervisorClientTestSupport.CreateUnityProject(),
            SupervisorClientTestSupport.CreateDeadline(TimeSpan.FromMilliseconds(100)),
            attemptTimeout: TimeSpan.FromMilliseconds(100),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.Attached, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(lifecycleObservation, result.LifecycleObservation);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenResponseSessionTargetsDifferentProject_ReturnsInternalError ()
    {
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, request, _, _) => ValueTask.FromResult(
                SupervisorClientTestSupport.CreateEnsureRunningResponse(request)),
        };
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var result = await client.EnsureRunningAsync(
            SupervisorClientTestSupport.CreateManifest(),
            Guid.NewGuid(),
            SupervisorClientTestSupport.CreateUnityProject(ProjectFingerprintTestFactory.Create("different-project-fingerprint")),
            SupervisorClientTestSupport.CreateDeadline(TimeSpan.FromMilliseconds(100)),
            attemptTimeout: TimeSpan.FromMilliseconds(100),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("projectFingerprint mismatch", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenFailurePayloadContainsDiagnosisAndStartup_ReturnsFailureWithMetadata ()
    {
        var diagnosis = DaemonDiagnosisTestFactory.CreateGuiEndpointNotRegistered();
        var startup = SupervisorClientTestSupport.CreateStartupObservation();
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (endpoint, request, timeout, cancellationToken) => ValueTask.FromResult(
                SupervisorClientTestSupport.CreateEnsureRunningFailureResponse(request, diagnosis, startup)),
        };
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var result = await client.EnsureRunningAsync(
            SupervisorClientTestSupport.CreateManifest(),
            Guid.NewGuid(),
            SupervisorClientTestSupport.CreateUnityProject(),
            SupervisorClientTestSupport.CreateDeadline(TimeSpan.FromMilliseconds(100)),
            attemptTimeout: TimeSpan.FromMilliseconds(100),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Equal(startup, result.Startup);
        Assert.Equal(DaemonStatusKind.Stale, result.DaemonStatus);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenFailureTerminalArrivesAfterCommandTimeoutWithinGrace_PreservesMetadata ()
    {
        var diagnosis = DaemonDiagnosisTestFactory.CreateGuiEndpointNotRegistered();
        var startup = SupervisorClientTestSupport.CreateStartupObservation();
        var requestObserved = new TaskCompletionSource<IpcRequest>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalResponseSource = new TaskCompletionSource<IpcResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, request, _, _) =>
            {
                requestObserved.TrySetResult(request);
                return new ValueTask<IpcResponse>(terminalResponseSource.Task);
            },
        };
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var resultTask = client.EnsureRunningAsync(
                SupervisorClientTestSupport.CreateManifest(),
                Guid.NewGuid(),
                SupervisorClientTestSupport.CreateUnityProject(),
                SupervisorClientTestSupport.CreateDeadline(TimeSpan.FromMilliseconds(1)),
                attemptTimeout: TimeSpan.FromMilliseconds(1),
                editorMode: DaemonEditorMode.Gui,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        var request = await requestObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(TimeSpan.FromMilliseconds(25));
        terminalResponseSource.TrySetResult(
            SupervisorClientTestSupport.CreateEnsureRunningFailureResponse(request, diagnosis, startup));

        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Equal(startup, result.Startup);
        Assert.Equal(DaemonStatusKind.Stale, result.DaemonStatus);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenSingleTerminalResponseNeverCompletes_ReturnsTimeoutAfterFiniteGrace ()
    {
        var terminalResponseSource = new TaskCompletionSource<IpcResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, _, _, cancellationToken) =>
            {
                _ = cancellationToken.Register(() => cancellationObserved.TrySetResult());
                return new ValueTask<IpcResponse>(terminalResponseSource.Task);
            },
        };
        var client = new SupervisorClient(transportClient, TimeProvider.System);
        var resultTask = client.EnsureRunningAsync(
                SupervisorClientTestSupport.CreateManifest(),
                Guid.NewGuid(),
                SupervisorClientTestSupport.CreateUnityProject(),
                SupervisorClientTestSupport.CreateDeadline(TimeSpan.FromMilliseconds(1)),
                attemptTimeout: TimeSpan.FromMilliseconds(1),
                editorMode: DaemonEditorMode.Gui,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();

        DaemonStartResult result;
        try
        {
            result = await resultTask.WaitAsync(
                SupervisorConstants.EnsureRunningTerminalResponseGrace + TimeSpan.FromSeconds(3));
        }
        finally
        {
            terminalResponseSource.TrySetException(new TimeoutException("Release non-cooperative single response."));
            _ = await resultTask.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Contains("terminal response", result.Error.Message, StringComparison.Ordinal);
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenTimeoutExceedsIpcMillisecondContract_ThrowsBeforeTransport ()
    {
        var transportClient = new StubIpcTransportClient();
        var client = new SupervisorClient(transportClient, TimeProvider.System);
        var timeout = TimeSpan.FromMilliseconds(int.MaxValue).Add(TimeSpan.FromMinutes(1));

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client
            .EnsureRunningAsync(
                SupervisorClientTestSupport.CreateManifest(),
                Guid.NewGuid(),
                SupervisorClientTestSupport.CreateUnityProject(),
                SupervisorClientTestSupport.CreateDeadline(timeout),
                attemptTimeout: timeout,
                editorMode: DaemonEditorMode.Gui,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask());

        Assert.Equal("attemptTimeout", exception.ParamName);
        Assert.Empty(transportClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenRequestIdIsEmpty_ThrowsBeforeTransport ()
    {
        var transportClient = new StubIpcTransportClient();
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => client
            .EnsureRunningAsync(
                SupervisorClientTestSupport.CreateManifest(),
                Guid.Empty,
                SupervisorClientTestSupport.CreateUnityProject(),
                SupervisorClientTestSupport.CreateDeadline(TimeSpan.FromSeconds(1)),
                attemptTimeout: TimeSpan.FromSeconds(1),
                editorMode: DaemonEditorMode.Gui,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask());

        Assert.Equal("requestId", exception.ParamName);
        Assert.Empty(transportClient.Invocations);
    }
}
