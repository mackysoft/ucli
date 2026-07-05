using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal static class OperationExecuteInvocationAssert
{
    public static RecordingOperationAuthorizationService.Invocation AuthorizationCheckedOnce (
        RecordingOperationAuthorizationService authorizationService,
        string expectedOperationName,
        OperationPolicy expectedPolicy)
    {
        var invocation = Assert.Single(authorizationService.Invocations);
        Assert.Equal(expectedOperationName, invocation.Operation.Name);
        Assert.Equal(expectedPolicy, invocation.Operation.Policy);
        return invocation;
    }

    public static UnityRequestExecutorInvocationAssert.ExecuteOperationInvocation CallDispatched (
        RecordingUnityRequestExecutor requestExecutor,
        UcliCommand expectedOwnerCommand,
        UnityExecutionMode expectedMode,
        TimeSpan expectedTimeout,
        string expectedRepositoryRoot,
        string expectedRequestId,
        bool expectedFailFast,
        string expectedOperationId,
        string expectedOperationName)
    {
        var execution = UnityRequestExecutorInvocationAssert.ExecuteOperationOnce(
            requestExecutor.Invocations,
            expectedOwnerCommand,
            UcliCommandIds.Call);

        Assert.Equal(expectedMode, execution.Invocation.Mode);
        Assert.Equal(expectedTimeout, execution.Invocation.Timeout);
        Assert.Equal(expectedRepositoryRoot, execution.Invocation.UnityProject.RepositoryRoot);
        Assert.Equal(expectedRequestId, execution.Request.RequestId);
        Assert.Equal(expectedFailFast, execution.Request.FailFast);
        Assert.Equal(expectedOperationId, execution.Request.OperationId);
        Assert.Equal(expectedOperationName, execution.Request.OperationName);
        return execution;
    }

    public static UnityRequestExecutorInvocationAssert.ExecuteOperationPair PlanThenCallDispatched (
        RecordingUnityRequestExecutor requestExecutor,
        UcliCommand expectedOwnerCommand,
        string expectedRequestId,
        string expectedPlanToken,
        bool expectedFailFast)
    {
        var executePair = UnityRequestExecutorInvocationAssert.ExecuteOperationPlanThenCall(requestExecutor.Invocations);

        Assert.Equal(expectedOwnerCommand, executePair.PlanInvocation.Command);
        Assert.Equal(expectedOwnerCommand, executePair.CallInvocation.Command);
        Assert.Null(executePair.PlanRequest.PlanToken);
        Assert.Equal(expectedPlanToken, executePair.CallRequest.PlanToken);
        Assert.Equal(expectedFailFast, executePair.PlanRequest.FailFast);
        Assert.Equal(expectedFailFast, executePair.CallRequest.FailFast);
        Assert.Equal(expectedRequestId, executePair.PlanRequest.RequestId);
        Assert.Equal(expectedRequestId, executePair.CallRequest.RequestId);
        return executePair;
    }

    public static UnityRequestExecutorInvocationAssert.ExecuteOperationPair PlanThenCallDispatchedWithTimeouts (
        RecordingUnityRequestExecutor requestExecutor,
        TimeSpan expectedPlanTimeout,
        TimeSpan expectedCallTimeout)
    {
        var executePair = UnityRequestExecutorInvocationAssert.ExecuteOperationPlanThenCall(requestExecutor.Invocations);
        Assert.Equal(expectedPlanTimeout, executePair.PlanInvocation.Timeout);
        Assert.Equal(expectedCallTimeout, executePair.CallInvocation.Timeout);
        return executePair;
    }

    public static UnityRequestExecutorInvocationAssert.ExecuteOperationInvocation PlanOnlyDispatched (
        RecordingUnityRequestExecutor requestExecutor,
        UcliCommand expectedOwnerCommand)
    {
        return UnityRequestExecutorInvocationAssert.ExecuteOperationOnce(
            requestExecutor.Invocations,
            expectedOwnerCommand,
            UcliCommandIds.Plan);
    }
}
