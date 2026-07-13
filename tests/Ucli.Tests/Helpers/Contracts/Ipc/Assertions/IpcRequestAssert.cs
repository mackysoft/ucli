using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class IpcRequestAssert
{
    public static IReadOnlyList<IpcRequest> Methods (
        RecordingIpcTransportClient transportClient,
        params UnityIpcMethod[] expectedMethods)
    {
        return Methods(transportClient.Requests, expectedMethods);
    }

    public static IReadOnlyList<IpcRequest> Methods (
        RecordingUnityIpcTransportClient transportClient,
        params UnityIpcMethod[] expectedMethods)
    {
        return Methods(transportClient.Requests, expectedMethods);
    }

    public static IReadOnlyList<IpcRequest> Methods (
        IReadOnlyList<IpcRequest> requests,
        params UnityIpcMethod[] expectedMethods)
    {
        Assert.Collection(
            requests,
            expectedMethods
                .Select<UnityIpcMethod, Action<IpcRequest>>(
                    expectedMethod => request => Assert.Equal(ContractLiteralCodec.ToValue(expectedMethod), request.Method))
                .ToArray());
        return requests;
    }

    public static IReadOnlyList<IpcRequest> RetriedAtLeastOnce (
        RecordingIpcTransportClient transportClient,
        int maximumAttempts = int.MaxValue)
    {
        return RetriedAtLeastOnce(transportClient.Requests, maximumAttempts);
    }

    public static IReadOnlyList<IpcRequest> RetriedAtLeastOnce (
        RecordingUnityIpcTransportClient transportClient,
        int maximumAttempts = int.MaxValue)
    {
        return RetriedAtLeastOnce(transportClient.Requests, maximumAttempts);
    }

    public static IReadOnlyList<IpcRequest> WithMethod (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod)
    {
        return WithMethod(transportClient.Requests, expectedMethod);
    }

    public static IReadOnlyList<IpcRequest> WithMethod (
        RecordingUnityIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod)
    {
        return WithMethod(transportClient.Requests, expectedMethod);
    }

    public static IReadOnlyList<IpcRequest> WithMethod (
        IReadOnlyList<IpcRequest> requests,
        UnityIpcMethod expectedMethod)
    {
        var expectedMethodLiteral = ContractLiteralCodec.ToValue(expectedMethod);
        var matchingRequests = requests
            .Where(request => string.Equals(request.Method, expectedMethodLiteral, StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(matchingRequests);
        return matchingRequests;
    }

    public static IpcRequest SingleWithMethod (
        IReadOnlyList<IpcRequest> requests,
        UnityIpcMethod expectedMethod)
    {
        return Assert.Single(WithMethod(requests, expectedMethod));
    }

    public static IpcRequest SingleWithMethod (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod)
    {
        return SingleWithMethod(transportClient.Requests, expectedMethod);
    }

    public static IpcRequest SingleWithMethod (
        RecordingUnityIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod)
    {
        return SingleWithMethod(transportClient.Requests, expectedMethod);
    }

    public static IReadOnlyList<IpcRequest> AllSessionToken (
        IReadOnlyList<IpcRequest> requests,
        string expectedSessionToken)
    {
        Assert.All(
            requests,
            request => Assert.Equal(expectedSessionToken, request.SessionToken));
        return requests;
    }

    public static IReadOnlyList<IpcRequest> SessionTokens (
        IReadOnlyList<IpcRequest> requests,
        params string[] expectedSessionTokens)
    {
        Assert.Collection(
            requests,
            expectedSessionTokens
                .Select<string, Action<IpcRequest>>(expectedToken => request => Assert.Equal(expectedToken, request.SessionToken))
                .ToArray());
        return requests;
    }

    public static Guid SingleRequestId (IReadOnlyList<IpcRequest> requests)
    {
        return Assert.Single(requests.Select(static request => request.RequestId).Distinct());
    }

    public static UnityIpcMethod ParseMethod (IpcRequest request)
    {
        Assert.True(
            ContractLiteralCodec.TryParse(request.Method, out UnityIpcMethod method),
            $"Expected a canonical Unity IPC method, but was '{request.Method ?? "<null>"}'.");
        return method;
    }

    public static IReadOnlyList<IpcRequest> RetriedAtLeastOnce (
        IReadOnlyList<IpcRequest> requests,
        int maximumAttempts = int.MaxValue)
    {
        Assert.InRange(requests.Count, 2, maximumAttempts);
        return requests;
    }

}
