using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Execution.Mode.IpcDaemonPingClientTestSupport;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonPingClientFingerprintTests
{
    [Theory]
    [InlineData(nameof(IpcDaemonPingClient.PingAsync))]
    [InlineData(nameof(IpcDaemonPingClient.PingAndReadAsync))]
    [Trait("Size", "Small")]
    public async Task PingMethods_WhenProjectFingerprintMismatches_ThrowsDaemonPingResponseException (string methodName)
    {
        var unityIpcClient = new RecordingIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                IpcUnityEditorObservationTestFactory.Create(projectFingerprint: "different-fingerprint")));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, CreateResolvedSessionProvider());

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                InvokePingMethodAsync(pingClient, methodName).AsTask(),
                "Mismatched project fingerprint ping result",
                AsyncWaitTimeout);
        });

        Assert.Contains("projectFingerprint mismatch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenProjectFingerprintValidationIsDisabled_ReturnsMismatchedPayload ()
    {
        var unityIpcClient = new RecordingIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                IpcUnityEditorObservationTestFactory.Create(projectFingerprint: "different-fingerprint")));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, CreateResolvedSessionProvider());

        var result = await pingClient.PingAndReadAsync(
            CreateFingerprintMatchedProject(),
            DefaultTimeout,
            validateProjectFingerprint: false,
            cancellationToken: CancellationToken.None);

        Assert.Equal("different-fingerprint", result.ProjectFingerprint);
    }

    private static ValueTask InvokePingMethodAsync (
        IpcDaemonPingClient pingClient,
        string methodName)
    {
        return methodName switch
        {
            nameof(IpcDaemonPingClient.PingAsync) => pingClient.PingAsync(
                CreateFingerprintMatchedProject(),
                DefaultTimeout,
                cancellationToken: CancellationToken.None),
            nameof(IpcDaemonPingClient.PingAndReadAsync) => new ValueTask(pingClient.PingAndReadAsync(
                CreateFingerprintMatchedProject(),
                DefaultTimeout,
                cancellationToken: CancellationToken.None).AsTask()),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unsupported ping method."),
        };
    }
}
