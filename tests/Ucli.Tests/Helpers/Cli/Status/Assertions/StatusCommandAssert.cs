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
        Assert.Equal(
            new StatusCommandInput(
                expectedProjectPath,
                expectedTimeoutMilliseconds),
            invocation.Input);
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
