using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayExitCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Exit_WithOptions_DispatchesProjectTimeoutAndCancellation ()
    {
        var service = new RecordingPlayExitService((_, _) => ValueTask.FromResult(PlayExitExecutionResult.Success(
            PlayExitCommandTestData.CreateOutput())));
        var invocationFactory = new RecordingLifecycleExecutionStartInvocationFactory();
        var command = new PlayExitCommand(service, CommandResultTestWriter.Create(), invocationFactory);
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.ExitAsync(
            projectPath: AbsolutePath.Parse(PlayCommandOutputTestData.ProjectPath),
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        PlayCommandAssert.ExitSucceededWithDispatchedInput(
            result,
            invocationFactory,
            cancellationTokenSource.Token,
            PlayCommandOutputTestData.ProjectPath,
            expectedTimeoutMilliseconds: 1234);
        Assert.Equal(1, invocationFactory.DisposeCount);
    }
}
