using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonReachabilityProbeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeAsync_WhenUnixSocketFileDoesNotExist_ReturnsNotRunningWithoutSendingPing ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "unix-socket-missing");
        var socketPath = scope.GetPath("ipc.sock");
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, socketPath));
        var ipcClient = new StubUnityIpcClient((_, _) => ValueTask.FromResult(CreateResponse()));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, ipcClient);

        var result = await probe.ProbeAsync(CreateContext(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(0, ipcClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeAsync_WhenPingSucceeds_ReturnsRunning ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test"));
        var ipcClient = new StubUnityIpcClient((_, _) => ValueTask.FromResult(CreateResponse()));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, ipcClient);

        var result = await probe.ProbeAsync(CreateContext(Path.GetFullPath(".")), CancellationToken.None);

        Assert.True(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(1, ipcClient.CallCount);
        var request = Assert.IsType<IpcRequest>(ipcClient.LastRequest);
        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal(IpcMethodNames.Ping, request.Method);
        Assert.Equal("mode-probe", request.SessionToken);
        Assert.Equal("ucli-mode-probe", request.Payload.GetProperty("clientVersion").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeAsync_WhenPingTimesOut_ReturnsNotRunning ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-timeout"));
        var ipcClient = new StubUnityIpcClient((_, _) => throw new TimeoutException("timeout"));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, ipcClient);

        var result = await probe.ProbeAsync(CreateContext(Path.GetFullPath(".")), CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(1, ipcClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeAsync_WhenUnexpectedExceptionOccurs_ReturnsInternalError ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-failure"));
        var ipcClient = new StubUnityIpcClient((_, _) => throw new InvalidOperationException("boom"));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, ipcClient);

        var result = await probe.ProbeAsync(CreateContext(Path.GetFullPath(".")), CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.True(result.HasError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to probe daemon reachability.", error.Message, StringComparison.Ordinal);
    }

    private static ResolvedUnityProjectContext CreateContext (string projectRoot)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: projectRoot,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption,
            ConfigPath: Path.Combine(projectRoot, ".ucli", "config.json"));
    }

    private static IpcResponse CreateResponse ()
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "request-id",
            Status: "ok",
            Payload: JsonDocument.Parse("{}").RootElement.Clone(),
            Errors: Array.Empty<IpcError>());
    }

    private sealed class StubEndpointResolver : IIpcEndpointResolver
    {
        private readonly IpcEndpoint endpoint;

        public StubEndpointResolver (IpcEndpoint endpoint)
        {
            this.endpoint = endpoint;
        }

        public IpcEndpoint Resolve (
            string projectRoot,
            string projectFingerprint)
        {
            return endpoint;
        }
    }

    private sealed class StubUnityIpcClient : IUnityIpcClient
    {
        private readonly Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> handler;

        public StubUnityIpcClient (Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> handler)
        {
            this.handler = handler;
        }

        public int CallCount { get; private set; }

        public IpcRequest? LastRequest { get; private set; }

        public ValueTask<IpcResponse> SendAsync (
            string projectRoot,
            string projectFingerprint,
            IpcRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return handler(request, cancellationToken);
        }
    }
}
