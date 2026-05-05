using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Execution.Process;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class DaemonProbeExceptionClassifierTests
{
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
    public void IsNotRunning_WhenSocketException_ReturnsTrue ()
    {
        var result = DaemonProbeExceptionClassifier.IsNotRunning(
            new SocketException((int)SocketError.ConnectionRefused));

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsNotRunning_WhenSessionTokenInvalidResponse_ReturnsTrue ()
    {
        var result = DaemonProbeExceptionClassifier.IsNotRunning(
            new DaemonPingResponseException("token invalid", IpcErrorCodes.SessionTokenInvalid));

        Assert.True(result);
    }
}
