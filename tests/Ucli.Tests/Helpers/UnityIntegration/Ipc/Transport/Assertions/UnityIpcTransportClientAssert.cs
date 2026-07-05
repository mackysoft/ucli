using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class UnityIpcTransportClientAssert
{
    public static void SendForwardedToResolvedEndpoint (
        RecordingIpcTransportClient transportClient,
        IpcEndpoint expectedEndpoint,
        IpcRequest expectedRequest,
        TimeSpan expectedTimeout,
        CancellationToken expectedCancellationToken)
    {
        Assert.Equal(expectedEndpoint, Assert.Single(transportClient.Endpoints));
        Assert.Same(expectedRequest, Assert.Single(transportClient.Requests));
        Assert.Equal(expectedTimeout, Assert.Single(transportClient.Timeouts));
        Assert.Equal(expectedCancellationToken, Assert.Single(transportClient.CancellationTokens));
    }

    public static void StreamingSendForwardedToResolvedEndpoint (
        RecordingIpcTransportClient transportClient,
        IpcEndpoint expectedEndpoint,
        IpcRequest expectedRequest,
        TimeSpan expectedTimeout,
        CancellationToken expectedCancellationToken)
    {
        Assert.Equal(expectedEndpoint, Assert.Single(transportClient.Endpoints));
        Assert.Same(expectedRequest, Assert.Single(transportClient.StreamingRequests));
        Assert.Equal(expectedTimeout, Assert.Single(transportClient.Timeouts));
        Assert.Equal(expectedCancellationToken, Assert.Single(transportClient.CancellationTokens));
    }

    public static void NoEndpointRequestWasSent (RecordingIpcTransportClient transportClient)
    {
        Assert.Empty(transportClient.Requests);
    }

    public static void EndpointDispatchAddressedOnce (
        RecordingUnityIpcTransportClient transportClient,
        string expectedEndpointAddress)
    {
        var endpointInvocation = Assert.Single(transportClient.EndpointInvocations);
        Assert.Equal(expectedEndpointAddress, endpointInvocation.Endpoint.Address);
    }

    public static IpcRequest SingleStreamingRequestSent (
        RecordingUnityIpcTransportClient transportClient,
        string expectedMethod)
    {
        var request = Assert.Single(transportClient.StreamingRequests);
        Assert.Equal(expectedMethod, request.Method);
        return request;
    }
}
