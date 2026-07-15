using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Create_WithBuildRunGoldenCase_MatchesGolden ()
    {
        foreach (var testCase in BuildRunCliOutputContractTestSupport.GoldenCases)
        {
            var result = BuildRunCliOutputContractTestSupport.CreateCommandResult(testCase.CaseName);

            var json = new CommandResultJsonContractWriter().Write(result);

            JsonGoldenFileAssert.Matches(
                CliOutputGoldenFiles.GetPath(BuildRunCliOutputContractTestSupport.GoldenDirectory, testCase.FileName),
                json);
        }
    }
}
