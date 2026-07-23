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

    public static IpcRequestEnvelope DaemonRequestDispatchedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher,
        UnityIpcMethod expectedMethod)
    {
        var request = Assert.Single(daemonTransportClient.Requests);
        Assert.Equal(TextVocabulary.GetText(expectedMethod), request.Method);
        OneshotExecutionWasNotStarted(oneshotTransportClient, launcher);
        return request;
    }

    public static IpcRequestEnvelope DaemonFailFastReadinessOpsReadDispatchedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher)
    {
        var request = DaemonRequestDispatchedOnly(
            daemonTransportClient,
            oneshotTransportClient,
            launcher,
            UnityIpcMethod.OpsRead);
        AssertFailFastReadinessOpsReadRequest(request);
        return request;
    }

    public static IpcRequestEnvelope DaemonRequestDispatchedOnlyWithoutPluginVerification (
        RecordingUnityUcliPluginLocator pluginLocator,
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher,
        UnityIpcMethod expectedMethod)
    {
        Assert.Empty(pluginLocator.Invocations);
        return DaemonRequestDispatchedOnly(
            daemonTransportClient,
            oneshotTransportClient,
            launcher,
            expectedMethod);
    }

    public static IpcRequestEnvelope DaemonStreamingRequestDispatchedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher,
        UnityIpcMethod expectedMethod)
    {
        var request = Assert.Single(daemonTransportClient.StreamingRequests);
        Assert.Same(request, Assert.Single(daemonTransportClient.Requests));
        Assert.Equal(TextVocabulary.GetText(expectedMethod), request.Method);
        OneshotExecutionWasNotStarted(oneshotTransportClient, launcher);
        return request;
    }

    public static IReadOnlyList<IpcRequestEnvelope> DaemonRequestsDispatchedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher,
        params UnityIpcMethod[] expectedMethods)
    {
        var requests = IpcRequestAssert.Methods(daemonTransportClient, expectedMethods);
        OneshotExecutionWasNotStarted(oneshotTransportClient, launcher);
        return requests;
    }

    public static IReadOnlyList<IpcRequestEnvelope> DaemonFailFastReadinessOpsReadRedispatchedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher)
    {
        var requests = DaemonRequestsDispatchedOnly(
            daemonTransportClient,
            oneshotTransportClient,
            launcher,
            UnityIpcMethod.OpsRead,
            UnityIpcMethod.OpsRead);
        Assert.All(requests, AssertFailFastReadinessOpsReadRequest);
        return requests;
    }

    public static IReadOnlyList<IpcRequestEnvelope> OneshotExecutionStartedOnly (
        RecordingUnityIpcTransportClient daemonTransportClient,
        RecordingUnityIpcTransportClient oneshotTransportClient,
        RecordingUnityBatchmodeProcessLauncher launcher,
        params UnityIpcMethod[] expectedMethods)
    {
        Assert.Empty(daemonTransportClient.Requests);
        var requests = IpcRequestAssert.Methods(oneshotTransportClient, expectedMethods);
        Assert.Single(launcher.Invocations);
        return requests;
    }

    private static void AssertFailFastReadinessOpsReadRequest (IpcRequestEnvelope request)
    {
        Assert.True(IpcPayloadCodec.TryDeserialize(
            request.Payload,
            out IpcOpsReadRequest payload,
            out _));
        Assert.True(payload.RequireReadinessGate);
        Assert.True(payload.FailFast);
    }
}
