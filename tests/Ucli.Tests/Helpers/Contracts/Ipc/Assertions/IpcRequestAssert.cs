using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class IpcRequestAssert
{
    public static IReadOnlyList<IpcRequestEnvelope> Methods (
        RecordingIpcTransportClient transportClient,
        params UnityIpcMethod[] expectedMethods)
    {
        return Methods(transportClient.Requests, expectedMethods);
    }

    public static IReadOnlyList<IpcRequestEnvelope> Methods (
        RecordingUnityIpcTransportClient transportClient,
        params UnityIpcMethod[] expectedMethods)
    {
        return Methods(transportClient.Requests, expectedMethods);
    }

    public static IReadOnlyList<IpcRequestEnvelope> Methods (
        IReadOnlyList<IpcRequestEnvelope> requests,
        params UnityIpcMethod[] expectedMethods)
    {
        Assert.Collection(
            requests,
            expectedMethods
                .Select<UnityIpcMethod, Action<IpcRequestEnvelope>>(
                    expectedMethod => request => Assert.Equal(TextVocabulary.GetText(expectedMethod), request.Method))
                .ToArray());
        return requests;
    }

    public static IReadOnlyList<IpcRequestEnvelope> RetriedAtLeastOnce (
        RecordingIpcTransportClient transportClient,
        int maximumAttempts = int.MaxValue)
    {
        return RetriedAtLeastOnce(transportClient.Requests, maximumAttempts);
    }

    public static IReadOnlyList<IpcRequestEnvelope> RetriedAtLeastOnce (
        RecordingUnityIpcTransportClient transportClient,
        int maximumAttempts = int.MaxValue)
    {
        return RetriedAtLeastOnce(transportClient.Requests, maximumAttempts);
    }

    public static IReadOnlyList<IpcRequestEnvelope> WithMethod (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod)
    {
        return WithMethod(transportClient.Requests, expectedMethod);
    }

    public static IReadOnlyList<IpcRequestEnvelope> WithMethod (
        RecordingUnityIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod)
    {
        return WithMethod(transportClient.Requests, expectedMethod);
    }

    public static IReadOnlyList<IpcRequestEnvelope> WithMethod (
        IReadOnlyList<IpcRequestEnvelope> requests,
        UnityIpcMethod expectedMethod)
    {
        var expectedMethodLiteral = TextVocabulary.GetText(expectedMethod);
        var matchingRequests = requests
            .Where(request => string.Equals(request.Method, expectedMethodLiteral, StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(matchingRequests);
        return matchingRequests;
    }

    public static IpcRequestEnvelope SingleWithMethod (
        IReadOnlyList<IpcRequestEnvelope> requests,
        UnityIpcMethod expectedMethod)
    {
        return Assert.Single(WithMethod(requests, expectedMethod));
    }

    public static IpcRequestEnvelope SingleWithMethod (
        RecordingIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod)
    {
        return SingleWithMethod(transportClient.Requests, expectedMethod);
    }

    public static IpcRequestEnvelope SingleWithMethod (
        RecordingUnityIpcTransportClient transportClient,
        UnityIpcMethod expectedMethod)
    {
        return SingleWithMethod(transportClient.Requests, expectedMethod);
    }

    public static IReadOnlyList<IpcRequestEnvelope> AllSessionToken (
        IReadOnlyList<IpcRequestEnvelope> requests,
        string expectedSessionToken)
    {
        Assert.All(
            requests,
            request => Assert.Equal(expectedSessionToken, request.SessionToken));
        return requests;
    }

    public static IReadOnlyList<IpcRequestEnvelope> SessionTokens (
        IReadOnlyList<IpcRequestEnvelope> requests,
        params string[] expectedSessionTokens)
    {
        Assert.Collection(
            requests,
            expectedSessionTokens
                .Select<string, Action<IpcRequestEnvelope>>(expectedToken => request => Assert.Equal(expectedToken, request.SessionToken))
                .ToArray());
        return requests;
    }

    public static Guid SingleRequestId (IReadOnlyList<IpcRequestEnvelope> requests)
    {
        return Assert.Single(requests.Select(static request => request.RequestId).Distinct());
    }

    public static UnityIpcMethod ParseMethod (IpcRequestEnvelope request)
    {
        Assert.True(
            TextVocabulary.TryGetValue(request.Method, out UnityIpcMethod method),
            $"Expected a canonical Unity IPC method, but was '{request.Method ?? "<null>"}'.");
        return method;
    }

    public static IReadOnlyList<IpcRequestEnvelope> RetriedAtLeastOnce (
        IReadOnlyList<IpcRequestEnvelope> requests,
        int maximumAttempts = int.MaxValue)
    {
        Assert.InRange(requests.Count, 2, maximumAttempts);
        return requests;
    }

}
