using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Tests;

internal static class VerifyCommandAssert
{
    public static void SucceededWithDispatchedInput (
        CommandExecutionResult result,
        RecordingVerifyService service,
        CancellationToken expectedCancellationToken,
        string? expectedProfile,
        string expectedProfilePath,
        string expectedFromPath,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);

        var input = Assert.IsType<VerifyCommandInput>(invocation.Input);
        Assert.Equal(expectedProfile, input.Profile);
        Assert.Equal(expectedProfilePath, input.ProfilePath);
        Assert.Equal(expectedFromPath, input.FromPath);
        Assert.Equal(expectedProjectPath, input.ProjectPath);
        Assert.Equal(expectedMode, input.Mode);
        Assert.Equal(expectedTimeoutMilliseconds, input.TimeoutMilliseconds);
    }

    public static void InvalidArgumentReturnedWithoutVerifyExecution (
        CommandExecutionResult result,
        RecordingVerifyService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailureWithEmptyStandardError(
            result,
            service.Invocations,
            UcliCommandNames.Verify);
    }
}
