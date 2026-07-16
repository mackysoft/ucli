using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Tests;

internal static class PlanCommandAssert
{
    public static void SucceededWithDispatchedRequest (
        CommandExecutionResult result,
        RecordingPlanService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds,
        ReadIndexMode expectedReadIndexMode,
        bool expectedFailFast,
        string expectedRequestJson,
        bool expectedAllowPlayMode)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.NotEqual(Guid.Empty, Assert.Single(service.RequestIds));
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(expectedProjectPath, invocation.Input.ProjectPath);
        Assert.Equal(expectedMode, invocation.Input.Mode);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.Input.TimeoutMilliseconds);
        Assert.Equal(expectedReadIndexMode, invocation.Input.ReadIndexMode);
        Assert.Equal(expectedFailFast, invocation.Input.FailFast);
        Assert.Equal(expectedRequestJson, invocation.Input.RequestJson);
        Assert.Equal(expectedAllowPlayMode, invocation.Input.AllowPlayMode);
    }

    public static void SucceededWithAllowPlayModeInput (
        CommandExecutionResult result,
        RecordingPlanService service)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.NotEqual(Guid.Empty, Assert.Single(service.RequestIds));
        var invocation = Assert.Single(service.Invocations);
        Assert.True(invocation.Input.AllowPlayMode);
        Assert.Null(invocation.Input.ReadIndexMode);
    }

    public static void InvalidArgumentReturnedWithoutPlanExecution (
        CommandExecutionResult result,
        RecordingPlanService service,
        string? expectedRequestId = null)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            UcliCommandNames.Plan);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        if (expectedRequestId is not null)
        {
            JsonAssert.For(outputJson.RootElement)
                .HasProperty("payload", payload => payload
                    .HasString("requestId", expectedRequestId));
        }
    }
}
