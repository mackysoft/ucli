using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCommandGoldenOutputTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Run_WithPassOutput_MatchesGolden ()
    {
        var service = new RecordingBuildService((_, _, _) => ValueTask.FromResult(BuildExecutionResult.Success(BuildRunTestData.CreateOutput())));
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            profilePath: Path.Combine(ProjectPathTestValues.RepositoryRoot, ".ucli", "build", "player.json"),
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(string.Empty, result.StdErr);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("build", "pass-success.json"),
            result.StdOut,
            new JsonGoldenFileNormalization().NormalizePathPrefix(ProjectPathTestValues.WorkspaceRoot, "/workspace"));
    }
}
