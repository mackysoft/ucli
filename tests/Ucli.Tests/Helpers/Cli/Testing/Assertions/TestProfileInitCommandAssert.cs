using MackySoft.Ucli.Application.Features.Testing.Profiles.UseCases.ProfileInit;

namespace MackySoft.Tests;

internal static class TestProfileInitCommandAssert
{
    public static void SucceededWithOutputPathAndForceInput (
        CommandExecutionResult result,
        RecordingTestProfileInitService service,
        CancellationToken expectedCancellationToken,
        string expectedOutputPath,
        bool expectedForce)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(
            new TestProfileInitCommandInput(
                expectedOutputPath,
                expectedForce),
            invocation.Input);
    }
}
