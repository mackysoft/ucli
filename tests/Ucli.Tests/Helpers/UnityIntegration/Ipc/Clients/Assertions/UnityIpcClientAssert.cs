using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class UnityIpcClientAssert
{
    public static void FailFastOpsReadDispatchedOnce (
        RecordingUnityIpcClient client,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        FailFastOpsReadDispatch(Assert.Single(client.Invocations), expectedUnityProject);
    }

    public static void FailFastOpsReadRedispatched (
        RecordingUnityIpcClient client,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        Assert.Collection(
            client.Invocations,
            invocation => FailFastOpsReadDispatch(invocation, expectedUnityProject),
            invocation => FailFastOpsReadDispatch(invocation, expectedUnityProject));
    }

    private static void FailFastOpsReadDispatch (
        RecordingUnityIpcClient.Invocation invocation,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            invocation.DispatchRequest.Payload,
            out IpcOpsReadRequest dispatchedPayload,
            out _));
        Assert.True(dispatchedPayload.FailFast);
        Assert.True(dispatchedPayload.RequireReadinessGate);
    }
}
