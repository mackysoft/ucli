using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonReachabilityProbeTests
{
    private static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan ProbeAttemptTimeoutCap = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenUnixSocketFileDoesNotExist_ReturnsNotRunningWithoutSendingPing ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "unix-socket-missing");
        var socketPath = scope.GetPath("ipc.sock");
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, socketPath));
        var daemonPingClient = new StubDaemonPingClient((_, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(scope.FullPath), DefaultProbeTimeout, CancellationToken.None);

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
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test"));
        var daemonPingClient = new StubDaemonPingClient((_, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var context = CreateContext(Path.GetFullPath("."));
        var result = await probe.Probe(context, DefaultProbeTimeout, CancellationToken.None);

        Assert.True(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(1, daemonPingClient.CallCount);
        var observedProject = Assert.IsType<ResolvedUnityProjectContext>(daemonPingClient.LastUnityProject);
        Assert.Equal(context.UnityProjectRoot, observedProject.UnityProjectRoot);
        Assert.Equal(context.RepositoryRoot, endpointResolver.LastStorageRoot);
        Assert.Equal(context.ProjectFingerprint, endpointResolver.LastProjectFingerprint);
        Assert.Equal(context.ProjectFingerprint, observedProject.ProjectFingerprint);
        Assert.Equal(ProbeAttemptTimeoutCap, daemonPingClient.LastTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenPingTimesOut_ReturnsTimeoutFailure ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-timeout"));
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw new TimeoutException("timeout"));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);
        var probeTimeout = TimeSpan.FromMilliseconds(150);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), probeTimeout, CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.True(result.HasError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("Timed out while probing daemon reachability.", error.Message, StringComparison.Ordinal);
        Assert.True(daemonPingClient.CallCount >= 1);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenConnectTimeoutOccurs_ReturnsTimeoutFailure ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-connect-timeout"));
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw new IpcConnectTimeoutException("connect timeout"));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);
        var probeTimeout = TimeSpan.FromMilliseconds(150);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), probeTimeout, CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.True(result.HasError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("Timed out while probing daemon reachability.", error.Message, StringComparison.Ordinal);
        Assert.True(daemonPingClient.CallCount >= 1);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenConnectTimeoutOccursBeforeDeadlineAndSubsequentPingSucceeds_ReturnsRunning ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-connect-timeout-retry-success"));
        var pingAttemptCount = 0;
        var daemonPingClient = new StubDaemonPingClient((_, _) =>
        {
            pingAttemptCount++;
            if (pingAttemptCount < 3)
            {
                throw new IpcConnectTimeoutException("connect timeout");
            }

            return ValueTask.CompletedTask;
        });
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), DefaultProbeTimeout, CancellationToken.None);

        Assert.True(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(3, daemonPingClient.CallCount);
        Assert.True(daemonPingClient.LastTimeout <= ProbeAttemptTimeoutCap);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenTimeoutOccursBeforeDeadlineAndSubsequentPingSucceeds_ReturnsRunning ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-retry-success"));
        var pingAttemptCount = 0;
        var daemonPingClient = new StubDaemonPingClient((_, _) =>
        {
            pingAttemptCount++;
            if (pingAttemptCount < 3)
            {
                throw new TimeoutException("timeout");
            }

            return ValueTask.CompletedTask;
        });
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), DefaultProbeTimeout, CancellationToken.None);

        Assert.True(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(3, daemonPingClient.CallCount);
        Assert.True(daemonPingClient.LastTimeout <= ProbeAttemptTimeoutCap);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(NotRunningConnectivityExceptions))]
    public async Task Probe_WhenConnectivityExceptionOccurs_ReturnsNotRunning (Exception exception)
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-connectivity"));
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw exception);
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), DefaultProbeTimeout, CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(1, daemonPingClient.CallCount);
    }

    public static IEnumerable<object[]> NotRunningConnectivityExceptions ()
    {
        yield return new object[] { new SocketException((int)SocketError.ConnectionRefused) };
        yield return new object[] { new DaemonPingResponseException("token invalid", IpcErrorCodes.SessionTokenInvalid) };
        yield return new object[] { new DaemonPingResponseException("token required", IpcErrorCodes.SessionTokenRequired) };
    }

    public static IEnumerable<object[]> IoFailureExceptions ()
    {
        yield return new object[] { new IOException("io") };
        yield return new object[] { new EndOfStreamException("truncated frame") };
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenUnexpectedExceptionOccurs_ReturnsInternalError ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-failure"));
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw new InvalidOperationException("boom"));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), DefaultProbeTimeout, CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.True(result.HasError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to probe daemon reachability.", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(IoFailureExceptions))]
    public async Task Probe_WhenIoFailureOccurs_ReturnsInternalError (Exception exception)
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-io-failure"));
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw exception);
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), DefaultProbeTimeout, CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.True(result.HasError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to probe daemon reachability.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenPingResponseIsRejected_ReturnsInternalError ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-ping-response-error"));
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw new DaemonPingResponseException("status=error", IpcErrorCodes.InternalError));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), DefaultProbeTimeout, CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.True(result.HasError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to probe daemon reachability.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenUnauthorizedAccessOccurs_ReturnsInternalError ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-unauthorized"));
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw new UnauthorizedAccessException("unauthorized"));
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);

        var result = await probe.Probe(CreateContext(Path.GetFullPath(".")), DefaultProbeTimeout, CancellationToken.None);

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
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-canceled"));
        var daemonPingClient = new StubDaemonPingClient((_, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await probe.Probe(CreateContext(Path.GetFullPath(".")), DefaultProbeTimeout, cancellationTokenSource.Token);
        });
        Assert.Equal(0, daemonPingClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenCanceledDuringPing_ThrowsOperationCanceledException ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-canceled-during-ping"));
        var pingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var daemonPingClient = new StubDaemonPingClient((_, cancellationToken) =>
        {
            pingStarted.TrySetResult();
            return new ValueTask(Task.Delay(System.Threading.Timeout.Infinite, cancellationToken));
        });
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);
        using var cancellationTokenSource = new CancellationTokenSource();

        var probeTask = probe.Probe(CreateContext(Path.GetFullPath(".")), DefaultProbeTimeout, cancellationTokenSource.Token).AsTask();
        await pingStarted.Task;
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await probeTask;
        });
        Assert.Equal(1, daemonPingClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WithNestedUnityProject_UsesRepositoryRootForEndpointResolution ()
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-nested-project"));
        var daemonPingClient = new StubDaemonPingClient((_, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);
        var repositoryRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Repo"));
        var unityProjectRoot = Path.Combine(repositoryRoot, "UnityProject");

        var result = await probe.Probe(CreateContext(unityProjectRoot, repositoryRoot), DefaultProbeTimeout, CancellationToken.None);

        Assert.True(result.IsRunning);
        Assert.Equal(repositoryRoot, endpointResolver.LastStorageRoot);
        Assert.NotEqual(unityProjectRoot, endpointResolver.LastStorageRoot);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Probe_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException (int timeoutMilliseconds)
    {
        var endpointResolver = new StubEndpointResolver(
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-invalid-timeout"));
        var daemonPingClient = new StubDaemonPingClient((_, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(endpointResolver, daemonPingClient);
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await probe.Probe(CreateContext(Path.GetFullPath(".")), timeout, CancellationToken.None);
        });
        Assert.Equal(0, daemonPingClient.CallCount);
    }

    private static ResolvedUnityProjectContext CreateContext (
        string projectRoot,
        string? repositoryRoot = null)
    {
        repositoryRoot ??= projectRoot;
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: projectRoot,
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private sealed class StubEndpointResolver : IIpcEndpointResolver
    {
        private readonly IpcEndpoint endpoint;

        public StubEndpointResolver (IpcEndpoint endpoint)
        {
            this.endpoint = endpoint;
        }

        public string? LastStorageRoot { get; private set; }

        public string? LastProjectFingerprint { get; private set; }

        public IpcEndpoint Resolve (
            string storageRoot,
            string projectFingerprint)
        {
            LastStorageRoot = storageRoot;
            LastProjectFingerprint = projectFingerprint;
            return endpoint;
        }
    }

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        private readonly Func<TimeSpan, CancellationToken, ValueTask> handler;

        public StubDaemonPingClient (Func<TimeSpan, CancellationToken, ValueTask> handler)
        {
            this.handler = handler;
        }

        public int CallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public ValueTask Ping (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            return handler(timeout, cancellationToken);
        }
    }
}