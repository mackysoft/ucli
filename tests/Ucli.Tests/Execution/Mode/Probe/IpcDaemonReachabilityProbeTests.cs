using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonReachabilityProbeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenUnixSocketFileDoesNotExist_ReturnsNotRunningWithoutSendingPing ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "unix-socket-missing");
        var socketPath = scope.GetPath("ipc.sock");
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, socketPath));
        var daemonPingClient = new StubDaemonPingClient(_ => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(0, daemonPingClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenPingSucceeds_ReturnsRunning ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test"));
        var daemonPingClient = new StubDaemonPingClient(_ => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var context = CreateContext(Path.GetFullPath("."));
        var result = await probe.Probe(context, CancellationToken.None);

        Assert.True(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(1, daemonPingClient.CallCount);
        var observedProject = Assert.IsType<ResolvedUnityProjectContext>(daemonPingClient.LastUnityProject);
        Assert.Equal(context.UnityProjectRoot, observedProject.UnityProjectRoot);
        Assert.Equal(context.ProjectFingerprint, observedProject.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenPingTimesOut_ReturnsNotRunning ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-timeout"));
        var daemonPingClient = new StubDaemonPingClient(_ => throw new TimeoutException("timeout"));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(1, daemonPingClient.CallCount);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(NotRunningConnectivityExceptions))]
    public async Task Probe_WhenConnectivityExceptionOccurs_ReturnsNotRunning (Exception exception)
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-connectivity"));
        var daemonPingClient = new StubDaemonPingClient(_ => throw exception);
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(1, daemonPingClient.CallCount);
    }

    public static IEnumerable<object[]> NotRunningConnectivityExceptions ()
    {
        yield return new object[] { new IOException("io") };
        yield return new object[] { new SocketException((int)SocketError.ConnectionRefused) };
        yield return new object[] { new UnauthorizedAccessException("unauthorized") };
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenUnexpectedExceptionOccurs_ReturnsInternalError ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-failure"));
        var daemonPingClient = new StubDaemonPingClient(_ => throw new InvalidOperationException("boom"));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.True(result.HasError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to probe daemon reachability.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenCanceled_ThrowsOperationCanceledException ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-canceled"));
        var daemonPingClient = new StubDaemonPingClient(_ => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await probe.Probe(CreateContext(Path.GetFullPath(".")), cancellationTokenSource.Token);
        });
        Assert.Equal(0, daemonPingClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenCanceledDuringPing_ThrowsOperationCanceledException ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-canceled-during-ping"));
        var daemonPingClient = new StubDaemonPingClient(cancellationToken =>
            new ValueTask(Task.Delay(Timeout.Infinite, cancellationToken)));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);
        using var cancellationTokenSource = new CancellationTokenSource();

        var probeTask = probe.Probe(CreateContext(Path.GetFullPath(".")), cancellationTokenSource.Token).AsTask();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await probeTask;
        });
        Assert.Equal(1, daemonPingClient.CallCount);
    }

    private static ResolvedUnityProjectContext CreateContext (string projectRoot)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: projectRoot,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption,
            ConfigPath: Path.Combine(projectRoot, ".ucli", "config.json"));
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

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        private readonly Func<CancellationToken, ValueTask> handler;

        public StubDaemonPingClient (Func<CancellationToken, ValueTask> handler)
        {
            this.handler = handler;
        }

        public int CallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public ValueTask Ping (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastUnityProject = unityProject;
            return handler(cancellationToken);
        }
    }
}