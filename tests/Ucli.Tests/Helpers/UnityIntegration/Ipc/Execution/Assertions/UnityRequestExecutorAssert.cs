using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class UnityRequestExecutorAssert
{
    public static PayloadInvocation<TPayload> PayloadExecutedOnce<TPayload> (
        RecordingUnityRequestExecutor executor,
        UcliCommand expectedCommand,
        UnityExecutionMode expectedMode)
        where TPayload : UnityRequestPayload
    {
        var invocation = Assert.Single(executor.Invocations);
        Assert.Equal(expectedCommand, invocation.Command);
        Assert.Equal(expectedMode, invocation.Mode);
        var payload = Assert.IsType<TPayload>(invocation.Payload);
        return new PayloadInvocation<TPayload>(invocation, payload);
    }

    internal readonly record struct PayloadInvocation<TPayload> (
        RecordingUnityRequestExecutor.Invocation Invocation,
        TPayload Payload)
        where TPayload : UnityRequestPayload;
}
