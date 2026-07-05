using MackySoft.Ucli.Application.Features.Init.UseCases.Init;

namespace MackySoft.Tests;

internal static class InitCommandAssert
{
    public static void SucceededWithForceInput (
        CommandExecutionResult result,
        RecordingInitService service,
        CancellationToken expectedCancellationToken,
        bool expectedForce)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(
            new InitCommandInput(expectedForce),
            invocation.Input);
    }
}
