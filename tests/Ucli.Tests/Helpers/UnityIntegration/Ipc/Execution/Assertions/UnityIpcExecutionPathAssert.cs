using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class UnityIpcExecutionPathAssert
{
    public static void NoUnityExecutionWasStarted (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher)
    {
        Assert.Empty(daemonTransportClient.Requests);
        Assert.Empty(oneshotTransportClient.Requests);
        Assert.Empty(launcher.Invocations);
    }

    public static void OneshotExecutionWasNotStarted (
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher)
    {
        Assert.Empty(oneshotTransportClient.Requests);
        Assert.Empty(launcher.Invocations);
    }

    public static void ExplicitDaemonEndpointDispatchedWithoutModeDecision (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher,
        StubModeDecisionService modeDecisionService,
        string expectedEndpointAddress)
    {
        UnityIpcTransportClientAssert.EndpointDispatchAddressedOnce(
            daemonTransportClient,
            expectedEndpointAddress);
        OneshotExecutionWasNotStarted(oneshotTransportClient, launcher);
        Assert.Empty(modeDecisionService.Invocations);
    }

    public static void NoPluginVerificationOrUnityExecutionWasStarted (
        RecordingUnityUcliPluginLocator pluginLocator,
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher)
    {
        Assert.Empty(pluginLocator.Invocations);
        NoUnityExecutionWasStarted(
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }

    public static IpcRequest DaemonRequestDispatchedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher,
        string expectedMethod)
    {
        var request = Assert.Single(daemonTransportClient.Requests);
        Assert.Equal(expectedMethod, request.Method);
        OneshotExecutionWasNotStarted(oneshotTransportClient, launcher);
        return request;
    }

    public static IpcRequest DaemonFailFastReadinessOpsReadDispatchedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher)
    {
        var request = DaemonRequestDispatchedOnly(
            daemonTransportClient,
            oneshotTransportClient,
            launcher,
            IpcMethodNames.OpsRead);
        AssertFailFastReadinessOpsReadRequest(request);
        return request;
    }

    public static IpcRequest DaemonRequestDispatchedOnlyWithoutPluginVerification (
        RecordingUnityUcliPluginLocator pluginLocator,
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher,
        string expectedMethod)
    {
        Assert.Empty(pluginLocator.Invocations);
        return DaemonRequestDispatchedOnly(
            daemonTransportClient,
            oneshotTransportClient,
            launcher,
            expectedMethod);
    }

    public static IpcRequest DaemonStreamingRequestDispatchedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher,
        string expectedMethod)
    {
        var request = Assert.Single(daemonTransportClient.StreamingRequests);
        Assert.Same(request, Assert.Single(daemonTransportClient.Requests));
        Assert.Equal(expectedMethod, request.Method);
        OneshotExecutionWasNotStarted(oneshotTransportClient, launcher);
        return request;
    }

    public static IReadOnlyList<IpcRequest> DaemonRequestsDispatchedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher,
        params string[] expectedMethods)
    {
        var requests = IpcRequestAssert.Methods(daemonTransportClient, expectedMethods);
        OneshotExecutionWasNotStarted(oneshotTransportClient, launcher);
        return requests;
    }

    public static IReadOnlyList<IpcRequest> DaemonFailFastReadinessOpsReadRedispatchedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher)
    {
        var requests = DaemonRequestsDispatchedOnly(
            daemonTransportClient,
            oneshotTransportClient,
            launcher,
            IpcMethodNames.OpsRead,
            IpcMethodNames.OpsRead);
        Assert.All(requests, AssertFailFastReadinessOpsReadRequest);
        return requests;
    }

    public static IReadOnlyList<IpcRequest> OneshotExecutionStartedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher,
        params string[] expectedMethods)
    {
        Assert.Empty(daemonTransportClient.Requests);
        var requests = IpcRequestAssert.Methods(oneshotTransportClient, expectedMethods);
        Assert.Single(launcher.Invocations);
        return requests;
    }

    private static void AssertFailFastReadinessOpsReadRequest (IpcRequest request)
    {
        Assert.True(IpcPayloadCodec.TryDeserialize(
            request.Payload,
            out IpcOpsReadRequest payload,
            out _));
        Assert.True(payload.RequireReadinessGate);
        Assert.True(payload.FailFast);
    }
}
