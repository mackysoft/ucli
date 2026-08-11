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
        var invocationFactory = new RecordingLifecycleExecutionStartInvocationFactory();
        var command = new PlayEnterCommand(service, CommandResultTestWriter.Create(), invocationFactory);
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.EnterAsync(
            projectPath: AbsolutePath.Parse(PlayCommandOutputTestData.ProjectPath),
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        PlayCommandAssert.EnterSucceededWithDispatchedInput(
            result,
            invocationFactory,
            cancellationTokenSource.Token,
            PlayCommandOutputTestData.ProjectPath,
            expectedTimeoutMilliseconds: 1234);
        Assert.Equal(1, invocationFactory.DisposeCount);
    }
}
