using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayStatusCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WithOptions_DispatchesProjectTimeoutAndCancellation ()
    {
        var service = new RecordingPlayStatusService((_, _) => ValueTask.FromResult(PlayStatusExecutionResult.Success(
            PlayStatusCommandTestData.CreateOutput())));
        var command = new PlayStatusCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.StatusAsync(
            projectPath: PlayCommandOutputTestData.ProjectPath,
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        PlayCommandAssert.StatusSucceededWithDispatchedInput(
            result,
            service,
            cancellationTokenSource.Token,
            PlayCommandOutputTestData.ProjectPath,
            expectedTimeoutMilliseconds: 1234);
    }
}
