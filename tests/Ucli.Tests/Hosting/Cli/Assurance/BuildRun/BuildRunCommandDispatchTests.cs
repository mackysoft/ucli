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
        var service = new RecordingBuildService((_, _, _) => ValueTask.FromResult<BuildExecutionResult>(BuildExecutionResult.Completed(BuildRunTestData.CreatePassedOutput())));
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);
        var profilePath = FilePathReference.Parse(".ucli/build/player.json");
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.RunAsync(
            profilePath: profilePath,
            projectPath: AbsolutePath.Parse(ProjectPathTestValues.RepositoryUnityProject),
            mode: "daemon",
            timeout: "120000",
            format: "json",
            cancellationToken: cancellationTokenSource.Token));

        BuildRunCommandAssert.SucceededWithDispatchedInput(
            result,
            service,
            cancellationTokenSource.Token,
            profilePath,
            ProjectPathTestValues.RepositoryUnityProject,
            UnityExecutionMode.Daemon,
            expectedTimeoutMilliseconds: 120000);
    }
}
