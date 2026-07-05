using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayEnterCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Enter_WithOptions_DispatchesProjectTimeoutAndCancellation ()
    {
        var service = new RecordingPlayEnterService((_, _) => ValueTask.FromResult(PlayEnterExecutionResult.Success(
            PlayEnterCommandTestData.CreateOutput())));
        var command = new PlayEnterCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.EnterAsync(
            projectPath: PlayCommandOutputTestData.ProjectPath,
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        PlayCommandAssert.EnterSucceededWithDispatchedInput(
            result,
            service,
            cancellationTokenSource.Token,
            PlayCommandOutputTestData.ProjectPath,
            expectedTimeoutMilliseconds: 1234);
    }
}
