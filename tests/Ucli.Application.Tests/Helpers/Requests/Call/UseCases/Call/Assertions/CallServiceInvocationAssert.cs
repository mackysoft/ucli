using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal static class CallServiceInvocationAssert
{
    public static UnityRequestExecutorInvocationAssert.ExecuteJsonInvocation SingleCallDispatched (
        RecordingUnityRequestExecutor requestExecutor)
    {
        return UnityRequestExecutorInvocationAssert.ExecuteJsonOnce(
            requestExecutor.Invocations,
            UcliCommandIds.Call,
            UcliCommandIds.Call);
    }

    public static UnityRequestExecutorInvocationAssert.ExecuteJsonInvocation SingleCallDispatched (
        RecordingUnityRequestExecutor requestExecutor,
        UnityExecutionMode expectedMode,
        TimeSpan expectedTimeout,
        string? expectedPlanToken,
        bool expectedFailFast,
        bool expectedAllowDangerous,
        bool expectedAllowPlayMode)
    {
        var execution = SingleCallDispatched(requestExecutor);

        Assert.Equal(expectedMode, execution.Invocation.Mode);
        Assert.Equal(expectedTimeout, execution.Invocation.Timeout);
        Assert.Equal(expectedPlanToken, execution.Request.PlanToken);
        Assert.Equal(expectedFailFast, execution.Request.FailFast);
        Assert.Equal(expectedAllowDangerous, execution.Request.AllowDangerous);
        Assert.Equal(expectedAllowPlayMode, execution.Request.AllowPlayMode);
        return execution;
    }

    public static UnityRequestExecutorInvocationAssert.ExecuteJsonPair PlanThenCallDispatched (
        RecordingUnityRequestExecutor requestExecutor,
        string? expectedCallPlanToken,
        bool expectedAllowDangerous,
        bool expectedAllowPlayMode)
    {
        var executePair = UnityRequestExecutorInvocationAssert.ExecuteJsonPlanThenCall(requestExecutor.Invocations);

        Assert.Null(executePair.PlanRequest.PlanToken);
        Assert.Equal(expectedCallPlanToken, executePair.CallRequest.PlanToken);
        Assert.Equal(expectedAllowDangerous, executePair.PlanRequest.AllowDangerous);
        Assert.Equal(expectedAllowDangerous, executePair.CallRequest.AllowDangerous);
        Assert.Equal(expectedAllowPlayMode, executePair.PlanRequest.AllowPlayMode);
        Assert.Equal(expectedAllowPlayMode, executePair.CallRequest.AllowPlayMode);
        return executePair;
    }

    public static UnityRequestExecutorInvocationAssert.ExecuteJsonPair PlanThenCallDispatchedByOwner (
        RecordingUnityRequestExecutor requestExecutor,
        UcliCommand expectedOwnerCommand,
        TimeSpan expectedPlanTimeout,
        TimeSpan expectedCallTimeout)
    {
        var executePair = UnityRequestExecutorInvocationAssert.ExecuteJsonPlanThenCall(requestExecutor.Invocations);

        Assert.Equal(expectedOwnerCommand, executePair.PlanInvocation.Command);
        Assert.Equal(expectedOwnerCommand, executePair.CallInvocation.Command);
        Assert.Equal(expectedPlanTimeout, executePair.PlanInvocation.Timeout);
        Assert.Equal(expectedCallTimeout, executePair.CallInvocation.Timeout);
        return executePair;
    }

    public static UnityRequestExecutorInvocationAssert.ExecuteJsonPair PlanThenCallDispatchedWithTimeouts (
        RecordingUnityRequestExecutor requestExecutor,
        TimeSpan expectedPlanTimeout,
        TimeSpan expectedCallTimeout)
    {
        var executePair = UnityRequestExecutorInvocationAssert.ExecuteJsonPlanThenCall(requestExecutor.Invocations);

        Assert.Equal(expectedPlanTimeout, executePair.PlanInvocation.Timeout);
        Assert.Equal(expectedCallTimeout, executePair.CallInvocation.Timeout);
        return executePair;
    }

    public static UnityRequestExecutorInvocationAssert.ExecuteJsonInvocation PlanOnlyDispatched (
        RecordingUnityRequestExecutor requestExecutor)
    {
        return UnityRequestExecutorInvocationAssert.ExecuteJsonOnce(
            requestExecutor.Invocations,
            UcliCommandIds.Call,
            UcliCommandIds.Plan);
    }
}
