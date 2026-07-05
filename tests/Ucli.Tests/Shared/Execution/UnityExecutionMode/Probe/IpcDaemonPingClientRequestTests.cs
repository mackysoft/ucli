using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Execution.Mode.IpcDaemonPingClientTestSupport;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonPingClientRequestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_SendsPingRequestWithProbeContract ()
    {
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        var pingClient = new IpcDaemonPingClient(unityIpcClient, CreateResolvedSessionProvider());

        await pingClient.PingAsync(CreateFingerprintMatchedProject(), DefaultTimeout, cancellationToken: CancellationToken.None);

        var request = DaemonIpcDispatchAssert.SingleDispatchSentToEndpointWithTimeout(
            unityIpcClient,
            expectedEndpointAddress: "/tmp/ucli-session.sock",
            expectedMethod: IpcMethodNames.Ping,
            expectedSessionToken: "resolved-token",
            expectedTimeout: DefaultTimeout);
        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.StartsWith("mode-probe-", request.RequestId, StringComparison.Ordinal);
        Assert.Equal("ucli-mode-probe", request.Payload.GetProperty("clientVersion").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenCanceled_ThrowsOperationCanceledException ()
    {
        var unityIpcClient = new UnexpectedIpcTransportClient("Canceled ping must stop before sending IPC requests.");
        var sessionConnectionProvider = new UnexpectedDaemonSessionConnectionProvider("Canceled ping must stop before resolving daemon session.");
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionConnectionProvider);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    cancellationToken: cancellationTokenSource.Token).AsTask(),
                "Canceled daemon ping",
                AsyncWaitTimeout);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WithProvidedSessionToken_UsesProvidedTokenAndPersistedEndpoint ()
    {
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        var pingClient = new IpcDaemonPingClient(unityIpcClient, CreateResolvedSessionProvider());

        await pingClient.PingAsync(CreateFingerprintMatchedProject(), DefaultTimeout, "provided-token", CancellationToken.None);

        DaemonIpcDispatchAssert.SingleDispatchSentToEndpoint(
            unityIpcClient,
            expectedEndpointAddress: "/tmp/ucli-session.sock",
            expectedMethod: IpcMethodNames.Ping,
            expectedSessionToken: "provided-token");
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Ping_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException (int timeoutMilliseconds)
    {
        var unityIpcClient = new UnexpectedIpcTransportClient("Invalid timeout must stop before sending IPC requests.");
        var sessionConnectionProvider = new UnexpectedDaemonSessionConnectionProvider("Invalid timeout must stop before resolving daemon session.");
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionConnectionProvider);
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAsync(
                    CreateFingerprintMatchedProject(),
                    timeout,
                    cancellationToken: CancellationToken.None).AsTask(),
                "Invalid timeout ping result",
                AsyncWaitTimeout);
        });
    }
}
