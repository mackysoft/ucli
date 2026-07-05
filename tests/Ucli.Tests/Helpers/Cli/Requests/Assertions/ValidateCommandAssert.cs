using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Tests;

internal static class ValidateCommandAssert
{
    public static void SucceededWithDispatchedRequest (
        CommandExecutionResult result,
        RecordingValidateService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        int expectedTimeoutMilliseconds,
        ReadIndexMode expectedReadIndexMode,
        string expectedRequestJson)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(expectedProjectPath, invocation.Input.ProjectPath);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.Input.TimeoutMilliseconds);
        Assert.Equal(expectedReadIndexMode, invocation.Input.ReadIndexMode);
        Assert.Equal(expectedRequestJson, invocation.Input.RequestJson);
    }

    public static void InvalidArgumentRejectedBeforeValidation (
        CommandExecutionResult result,
        RecordingValidateService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            UcliCommandNames.Validate);
    }
}
