using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new RecordingBuildService((_, _, _) => ValueTask.FromResult(BuildExecutionResult.Success(BuildRunTestData.CreateOutput())));
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.RunAsync(
            profilePath: "/repo/.ucli/build/player.json",
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "120000",
            format: "json",
            cancellationToken: cancellationTokenSource.Token));

        BuildRunCommandAssert.SucceededWithDispatchedInput(
            result,
            service,
            cancellationTokenSource.Token,
            "/repo/.ucli/build/player.json",
            "/repo/UnityProject",
            UnityExecutionMode.Daemon,
            expectedTimeoutMilliseconds: 120000);
    }
}
