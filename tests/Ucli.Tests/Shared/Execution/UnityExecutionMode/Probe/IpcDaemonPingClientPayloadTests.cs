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
                IpcPingResponseTestFactory.Create(
                    serverVersion: "0.5.0",
                    editorMode: "batchmode",
                    unityVersion: "2022.3.5f1",
                    projectFingerprint: "fingerprint",
                    compileState: "ready")));
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            CreateResolvedSessionProvider(),
            TimeProvider.System);

        var result = await pingClient.PingAndReadAsync(
            CreateFingerprintMatchedProject(),
            DefaultTimeout,
            validateProjectFingerprint: true,
            cancellationToken: CancellationToken.None);

        Assert.Equal("0.5.0", result.ServerVersion);
        Assert.Equal("batchmode", result.EditorMode);
        Assert.Equal("2022.3.5f1", result.UnityVersion);
        Assert.Equal("fingerprint", result.ProjectFingerprint);
        Assert.Equal("ready", result.CompileState);
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
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            CreateResolvedSessionProvider(),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAndReadAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    validateProjectFingerprint: true,
                    cancellationToken: CancellationToken.None).AsTask(),
                "Invalid ping payload result",
                AsyncWaitTimeout);
        });

        Assert.Contains("payload", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenCompileStateIsMissing_ReturnsPayload ()
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
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            CreateResolvedSessionProvider(),
            TimeProvider.System);

        var result = await pingClient.PingAndReadAsync(
            CreateFingerprintMatchedProject(),
            DefaultTimeout,
            validateProjectFingerprint: true,
            cancellationToken: CancellationToken.None);

        Assert.Equal("0.5.0", result.ServerVersion);
        Assert.Equal("batchmode", result.EditorMode);
        Assert.Equal("2022.3.5f1", result.UnityVersion);
        Assert.True(string.IsNullOrWhiteSpace(result.CompileState));
    }
}
