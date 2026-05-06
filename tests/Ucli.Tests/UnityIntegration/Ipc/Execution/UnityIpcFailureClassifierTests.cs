using System.Net.Sockets;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcFailureClassifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FromDaemonDispatchException_WithTimeout_ReturnsIpcTimeout ()
    {
        var failure = UnityIpcFailureClassifier.FromDaemonDispatchException(
            new TimeoutException("timed out"),
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(ExecutionErrorCodes.IpcTimeout, failure.Code);
        Assert.Equal(ApplicationOutcome.ToolError, failure.Outcome);
        Assert.Contains("500 milliseconds", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromDaemonDispatchException_WithSocketException_ReturnsDaemonNotRunning ()
    {
        var failure = UnityIpcFailureClassifier.FromDaemonDispatchException(
            new SocketException((int)SocketError.ConnectionRefused),
            TimeSpan.FromSeconds(1));

        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, failure.Code);
        Assert.Equal(ApplicationOutcome.ToolError, failure.Outcome);
        Assert.Contains("Unity daemon is not running.", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromExecutionError_WithInvalidArgument_ReturnsInvalidArgumentOutcome ()
    {
        var failure = UnityIpcFailureClassifier.FromExecutionError(
            ExecutionError.InvalidArgument("Plugin marker is missing."));

        Assert.Equal(IpcErrorCodes.InvalidArgument, failure.Code);
        Assert.Equal(ApplicationOutcome.InvalidArgument, failure.Outcome);
        Assert.Equal("Plugin marker is missing.", failure.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromModeDecisionContractError_WithOneshotForbidden_ReturnsToolErrorOutcome ()
    {
        var failure = UnityIpcFailureClassifier.FromModeDecisionContractError(
            new UnityExecutionModeDecisionContractError(
                UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden,
                "Daemon is running for mode=oneshot."));

        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden, failure.Code);
        Assert.Equal(ApplicationOutcome.ToolError, failure.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromOneshotDispatchException_WithUnexpectedException_ReturnsInternalError ()
    {
        var failure = UnityIpcFailureClassifier.FromOneshotDispatchException(
            new InvalidOperationException("boom"),
            TimeSpan.FromSeconds(1));

        Assert.Equal(IpcErrorCodes.InternalError, failure.Code);
        Assert.Equal(ApplicationOutcome.ToolError, failure.Outcome);
        Assert.Contains("Failed to execute Unity oneshot IPC request. boom", failure.Message, StringComparison.Ordinal);
    }
}
