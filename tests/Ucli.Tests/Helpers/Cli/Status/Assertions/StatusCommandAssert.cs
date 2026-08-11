namespace MackySoft.Tests;

internal static class StatusCommandAssert
{
    public static void SucceededWithDispatchedInput (
        CommandExecutionResult result,
        RecordingStatusService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        int expectedTimeoutMilliseconds)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        ProjectPathDispatchAssert.EqualNormalized(expectedProjectPath, invocation.Input.ProjectPath);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.Input.TimeoutMilliseconds);
    }

    public static void InvalidTimeoutRejectedBeforeStatusExecution (
        CommandExecutionResult result,
        RecordingStatusService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            UcliCommandNames.Status);
    }
}
