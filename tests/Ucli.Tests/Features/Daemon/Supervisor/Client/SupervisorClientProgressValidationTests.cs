using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorClientProgressValidationTests
{
    public static TheoryData<string, Func<IpcRequest, IpcStreamFrame>> InvalidProgressFrames => new()
    {
        {
            "unsupported progress event",
            request => SupervisorClientTestSupport.CreateProgressFrame(
                request,
                "daemon.start.unknown",
                new
                {
                    payloadKind = "startupObservation",
                })
        },
        {
            "payload kind mismatches event",
            request => SupervisorClientTestSupport.CreateProgressFrame(
                request,
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint),
                DaemonStartProgressEntryTestFactory.CreateStartupObservation(
                    payloadKind: ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.LifecycleSnapshot),
                    timeoutMilliseconds: 5000,
                    editorMode: "gui",
                    launchAttemptId: null,
                    ownerKind: "user",
                    canShutdownProcess: false,
                    processId: 42,
                    startupStatus: "waitingForEndpoint",
                    startupPhase: "endpointRegistration"))
        },
        {
            "known event has no stream payload contract",
            request => SupervisorClientTestSupport.CreateProgressFrame(
                request,
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed),
                new
                {
                    payloadKind = "startupObservation",
                })
        },
        {
            "progress envelope targets different project",
            request => SupervisorClientTestSupport.CreateProgressFrame(
                request,
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint),
                DaemonStartProgressEntryTestFactory.CreateStartupObservation(
                    projectFingerprint: ProjectFingerprintTestFactory.Create("other-fingerprint"),
                    timeoutMilliseconds: 5000,
                    editorMode: "gui",
                    launchAttemptId: null,
                    ownerKind: "user",
                    canShutdownProcess: false,
                    processId: 42,
                    startupStatus: "waitingForEndpoint",
                    startupPhase: "endpointRegistration"))
        },
    };

    [Theory]
    [MemberData(nameof(InvalidProgressFrames))]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithInvalidProgressFrame_DropsProgressAndPreservesTerminal (
        string caseName,
        Func<IpcRequest, IpcStreamFrame> createProgressFrame)
    {
        Assert.NotEmpty(caseName);

        var transportClient = new StreamingSupervisorTransportClient((request, onProgressFrame, cancellationToken) =>
        {
            return SupervisorClientTestSupport.ForwardProgressThenReturnStartedAsync(
                request,
                onProgressFrame,
                createProgressFrame(request),
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
        Assert.Empty(progressSink.Entries);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenProgressSinkThrows_PreservesTerminal ()
    {
        var transportClient = new StreamingSupervisorTransportClient((request, onProgressFrame, cancellationToken) =>
        {
            return SupervisorClientTestSupport.ForwardProgressThenReturnStartedAsync(
                request,
                onProgressFrame,
                SupervisorClientTestSupport.CreateWaitingForEndpointProgressFrame(request),
                cancellationToken);
        });
        var client = new SupervisorClient(transportClient, TimeProvider.System);

        var result = await client.EnsureRunningAsync(
            SupervisorClientTestSupport.CreateManifest(),
            Guid.NewGuid(),
            SupervisorClientTestSupport.CreateUnityProject(),
            SupervisorClientTestSupport.CreateDeadline(TimeSpan.FromSeconds(5)),
            attemptTimeout: TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            new ThrowingCommandProgressSink(new IOException("Simulated progress sink failure.")),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
