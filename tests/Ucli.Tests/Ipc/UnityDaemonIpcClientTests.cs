using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSuccessful_ResolvesSessionTokenAndDelegatesToTransport ()
    {
        var transportClient = new StubUnityIpcTransportClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.Success("daemon-token"));
        var client = new UnityDaemonIpcClient(transportClient, sessionTokenProvider);
        var response = CreateResponse("req-success");
        transportClient.Response = response;

        var result = await client.SendAsync(
            CreateContext(),
            IpcMethodNames.OpsRead,
            EmptyPayload(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(response, result.Response);
        Assert.Equal(1, sessionTokenProvider.CallCount);
        Assert.Equal(1, transportClient.CallCount);
        Assert.Equal("daemon-token", transportClient.LastRequest!.SessionToken);
        Assert.Equal(IpcMethodNames.OpsRead, transportClient.LastRequest.Method);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionTokenIsNotAvailable_ReturnsFailureWithoutCallingTransport ()
    {
        var transportClient = new StubUnityIpcTransportClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.SessionNotAvailable());
        var client = new UnityDaemonIpcClient(transportClient, sessionTokenProvider);

        var result = await client.SendAsync(
            CreateContext(),
            IpcMethodNames.OpsRead,
            EmptyPayload(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal(1, sessionTokenProvider.CallCount);
        Assert.Equal(0, transportClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenTransportTimesOut_ReturnsIpcTimeout ()
    {
        var transportClient = new StubUnityIpcTransportClient
        {
            Exception = new TimeoutException("timed out"),
        };
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.Success("daemon-token"));
        var client = new UnityDaemonIpcClient(transportClient, sessionTokenProvider);

        var result = await client.SendAsync(
            CreateContext(),
            IpcMethodNames.OpsRead,
            EmptyPayload(),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CliErrorCodes.IpcTimeout, result.ErrorCode);
    }

    private static ResolvedUnityProjectContext CreateContext ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private static IpcResponse CreateResponse (string requestId)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: EmptyPayload(),
            Errors: Array.Empty<IpcError>());
    }

    private sealed class StubUnityIpcTransportClient : IUnityIpcTransportClient
    {
        public int CallCount { get; private set; }

        public Exception? Exception { get; set; }

        public IpcRequest? LastRequest { get; private set; }

        public IpcResponse Response { get; set; } = CreateResponse("default");

        public ValueTask<IpcResponse> SendAsync (
            string storageRoot,
            string projectFingerprint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;

            if (Exception != null)
            {
                throw Exception;
            }

            return ValueTask.FromResult(Response);
        }
    }

    private sealed class StubDaemonSessionTokenProvider : IDaemonSessionTokenProvider
    {
        private readonly DaemonSessionTokenResolutionResult result;

        public StubDaemonSessionTokenProvider (DaemonSessionTokenResolutionResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public ValueTask<DaemonSessionTokenResolutionResult> Resolve (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }
}
