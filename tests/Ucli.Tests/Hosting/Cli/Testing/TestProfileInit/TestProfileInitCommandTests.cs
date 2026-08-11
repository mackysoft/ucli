using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Testing;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class TestProfileInitCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Init_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new RecordingTestProfileInitService((_, _) => ValueTask.FromResult(TestProfileInitExecutionResult.Success(
            new TestProfileInitExecutionOutput("/repo/test.profile.json"))));
        var command = new TestProfileInitCommand(service, CommandResultTestWriter.Create());
        var outputPath = AbsolutePath.Parse(Path.GetFullPath("test.profile.json"));
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.InitAsync(
            outputPath: outputPath,
            force: true,
            cancellationToken: cancellationTokenSource.Token));

        TestProfileInitCommandAssert.SucceededWithOutputPathAndForceInput(
            result,
            service,
            cancellationTokenSource.Token,
            expectedOutputPath: outputPath,
            expectedForce: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Init_WhenServiceReturnsFailure_UsesCommandResultFactoryEnvelope ()
    {
        var service = new RecordingTestProfileInitService((_, _) => ValueTask.FromResult(
            TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument("profile path already exists."))));
        var command = new TestProfileInitCommand(service, CommandResultTestWriter.Create());
        var outputPath = AbsolutePath.Parse(Path.GetFullPath("test.profile.json"));

        var result = await CommandResultCapture.ExecuteAsync(() => command.InitAsync(
            outputPath: outputPath,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.TestProfileInit);
    }

}
