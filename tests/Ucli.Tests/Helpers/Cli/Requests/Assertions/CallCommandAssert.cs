using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Tests;

internal static class CallCommandAssert
{
    public static void SucceededWithDispatchedRequest (
        CommandExecutionResult result,
        RecordingCallService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds,
        string expectedPlanToken,
        string expectedRequestJson)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CallServiceDispatchAssert.DispatchedWithOptions(
            service,
            expectedCancellationToken,
            expectedProjectPath,
            expectedMode,
            expectedTimeoutMilliseconds,
            expectedPlanToken,
            expectedWithPlan: true,
            expectedAllowDangerous: true,
            expectedAllowPlayMode: true,
            expectedFailFast: true,
            expectedRequestJson,
            UcliCommandIds.Call);
    }

    public static void InvalidModePreparedPayloadWithoutCallExecution (
        CommandExecutionResult result,
        RecordingCallService service,
        RecordingCallCommandPreflightService preflightService,
        string expectedRequestJson,
        string expectedRequestId)
    {
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(service.Invocations);
        CallCommandPreflightAssert.PreparedOnce(
            preflightService,
            expectedProjectPath: null,
            expectedRequestJson);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("requestId", expectedRequestId));
    }
}
