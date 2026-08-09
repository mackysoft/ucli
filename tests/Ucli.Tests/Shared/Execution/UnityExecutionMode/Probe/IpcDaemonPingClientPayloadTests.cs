using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Execution.Mode.IpcDaemonPingClientTestSupport;
using MackySoft.Ucli.Contracts.Editor;

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
                IpcResponseStatus.Ok,
                Array.Empty<IpcError>(),
                UnityEditorObservationTestFactory.Create(
                    serverVersion: "0.5.0",
                    editorMode: UnityEditorMode.Batchmode,
                    unityVersion: "2022.3.5f1",
                    projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
                    compileState: UnityEditorCompileState.Ready)));
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(CreateResolvedSessionStore("resolved-token")),
            TimeProvider.System);

        var result = await pingClient.PingAndReadAsync(
            CreateFingerprintMatchedProject(),
            DefaultTimeout,
            validateProjectFingerprint: true,
            cancellationToken: CancellationToken.None);

        Assert.Equal("0.5.0", result.ServerVersion);
        Assert.Equal(UnityEditorMode.Batchmode, result.State.EditorMode);
        Assert.Equal("2022.3.5f1", result.UnityVersion);
        Assert.Equal(ProjectFingerprintTestFactory.Create("fingerprint"), result.ProjectFingerprint);
        Assert.Equal(UnityEditorCompileState.Ready, result.State.CompileState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenPayloadIsInvalid_ThrowsDaemonPingResponseException ()
    {
        var unityIpcClient = new RecordingIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcResponseStatus.Ok,
                Array.Empty<IpcError>()));
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(CreateResolvedSessionStore("resolved-token")),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await pingClient.PingAndReadAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    validateProjectFingerprint: true,
                    cancellationToken: CancellationToken.None).AsTask().WaitAsync(AsyncWaitTimeout);
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
                IpcResponseStatus.Ok,
                Array.Empty<IpcError>(),
                new
                {
                    serverVersion = "0.5.0",
                    editorMode = "batchmode",
                    unityVersion = "2022.3.5f1",
                    projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint").ToString(),
                }));
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(CreateResolvedSessionStore("resolved-token")),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await pingClient.PingAndReadAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    validateProjectFingerprint: true,
                    cancellationToken: CancellationToken.None).AsTask().WaitAsync(AsyncWaitTimeout);
        });

        Assert.Contains("payload", exception.Message, StringComparison.Ordinal);
    }
}
