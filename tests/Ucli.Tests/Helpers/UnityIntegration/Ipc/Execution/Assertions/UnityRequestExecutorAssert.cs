using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class UnityRequestExecutorAssert
{
    public static RawPayloadInvocation<TPayload> RawPayloadExecutedOnce<TPayload> (
        RecordingUnityRequestExecutor executor,
        UcliCommand expectedCommand,
        UnityExecutionMode expectedMode,
        string expectedMethod)
    {
        var invocation = Assert.Single(executor.Invocations);
        Assert.Equal(expectedCommand, invocation.Command);
        Assert.Equal(expectedMode, invocation.Mode);
        var request = Assert.IsType<UnityRequestPayload.Raw>(invocation.Payload);
        Assert.Equal(expectedMethod, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out TPayload payload, out _));
        return new RawPayloadInvocation<TPayload>(invocation, request, payload);
    }

    internal readonly record struct RawPayloadInvocation<TPayload> (
        RecordingUnityRequestExecutor.Invocation Invocation,
        UnityRequestPayload.Raw Request,
        TPayload Payload);
}
