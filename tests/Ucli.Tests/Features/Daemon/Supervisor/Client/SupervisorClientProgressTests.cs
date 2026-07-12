using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorClientProgressTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithProgressSink_UsesStreamResponseModeAndForwardsStartupObservation ()
    {
        var transportClient = new StreamingSupervisorTransportClient((request, onProgressFrame, cancellationToken) =>
        {
            Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), request.ResponseMode);

            return SupervisorClientTestSupport.ForwardProgressThenReturnStartedAsync(
                request,
                onProgressFrame,
                SupervisorClientTestSupport.CreateWaitingForEndpointProgressFrame(
                    request,
                    onStartupBlocked: "terminate",
                    message: "Waiting for daemon endpoint."),
                cancellationToken);
        });
        var progressSink = new CollectingCommandProgressSink();
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var result = await client.EnsureRunningAsync(
            SupervisorClientTestSupport.CreateManifest(),
            Guid.NewGuid(),
            SupervisorClientTestSupport.CreateUnityProject(),
            SupervisorClientTestSupport.CreateDeadline(TimeSpan.FromSeconds(5)),
            attemptTimeout: TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Terminate,
            progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        transportClient.AssertEnsureRunningStreamingRequested();
        SupervisorProgressAssert.WaitingForEndpointProgressForwarded(
            progressSink,
            expectedProjectFingerprint: "fingerprint",
            expectedMessage: "Waiting for daemon endpoint.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithProgressSink_ForwardsLifecycleSnapshot ()
    {
        var transportClient = new StreamingSupervisorTransportClient((request, onProgressFrame, cancellationToken) =>
        {
            return SupervisorClientTestSupport.ForwardProgressThenReturnStartedAsync(
                request,
                onProgressFrame,
                SupervisorClientTestSupport.CreateLifecycleSnapshotProgressFrame(request),
                cancellationToken);
        });
        var progressSink = new CollectingCommandProgressSink();
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var result = await client.EnsureRunningAsync(
            SupervisorClientTestSupport.CreateManifest(),
            Guid.NewGuid(),
            SupervisorClientTestSupport.CreateUnityProject(),
            SupervisorClientTestSupport.CreateDeadline(TimeSpan.FromSeconds(5)),
            attemptTimeout: TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        SupervisorProgressAssert.LifecycleSnapshotProgressForwarded(progressSink);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithProgressSink_WhenTerminalFails_PreservesFailureMetadata ()
    {
        var diagnosis = DaemonDiagnosisTestFactory.CreateGuiEndpointNotRegistered();
        var startup = SupervisorClientTestSupport.CreateStartupObservation();
        var transportClient = new StreamingSupervisorTransportClient((request, onProgressFrame, cancellationToken) => ValueTask.FromResult(
            SupervisorClientTestSupport.CreateEnsureRunningFailureResponse(request, diagnosis, startup)));
        var progressSink = new CollectingCommandProgressSink();
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var result = await client.EnsureRunningAsync(
            SupervisorClientTestSupport.CreateManifest(),
            Guid.NewGuid(),
            SupervisorClientTestSupport.CreateUnityProject(),
            SupervisorClientTestSupport.CreateDeadline(TimeSpan.FromSeconds(5)),
            attemptTimeout: TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Equal(startup, result.Startup);
        Assert.Equal(DaemonStatusKind.Stale, result.DaemonStatus);
        Assert.Empty(progressSink.Entries);
        transportClient.AssertEnsureRunningStreamingRequested();
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenStreamingTerminalResponseNeverCompletes_ReturnsTimeoutAfterFiniteGrace ()
    {
        var terminalResponseSource = new TaskCompletionSource<IpcResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new StreamingSupervisorTransportClient(
            (_, _, cancellationToken) =>
            {
                _ = cancellationToken.Register(() => cancellationObserved.TrySetResult());
                return new ValueTask<IpcResponse>(terminalResponseSource.Task);
            });
        var client = new SupervisorClient(transportClient, TimeProvider.System);
        var resultTask = client.EnsureRunningAsync(
                SupervisorClientTestSupport.CreateManifest(),
                Guid.NewGuid(),
                SupervisorClientTestSupport.CreateUnityProject(),
                SupervisorClientTestSupport.CreateDeadline(TimeSpan.FromMilliseconds(1)),
                attemptTimeout: TimeSpan.FromMilliseconds(1),
                editorMode: DaemonEditorMode.Gui,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                new CollectingCommandProgressSink(),
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
            terminalResponseSource.TrySetException(new TimeoutException("Release non-cooperative streaming response."));
            _ = await resultTask.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Contains("terminal response", result.Error.Message, StringComparison.Ordinal);
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
