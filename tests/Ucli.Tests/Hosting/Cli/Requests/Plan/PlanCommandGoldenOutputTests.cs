using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.PlanCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class PlanCommandGoldenOutputTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WithSuccessOutput_MatchesGolden ()
    {
        var service = new RecordingPlanService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var preflightService = new UnexpectedPlanCommandPreflightService();
        var command = new PlanCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.PlanAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            readIndexMode: "disabled",
            failFast: true,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("requestId", RequestId));
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("plan", "success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Plan_WhenContractViolationExists_MatchesGolden ()
    {
        var service = new RecordingPlanService((_, _) => ValueTask.FromResult(CreateContractViolationFailureResult()));
        var preflightService = new UnexpectedPlanCommandPreflightService();
        var command = new PlanCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.PlanAsync(
            projectPath: "/repo/UnityProject",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("plan", "contract-violation.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }
}
