using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.PlanCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class PlanCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Plan_WhenServiceFailsWithoutPlanToken_OmitsPlanTokenFromPayload ()
    {
        var service = new RecordingPlanService((_, _) => ValueTask.FromResult(CreateStaticValidationFailureResult()));
        var preflightService = new UnexpectedPlanCommandPreflightService();
        var command = new PlanCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.PlanAsync(
            projectPath: "/repo/UnityProject",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Plan);
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("planToken", out _));
    }
}
