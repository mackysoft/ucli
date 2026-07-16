using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.CallCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class CallCommandGoldenOutputTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WithSuccessOutput_MatchesGolden ()
    {
        var service = new RecordingCallService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var preflightService = new RecordingCallCommandPreflightService((_, _, _) => throw new InvalidOperationException("Preflight should not be called."));
        var command = new CallCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.CallAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            planToken: "user-token",
            withPlan: true,
            allowDangerous: true,
            allowPlayMode: true,
            failFast: true,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("requestId", RequestId)
                .HasProperty("plan", plan => plan
                    .HasString("requestId", RequestId)));
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("call", "success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Call_WhenContractViolationExists_MatchesGolden ()
    {
        var service = new RecordingCallService((_, _) => ValueTask.FromResult(CreateContractViolationFailureResult()));
        var preflightService = new RecordingCallCommandPreflightService((_, _, _) => throw new InvalidOperationException("Preflight should not be called."));
        var command = new CallCommand(service, preflightService, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.CallAsync(
            projectPath: "/repo/UnityProject",
            withPlan: true,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("call", "contract-violation.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }
}
