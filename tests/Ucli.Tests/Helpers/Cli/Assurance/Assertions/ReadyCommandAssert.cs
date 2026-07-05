using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Tests;

internal static class ReadyCommandAssert
{
    public static void ExecutionTargetDispatchedWithOptions (
        CommandExecutionResult result,
        RecordingReadyService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds,
        bool expectedFailFast)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(
            new ReadyCommandInput(
                expectedProjectPath,
                ReadyTarget.Execution,
                expectedMode,
                expectedTimeoutMilliseconds,
                null,
                false,
                expectedFailFast),
            invocation.Input);
    }

    public static void ReadIndexTargetDispatchedWithReadIndexMode (
        CommandExecutionResult result,
        RecordingReadyService service,
        ReadIndexMode expectedReadIndexMode)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(
            ReadyTarget.ReadIndex,
            invocation.Input.Target);
        Assert.Null(invocation.Input.Mode);
        Assert.Equal(
            expectedReadIndexMode,
            invocation.Input.ReadIndexMode);
        Assert.True(invocation.Input.IsReadIndexModeSpecified);
    }

    public static void DefaultExecutionTargetDispatched (
        CommandExecutionResult result,
        RecordingReadyService service)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(
            ReadyTarget.Execution,
            invocation.Input.Target);
    }

    public static void InvalidTargetRejectedBeforeReadyExecution (
        CommandExecutionResult result,
        RecordingReadyService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            UcliCommandNames.Ready);
    }
}
