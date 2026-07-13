using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Execution.Mode.IpcDaemonPingClientTestSupport;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonPingClientPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_ReturnsDecodedPingPayload ()
    {
        var unityIpcClient = new RecordingIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                IpcUnityEditorObservationTestFactory.Create(
                    serverVersion: "0.5.0",
                    editorMode: DaemonEditorMode.Batchmode,
                    unityVersion: "2022.3.5f1",
                    projectFingerprint: "fingerprint",
                    compileState: IpcCompileState.Ready)));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, CreateResolvedSessionProvider());

        var result = await pingClient.PingAndReadAsync(
            CreateFingerprintMatchedProject(),
            DefaultTimeout,
            cancellationToken: CancellationToken.None);

        Assert.Equal("0.5.0", result.ServerVersion);
        Assert.Equal(DaemonEditorMode.Batchmode, result.State.EditorMode);
        Assert.Equal("2022.3.5f1", result.UnityVersion);
        Assert.Equal("fingerprint", result.ProjectFingerprint);
        Assert.Equal(IpcCompileState.Ready, result.State.CompileState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenPayloadIsInvalid_ThrowsDaemonPingResponseException ()
    {
        var unityIpcClient = new RecordingIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>()));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, CreateResolvedSessionProvider());

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAndReadAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    cancellationToken: CancellationToken.None).AsTask(),
                "Invalid ping payload result",
                AsyncWaitTimeout);
        });

        Assert.Contains("payload", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenEditorStateIsMissing_ThrowsDaemonPingResponseException ()
    {
        var unityIpcClient = new RecordingIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                new
                {
                    serverVersion = "0.5.0",
                    editorMode = "batchmode",
                    unityVersion = "2022.3.5f1",
                    projectFingerprint = "fingerprint",
                }));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, CreateResolvedSessionProvider());

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAndReadAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    cancellationToken: CancellationToken.None).AsTask(),
                "Missing editor state ping result",
                AsyncWaitTimeout);
        });

        Assert.Contains("payload", exception.Message, StringComparison.Ordinal);
    }
}
