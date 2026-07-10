using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Execution.Mode.IpcDaemonPingClientTestSupport;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonPingClientFailureTests
{
    public static TheoryData<PingResponseFailureCase> PingResponseFailureCases =>
    [
        new(
            "error status",
            request => CreateResponse(
                request,
                IpcProtocol.StatusError,
                Array.Empty<IpcError>())),
        new(
            "error entries",
            request => CreateResponse(
                request,
                IpcProtocol.StatusOk,
                [
                    new IpcError(
                        Code: UcliCoreErrorCodes.InvalidArgument,
                        Message: "invalid request",
                        OpId: null),
                ])),
    ];

    [Theory]
    [MemberData(nameof(PingResponseFailureCases))]
    [Trait("Size", "Small")]
    public async Task Ping_WhenResponseReportsFailure_ThrowsDaemonPingResponseException (PingResponseFailureCase testCase)
    {
        var unityIpcClient = new RecordingIpcTransportClient(testCase.CreateResponse);
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            CreateResolvedSessionProvider(),
            TimeProvider.System);

        await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    cancellationToken: CancellationToken.None).AsTask(),
                testCase.Name + " ping failure response",
                AsyncWaitTimeout);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenSessionIsNotAvailable_ThrowsDaemonPingResponseExceptionWithSessionTokenRequired ()
    {
        var unityIpcClient = new UnexpectedIpcTransportClient("Missing daemon session must stop before sending IPC requests.");
        var sessionConnectionProvider = new StaticDaemonSessionConnectionProvider(DaemonSessionConnectionResolutionResult.SessionNotAvailable());
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            sessionConnectionProvider,
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    cancellationToken: CancellationToken.None).AsTask(),
                "Missing session token ping result",
                AsyncWaitTimeout);
        });

        Assert.Equal(IpcSessionErrorCodes.SessionTokenRequired, exception.ErrorCode!.Value);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)ExecutionErrorKind.InvalidArgument)]
    [InlineData((int)ExecutionErrorKind.InternalError)]
    public async Task Ping_WhenSessionConnectionResolutionFailsForLocalError_ThrowsDaemonPingResponseExceptionWithoutTokenErrorCode (int errorKind)
    {
        var unityIpcClient = new UnexpectedIpcTransportClient("Failed daemon session resolution must stop before sending IPC requests.");
        var sessionConnectionProvider = new StaticDaemonSessionConnectionProvider(DaemonSessionConnectionResolutionResult.Failure(
            new ExecutionError((ExecutionErrorKind)errorKind, "session token read failed")));
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            sessionConnectionProvider,
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    cancellationToken: CancellationToken.None).AsTask(),
                "Session token resolution failure ping result",
                AsyncWaitTimeout);
        });

        Assert.Null(exception.ErrorCode);
        Assert.Contains("session token read failed", exception.Message, StringComparison.Ordinal);
    }

    public sealed record PingResponseFailureCase (
        string Name,
        Func<IpcRequest, IpcResponse> CreateResponse)
    {
        public override string ToString ()
        {
            return Name;
        }
    }
}
