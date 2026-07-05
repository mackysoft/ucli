using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal static class UnityRequestExecutorInvocationAssert
{
    public static ExecuteJsonInvocation ExecuteJsonOnce (
        IReadOnlyList<RecordingUnityRequestExecutor.Invocation> invocations,
        UcliCommand expectedInvocationCommand,
        UcliCommand expectedExecuteCommand)
    {
        var invocation = Assert.Single(invocations);
        Assert.Equal(expectedInvocationCommand, invocation.Command);
        var request = AssertExecuteJson(invocation, expectedExecuteCommand);
        return new ExecuteJsonInvocation(invocation, request);
    }

    public static ExecuteJsonPair ExecuteJsonPlanThenCall (IReadOnlyList<RecordingUnityRequestExecutor.Invocation> invocations)
    {
        Assert.Collection(
            invocations,
            static invocation => AssertExecuteJson(invocation, UcliCommandIds.Plan),
            static invocation => AssertExecuteJson(invocation, UcliCommandIds.Call));

        var planInvocation = invocations[0];
        var callInvocation = invocations[1];
        return new ExecuteJsonPair(
            planInvocation,
            Assert.IsType<UnityRequestPayload.ExecuteJson>(planInvocation.Payload),
            callInvocation,
            Assert.IsType<UnityRequestPayload.ExecuteJson>(callInvocation.Payload));
    }

    public static ExecuteOperationPair ExecuteOperationPlanThenCall (IReadOnlyList<RecordingUnityRequestExecutor.Invocation> invocations)
    {
        Assert.Collection(
            invocations,
            static invocation => AssertExecuteOperation(invocation, UcliCommandIds.Plan),
            static invocation => AssertExecuteOperation(invocation, UcliCommandIds.Call));

        var planInvocation = invocations[0];
        var callInvocation = invocations[1];
        return new ExecuteOperationPair(
            planInvocation,
            Assert.IsType<UnityRequestPayload.ExecuteOperation>(planInvocation.Payload),
            callInvocation,
            Assert.IsType<UnityRequestPayload.ExecuteOperation>(callInvocation.Payload));
    }

    public static ExecuteOperationInvocation ExecuteOperationOnce (
        IReadOnlyList<RecordingUnityRequestExecutor.Invocation> invocations,
        UcliCommand expectedInvocationCommand,
        UcliCommand expectedExecuteCommand)
    {
        var invocation = Assert.Single(invocations);
        Assert.Equal(expectedInvocationCommand, invocation.Command);
        var request = AssertExecuteOperation(invocation, expectedExecuteCommand);
        return new ExecuteOperationInvocation(invocation, request);
    }

    public static void Commands (
        IReadOnlyList<RecordingUnityRequestExecutor.Invocation> invocations,
        params UcliCommand[] expectedCommands)
    {
        Assert.Collection(
            invocations,
            expectedCommands
                .Select<UcliCommand, Action<RecordingUnityRequestExecutor.Invocation>>(expectedCommand => invocation => Assert.Equal(expectedCommand, invocation.Command))
                .ToArray());
    }

    public static void Timeouts (
        IReadOnlyList<RecordingUnityRequestExecutor.Invocation> invocations,
        params TimeSpan[] expectedTimeouts)
    {
        Assert.Collection(
            invocations,
            expectedTimeouts
                .Select<TimeSpan, Action<RecordingUnityRequestExecutor.Invocation>>(expectedTimeout => invocation => Assert.Equal(expectedTimeout, invocation.Timeout))
                .ToArray());
    }

    public static RecordingUnityRequestExecutor.Invocation ExecutedOnce (
        RecordingUnityRequestExecutor executor,
        UcliCommand expectedCommand)
    {
        var invocation = Assert.Single(executor.Invocations);
        Assert.Equal(expectedCommand, invocation.Command);
        return invocation;
    }

    public static UnityRequestPayload.Compile CompileOnce (
        RecordingUnityRequestExecutor executor,
        string? expectedRunId = null,
        TimeSpan? expectedTimeout = null)
    {
        var invocation = ExecutedOnce(executor, UcliCommandIds.Compile);
        if (expectedTimeout.HasValue)
        {
            Assert.Equal(expectedTimeout.Value, invocation.Timeout);
        }

        var payload = Assert.IsType<UnityRequestPayload.Compile>(invocation.Payload);
        if (expectedRunId is not null)
        {
            Assert.Equal(expectedRunId, payload.RunId);
        }

        return payload;
    }

    public static UnityRequestPayload.Ping ReadyPingOnce (
        RecordingUnityRequestExecutor executor,
        bool expectedFailFast)
    {
        var invocation = ExecutedOnce(executor, UcliCommandIds.Ready);
        var payload = Assert.IsType<UnityRequestPayload.Ping>(invocation.Payload);
        Assert.Equal(IpcPingClientVersions.Ready, payload.ClientVersion);
        Assert.Equal(expectedFailFast, payload.FailFast);
        return payload;
    }

    public static UnityRequestPayload.PlayEnter PlayEnterOnce (
        RecordingUnityRequestExecutor executor,
        TimeSpan expectedTimeout,
        int expectedPayloadTimeoutMilliseconds)
    {
        var invocation = ExecutedOnce(executor, UcliCommandIds.PlayEnter);
        Assert.Equal(UnityExecutionMode.Daemon, invocation.Mode);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        var payload = Assert.IsType<UnityRequestPayload.PlayEnter>(invocation.Payload);
        Assert.Equal(expectedPayloadTimeoutMilliseconds, payload.TimeoutMilliseconds);
        return payload;
    }

    public static UnityRequestPayload.PlayExit PlayExitOnce (
        RecordingUnityRequestExecutor executor,
        TimeSpan expectedTimeout,
        int expectedPayloadTimeoutMilliseconds)
    {
        var invocation = ExecutedOnce(executor, UcliCommandIds.PlayExit);
        Assert.Equal(UnityExecutionMode.Daemon, invocation.Mode);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        var payload = Assert.IsType<UnityRequestPayload.PlayExit>(invocation.Payload);
        Assert.Equal(expectedPayloadTimeoutMilliseconds, payload.TimeoutMilliseconds);
        return payload;
    }

    public static UnityRequestPayload.PlayStatus PlayStatusOnce (
        RecordingUnityRequestExecutor executor,
        TimeSpan? expectedTimeout = null)
    {
        var invocation = ExecutedOnce(executor, UcliCommandIds.PlayStatus);
        Assert.Equal(UnityExecutionMode.Daemon, invocation.Mode);
        if (expectedTimeout.HasValue)
        {
            Assert.Equal(expectedTimeout.Value, invocation.Timeout);
        }

        return Assert.IsType<UnityRequestPayload.PlayStatus>(invocation.Payload);
    }

    private static UnityRequestPayload.ExecuteJson AssertExecuteJson (
        RecordingUnityRequestExecutor.Invocation invocation,
        UcliCommand expectedExecuteCommand)
    {
        var request = Assert.IsType<UnityRequestPayload.ExecuteJson>(invocation.Payload);
        Assert.Equal(expectedExecuteCommand, request.Command);
        return request;
    }

    private static UnityRequestPayload.ExecuteOperation AssertExecuteOperation (
        RecordingUnityRequestExecutor.Invocation invocation,
        UcliCommand expectedExecuteCommand)
    {
        var request = Assert.IsType<UnityRequestPayload.ExecuteOperation>(invocation.Payload);
        Assert.Equal(expectedExecuteCommand, request.Command);
        return request;
    }

    internal readonly record struct ExecuteJsonPair (
        RecordingUnityRequestExecutor.Invocation PlanInvocation,
        UnityRequestPayload.ExecuteJson PlanRequest,
        RecordingUnityRequestExecutor.Invocation CallInvocation,
        UnityRequestPayload.ExecuteJson CallRequest);

    internal readonly record struct ExecuteJsonInvocation (
        RecordingUnityRequestExecutor.Invocation Invocation,
        UnityRequestPayload.ExecuteJson Request);

    internal readonly record struct ExecuteOperationPair (
        RecordingUnityRequestExecutor.Invocation PlanInvocation,
        UnityRequestPayload.ExecuteOperation PlanRequest,
        RecordingUnityRequestExecutor.Invocation CallInvocation,
        UnityRequestPayload.ExecuteOperation CallRequest);

    internal readonly record struct ExecuteOperationInvocation (
        RecordingUnityRequestExecutor.Invocation Invocation,
        UnityRequestPayload.ExecuteOperation Request);
}
