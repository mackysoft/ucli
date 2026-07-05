using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Tests;

internal static class BuildRunCommandAssert
{
    public static void SucceededWithDispatchedInput (
        CommandExecutionResult result,
        RecordingBuildService service,
        CancellationToken expectedCancellationToken,
        string expectedProfilePath,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(
            new BuildCommandInput(
                expectedProfilePath,
                expectedProjectPath,
                expectedMode,
                expectedTimeoutMilliseconds),
            invocation.Input);
        Assert.NotNull(invocation.ProgressSink);
    }

    public static void InvalidArgumentReturnedWithoutBuildExecution (
        CommandExecutionResult result,
        RecordingBuildService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailureWithEmptyStandardError(
            result,
            service.Invocations,
            UcliCommandNames.BuildRun);
    }
}
