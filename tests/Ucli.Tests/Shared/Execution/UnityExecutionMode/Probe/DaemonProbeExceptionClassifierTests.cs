using System.Net.Sockets;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class DaemonProbeExceptionClassifierTests
{
    public static TheoryData<UcliCode> SessionAuthenticationErrorCodes =>
    [
        IpcSessionErrorCodes.SessionTokenRequired,
        IpcSessionErrorCodes.SessionTokenInvalid,
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void IsNotRunning_WhenTimeoutException_ReturnsFalse ()
    {
        var result = DaemonProbeExceptionClassifier.IsNotRunning(
            new TimeoutException("connect timeout"));

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNotRunning_WhenOperationCanceledException_ReturnsFalse ()
    {
        var result = DaemonProbeExceptionClassifier.IsNotRunning(
            new OperationCanceledException());

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNotRunning_WhenTypedConnectionIsRefused_ReturnsTrue ()
    {
        var result = DaemonProbeExceptionClassifier.IsNotRunning(
            new IpcConnectException(
                "IPC connection was refused before the request was sent.",
                new SocketException((int)SocketError.ConnectionRefused)));

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNotRunning_WhenTypedSocketFailureDoesNotProveEndpointAbsence_ReturnsFalse ()
    {
        var result = DaemonProbeExceptionClassifier.IsNotRunning(
            new IpcConnectException(
                "IPC connection failed before the request was sent.",
                new SocketException((int)SocketError.ConnectionReset)));

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNotRunning_WhenSocketExceptionLacksConnectionPhaseGuarantee_ReturnsFalse ()
    {
        var result = DaemonProbeExceptionClassifier.IsNotRunning(
            new SocketException((int)SocketError.ConnectionRefused));

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNotRunning_WhenDaemonSessionMetadataIsUnavailable_ReturnsTrue ()
    {
        var result = DaemonProbeExceptionClassifier.IsNotRunning(
            new DaemonSessionNotAvailableException("Daemon session is not available."));

        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(SessionAuthenticationErrorCodes))]
    [Trait("Size", "Small")]
    public void IsNotRunning_WhenSessionAuthenticationIsRejected_ReturnsFalse (UcliCode errorCode)
    {
        var result = DaemonProbeExceptionClassifier.IsNotRunning(
            new DaemonPingResponseException("session authentication rejected", errorCode));

        Assert.False(result);
    }
}
