using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorClientEnsureRunningTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_UsesOriginalOperationTimeoutAndUnboundedResponseWait ()
    {
        var observedOperationTimeoutMilliseconds = 0;
        var observedOnStartupBlocked = (string?)null;
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (endpoint, request, timeout, cancellationToken) =>
            {
                Assert.True(IpcPayloadCodec.TryDeserialize(
                    request.Payload,
                    out SupervisorIpcContracts.EnsureRunningRequest payload,
                    out _));
                observedOperationTimeoutMilliseconds = payload.TimeoutMilliseconds;
                observedOnStartupBlocked = payload.OnStartupBlocked;

                return ValueTask.FromResult(SupervisorClientTestSupport.CreateEnsureRunningResponse(
                    request,
                    lifecycleSnapshot: SupervisorClientTestSupport.CreateCompilingLifecycleSnapshot()));
            },
        };
        var client = new SupervisorClient(transportClient);
        var requestedTimeout = TimeSpan.FromSeconds(5);

        var result = await client.EnsureRunningAsync(
            SupervisorClientTestSupport.CreateManifest(),
            SupervisorClientTestSupport.CreateUnityProject(),
            requestedTimeout,
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Terminate,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IpcEditorLifecycleStateCodec.Compiling, result.LifecycleSnapshot!.LifecycleState);
        Assert.Equal(IpcEditorBlockingReasonCodec.Compile, result.LifecycleSnapshot.BlockingReason);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
        SupervisorTransportAssert.EnsureRunningRequestedWithUnboundedResponseWait(
            transportClient,
            requestedTimeout);
        Assert.Equal((int)requestedTimeout.TotalMilliseconds, observedOperationTimeoutMilliseconds);
        Assert.Equal("terminate", observedOnStartupBlocked);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenSupervisorReturnsAttached_ReturnsAttachedResult ()
    {
        var session = SupervisorClientTestSupport.CreateGuiDaemonSession();
        var lifecycleSnapshot = SupervisorClientTestSupport.CreateReadyLifecycleSnapshot();
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (endpoint, request, timeout, cancellationToken) => ValueTask.FromResult(
                SupervisorClientTestSupport.CreateEnsureRunningResponse(
                    request,
                    startStatus: "attached",
                    session: session,
                    lifecycleSnapshot: lifecycleSnapshot)),
        };
        var client = new SupervisorClient(transportClient);

        var result = await client.EnsureRunningAsync(
            SupervisorClientTestSupport.CreateManifest(),
            SupervisorClientTestSupport.CreateUnityProject(),
            TimeSpan.FromMilliseconds(100),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.Attached, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(lifecycleSnapshot, result.LifecycleSnapshot);
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
        var client = new SupervisorClient(transportClient);

        var result = await client.EnsureRunningAsync(
            SupervisorClientTestSupport.CreateManifest(),
            SupervisorClientTestSupport.CreateUnityProject(),
            TimeSpan.FromMilliseconds(100),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Equal(startup, result.Startup);
        Assert.Equal(DaemonStatusKind.Stale, result.DaemonStatus);
    }
}
