using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Tests;

internal static class CallServiceDispatchAssert
{
    public static CallCommandInput DispatchedWithOptions (
        RecordingCallService service,
        CancellationToken expectedCancellationToken,
        string? expectedProjectPath,
        UnityExecutionMode? expectedMode,
        int? expectedTimeoutMilliseconds,
        string? expectedPlanToken,
        bool expectedWithPlan,
        bool expectedAllowDangerous,
        bool expectedAllowPlayMode,
        bool expectedFailFast,
        string? expectedRequestJson,
        UcliCommand expectedExecutionOwnerCommand)
    {
        Assert.NotEqual(Guid.Empty, Assert.Single(service.RequestIds));
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(expectedProjectPath, invocation.Input.ProjectPath);
        Assert.Equal(expectedMode, invocation.Input.Mode);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.Input.TimeoutMilliseconds);
        Assert.Equal(expectedPlanToken, invocation.Input.PlanToken);
        Assert.Equal(expectedWithPlan, invocation.Input.WithPlan);
        Assert.Equal(expectedAllowDangerous, invocation.Input.AllowDangerous);
        Assert.Equal(expectedAllowPlayMode, invocation.Input.AllowPlayMode);
        Assert.Equal(expectedFailFast, invocation.Input.FailFast);
        Assert.Equal(expectedExecutionOwnerCommand, invocation.Input.ExecutionOwnerCommand);
        if (expectedRequestJson is not null)
        {
            Assert.Equal(expectedRequestJson, invocation.Input.RequestJson);
        }

        return invocation.Input;
    }
}
