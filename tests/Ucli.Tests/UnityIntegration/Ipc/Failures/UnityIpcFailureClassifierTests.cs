using System.Net.Sockets;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcFailureClassifierTests
{
    public static TheoryData<string, Func<Exception>> TransportInterruptedOneshotExceptions => new()
    {
        {
            "io-exception",
            static () => new IpcResponseReadInterruptedException(new IOException("Pipe is broken."))
        },
        {
            "end-of-stream",
            static () => new IpcResponseReadInterruptedException(
                new EndOfStreamException("IPC stream ended before a complete frame was read."))
        },
    };

    private static readonly UcliCode[] PlanTokenValidationCodes =
    [
        PlanTokenErrorCodes.PlanTokenRequired,
        PlanTokenErrorCodes.PlanTokenInvalid,
        PlanTokenErrorCodes.PlanTokenExpired,
        PlanTokenErrorCodes.PlanTokenRequestMismatch,
        PlanTokenErrorCodes.StateChangedSincePlan,
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void FromDaemonDispatchException_WithTimeout_ReturnsIpcTimeout ()
    {
        var failure = UnityIpcFailureClassifier.FromDaemonDispatchException(
            new TimeoutException("timed out"),
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(ExecutionErrorCodes.IpcTimeout, failure.Code);
        Assert.Equal(ApplicationOutcome.ToolError, ApplicationFailure.FromCode(failure.Code, failure.Message).Outcome);
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
        Assert.Equal(ApplicationOutcome.ToolError, ApplicationFailure.FromCode(failure.Code, failure.Message).Outcome);
        Assert.Contains("Unity daemon is not running.", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromExecutionError_WithInvalidArgument_ReturnsInvalidArgumentCode ()
    {
        var failure = UnityIpcFailureClassifier.FromExecutionError(
            ExecutionError.InvalidArgument("Plugin marker is missing."));

        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, failure.Code);
        Assert.Equal(ApplicationOutcome.InvalidArgument, ApplicationFailure.FromCode(failure.Code, failure.Message).Outcome);
        Assert.Equal("Plugin marker is missing.", failure.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromCodeAndMessage_WithPlanTokenValidationCode_ReturnsCode ()
    {
        foreach (UcliCode code in PlanTokenValidationCodes)
        {
            var failure = UnityIpcFailureClassifier.FromCodeAndMessage(
                code,
                "Plan token validation failed.");

            Assert.Equal(code, failure.Code);
            Assert.Equal(ApplicationOutcome.InvalidArgument, ApplicationFailure.FromCode(failure.Code, failure.Message).Outcome);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromModeDecisionContractError_WithOneshotForbidden_ReturnsCode ()
    {
        var failure = UnityIpcFailureClassifier.FromModeDecisionContractError(
            new UnityExecutionModeDecisionContractError(
                UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden,
                "Daemon is running for mode=oneshot."));

        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden, failure.Code);
        Assert.Equal(ApplicationOutcome.ToolError, ApplicationFailure.FromCode(failure.Code, failure.Message).Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromOneshotDispatchException_WithUnexpectedException_ReturnsInternalError ()
    {
        var failure = UnityIpcFailureClassifier.FromOneshotDispatchException(
            new InvalidOperationException("boom"),
            TimeSpan.FromSeconds(1));

        Assert.Equal(UcliCoreErrorCodes.InternalError, failure.Code);
        Assert.Equal(UnityRequestFailureKind.General, failure.FailureKind);
        Assert.Equal(ApplicationOutcome.ToolError, ApplicationFailure.FromCode(failure.Code, failure.Message).Outcome);
        Assert.Contains("Failed to execute Unity oneshot IPC request. boom", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromOneshotDispatchException_WithNonTransportIOException_ReturnsGeneralFailure ()
    {
        var failure = UnityIpcFailureClassifier.FromOneshotDispatchException(
            new IOException("Lifecycle lock file could not be read."),
            TimeSpan.FromSeconds(1));

        Assert.Equal(UnityRequestFailureKind.General, failure.FailureKind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, failure.Code);
    }

    [Theory]
    [MemberData(nameof(TransportInterruptedOneshotExceptions))]
    [Trait("Size", "Small")]
    public void FromOneshotDispatchException_WithTransportInterruption_ReturnsTransportInterrupted (
        string _,
        Func<Exception> createException)
    {
        var failure = UnityIpcFailureClassifier.FromOneshotDispatchException(
            createException(),
            TimeSpan.FromSeconds(1));

        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, failure.FailureKind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, failure.Code);
    }
}
