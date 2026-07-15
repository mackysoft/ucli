using System.Net.Sockets;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class DaemonIpcConnectionFailureClassifierTests
{
    public static TheoryData<Exception> RequestNotSentExceptions => new()
    {
        new IpcConnectException(
            "IPC connection failed before the request was sent.",
            new SocketException((int)SocketError.ConnectionRefused)),
        new IpcConnectException(
            "IPC connection failed before the request was sent.",
            new SocketException((int)SocketError.AddressNotAvailable)),
        new IpcConnectTimeoutException(
            "IPC connection timed out before the request was sent."),
    };

    public static TheoryData<Exception> UntypedSocketExceptions => new()
    {
        new SocketException((int)SocketError.ConnectionRefused),
        new SocketException((int)SocketError.AddressNotAvailable),
    };

    [Theory]
    [MemberData(nameof(RequestNotSentExceptions))]
    [Trait("Size", "Small")]
    public void IsRetryableBeforeRequestWrite_WithConnectionPhaseException_ReturnsTrue (Exception exception)
    {
        Assert.True(DaemonIpcConnectionFailureClassifier.IsRetryableBeforeRequestWrite(exception));
    }

    [Theory]
    [MemberData(nameof(UntypedSocketExceptions))]
    [Trait("Size", "Small")]
    public void IsRetryableBeforeRequestWrite_WithUntypedSocketException_ReturnsFalse (Exception exception)
    {
        Assert.False(DaemonIpcConnectionFailureClassifier.IsRetryableBeforeRequestWrite(exception));
    }
}
