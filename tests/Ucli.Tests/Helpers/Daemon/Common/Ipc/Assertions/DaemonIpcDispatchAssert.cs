using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class DaemonIpcDispatchAssert
{
    public static void NoDispatchWasSent (RecordingIpcTransportClient transportClient)
    {
        Assert.Empty(transportClient.Requests);
    }

    public static IpcRequest SingleDispatchSentToEndpoint (
        RecordingIpcTransportClient transportClient,
        string expectedEndpointAddress,
        UnityIpcMethod expectedMethod,
        string expectedSessionToken)
    {
        var endpoint = Assert.Single(transportClient.Endpoints);
        Assert.Equal(expectedEndpointAddress, endpoint.Address);
        return SingleDispatchSent(transportClient, expectedMethod, expectedSessionToken);
    }

    public static IpcRequest SingleDispatchSentToEndpointWithTimeout (
        RecordingIpcTransportClient transportClient,
        string expectedEndpointAddress,
        UnityIpcMethod expectedMethod,
        string expectedSessionToken,
        TimeSpan expectedTimeout)
    {
        var request = SingleDispatchSentToEndpoint(
            transportClient,
            expectedEndpointAddress,
            expectedMethod,
            expectedSessionToken);
        Assert.Equal(expectedTimeout, Assert.Single(transportClient.Timeouts));
        return request;
    }

    public static IpcRequest SingleDispatchSent (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod,
        string expectedSessionToken)
    {
        var request = SingleDispatchAttempted(transportClient, expectedMethod);
        Assert.Equal(expectedSessionToken, request.SessionToken);
        return request;
    }

    public static IpcRequest SingleDispatchPreservedCallerTimeoutBudget (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod,
        string expectedSessionToken,
        TimeSpan minimumTimeout)
    {
        var request = SingleDispatchSent(transportClient, expectedMethod, expectedSessionToken);
        var timeout = Assert.Single(transportClient.Timeouts);
        Assert.True(
            timeout > minimumTimeout,
            $"Expected dispatch timeout to be greater than {minimumTimeout}, but was {timeout}.");
        return request;
    }

    public static IpcRequest SingleStreamingDispatchSent (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod,
        string expectedSessionToken)
    {
        var request = Assert.Single(transportClient.StreamingRequests);
        Assert.Same(request, Assert.Single(transportClient.Requests));
        Assert.Equal(ContractLiteralCodec.ToValue(expectedMethod), request.Method);
        Assert.Equal(expectedSessionToken, request.SessionToken);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), request.ResponseMode);
        return request;
    }

    public static IpcRequest SingleDispatchAttempted (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod)
    {
        var request = Assert.Single(transportClient.Requests);
        Assert.Equal(ContractLiteralCodec.ToValue(expectedMethod), request.Method);
        return request;
    }

    public static IpcRequest SingleStreamingDispatchAttempted (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod)
    {
        var request = Assert.Single(transportClient.StreamingRequests);
        Assert.Same(request, Assert.Single(transportClient.Requests));
        Assert.Equal(ContractLiteralCodec.ToValue(expectedMethod), request.Method);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), request.ResponseMode);
        return request;
    }

    public static IReadOnlyList<IpcRequest> RecoveredDispatchesWithReloadedSessionToken (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod,
        string firstSessionToken,
        string recoveredSessionToken)
    {
        var requests = IpcRequestAssert.Methods(transportClient, expectedMethod, expectedMethod);
        IpcRequestAssert.SessionTokens(requests, firstSessionToken, recoveredSessionToken);
        return requests;
    }

    public static Guid RecoverableDispatchRetriedWithReloadedSessionTokenAndAttemptTimeout (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod,
        string firstSessionToken,
        string recoveredSessionToken,
        TimeSpan expectedAttemptTimeout)
    {
        var requests = RecoveredDispatchesWithReloadedSessionToken(
            transportClient,
            expectedMethod,
            firstSessionToken,
            recoveredSessionToken);
        Assert.Collection(
            transportClient.Timeouts,
            timeout => Assert.Equal(expectedAttemptTimeout, timeout),
            timeout => Assert.Equal(expectedAttemptTimeout, timeout));
        return IpcRequestAssert.SingleRequestId(requests);
    }

    public static IReadOnlyList<IpcRequest> RecoveredStreamingDispatchesWithReloadedSessionToken (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod,
        string firstSessionToken,
        string recoveredSessionToken)
    {
        var requests = IpcRequestAssert.Methods(transportClient.StreamingRequests, expectedMethod, expectedMethod);
        Assert.Equal(requests.Count, transportClient.Requests.Count);
        IpcRequestAssert.SessionTokens(requests, firstSessionToken, recoveredSessionToken);
        Assert.All(
            requests,
            request => Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), request.ResponseMode));
        return requests;
    }

    public static IReadOnlyList<IpcRequest> RetriedDispatchesWithSameRequestId (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod,
        int maximumAttempts = int.MaxValue)
    {
        var requests = IpcRequestAssert.RetriedAtLeastOnce(transportClient, maximumAttempts);
        Assert.All(requests, request => Assert.Equal(ContractLiteralCodec.ToValue(expectedMethod), request.Method));
        IpcRequestAssert.SingleRequestId(requests);
        return requests;
    }
}
