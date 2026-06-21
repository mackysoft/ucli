using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonReachabilityProbeTests
{
    private static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan ProbeAttemptTimeoutCap = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenUnixSocketFileDoesNotExist_ReturnsNotRunningWithoutSendingPing ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("mode-probe", "unix-socket-missing");
        var daemonPingClient = new StubDaemonPingClient((_, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);

        var result = await probe.ProbeAsync(CreateContext(scope.FullPath), DefaultProbeTimeout, CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(0, daemonPingClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenPingSucceeds_ReturnsRunning ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "ping-success");
        var daemonPingClient = new StubDaemonPingClient((_, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);

        var context = CreateReadyContext(scope);
        var result = await probe.ProbeAsync(context, DefaultProbeTimeout, CancellationToken.None);

        Assert.True(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(1, daemonPingClient.CallCount);
        var observedProject = Assert.IsType<ResolvedUnityProjectContext>(daemonPingClient.LastUnityProject);
        Assert.Equal(context.UnityProjectRoot, observedProject.UnityProjectRoot);
        Assert.Equal(context.ProjectFingerprint, observedProject.ProjectFingerprint);
        Assert.Equal(ProbeAttemptTimeoutCap, daemonPingClient.LastTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenPingTimesOut_ReturnsTimeoutFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "ping-timeout");
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw new TimeoutException("timeout"));
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);
        var probeTimeout = TimeSpan.FromMilliseconds(150);

        var result = await probe.ProbeAsync(CreateReadyContext(scope), probeTimeout, CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("mode-probe", "connect-timeout");
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw new IpcConnectTimeoutException("connect timeout"));
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);
        var probeTimeout = TimeSpan.FromMilliseconds(150);

        var result = await probe.ProbeAsync(CreateReadyContext(scope), probeTimeout, CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("mode-probe", "connect-timeout-retry-success");
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
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);

        var result = await probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("mode-probe", "timeout-retry-success");
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
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);

        var result = await probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("mode-probe", "connectivity");
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw exception);
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);

        var result = await probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(1, daemonPingClient.CallCount);
    }

    public static IEnumerable<object[]> NotRunningConnectivityExceptions ()
    {
        yield return new object[] { new SocketException((int)SocketError.ConnectionRefused) };
        yield return new object[] { new DaemonPingResponseException("token invalid", IpcSessionErrorCodes.SessionTokenInvalid) };
        yield return new object[] { new DaemonPingResponseException("token required", IpcSessionErrorCodes.SessionTokenRequired) };
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
        using var scope = TestDirectories.CreateTempScope("mode-probe", "unexpected");
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw new InvalidOperationException("boom"));
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);

        var result = await probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("mode-probe", "io-failure");
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw exception);
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);

        var result = await probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("mode-probe", "ping-response-rejected");
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw new DaemonPingResponseException("status=error", UcliCoreErrorCodes.InternalError));
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);

        var result = await probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("mode-probe", "unauthorized");
        var daemonPingClient = new StubDaemonPingClient((_, _) => throw new UnauthorizedAccessException("unauthorized"));
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);

        var result = await probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, CancellationToken.None);

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
        var daemonPingClient = new StubDaemonPingClient((_, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                probe.ProbeAsync(CreateContext(Path.GetFullPath(".")), DefaultProbeTimeout, cancellationTokenSource.Token).AsTask(),
                "Canceled daemon reachability probe",
                SignalWaitTimeout);
        });
        Assert.Equal(0, daemonPingClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenCanceledDuringPing_ThrowsOperationCanceledException ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "canceled-during-ping");
        var pingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var daemonPingClient = new StubDaemonPingClient((_, cancellationToken) =>
        {
            pingStarted.TrySetResult();
            return new ValueTask(Task.Delay(System.Threading.Timeout.Infinite, cancellationToken));
        });
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);
        using var cancellationTokenSource = new CancellationTokenSource();

        var probeTask = probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, cancellationTokenSource.Token).AsTask();
        await TestAwaiter.WaitAsync(pingStarted.Task, "Daemon reachability ping start", SignalWaitTimeout);
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(probeTask, "Daemon reachability probe cancellation", SignalWaitTimeout);
        });
        Assert.Equal(1, daemonPingClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WithNestedUnityProject_UsesRepositoryRootForEndpointResolution ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "nested-project");
        var daemonPingClient = new StubDaemonPingClient((_, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);
        var repositoryRoot = scope.CreateDirectory("Repo");
        var unityProjectRoot = Path.Combine(repositoryRoot, "UnityProject");
        var context = CreateContext(unityProjectRoot, repositoryRoot);
        EnsureEndpointAllowsPing(context);

        var result = await probe.ProbeAsync(context, DefaultProbeTimeout, CancellationToken.None);

        Assert.True(result.IsRunning);
        Assert.Equal(repositoryRoot, daemonPingClient.LastUnityProject?.RepositoryRoot);
        Assert.NotEqual(unityProjectRoot, daemonPingClient.LastUnityProject?.RepositoryRoot);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Probe_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException (int timeoutMilliseconds)
    {
        var daemonPingClient = new StubDaemonPingClient((_, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient);
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                probe.ProbeAsync(CreateContext(Path.GetFullPath(".")), timeout, CancellationToken.None).AsTask(),
                "Invalid timeout daemon reachability probe",
                SignalWaitTimeout);
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

    private static ResolvedUnityProjectContext CreateReadyContext (TestDirectoryScope scope)
    {
        var context = CreateContext(scope.FullPath);
        EnsureEndpointAllowsPing(context);
        return context;
    }

    private static void EnsureEndpointAllowsPing (ResolvedUnityProjectContext context)
    {
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(context.RepositoryRoot, context.ProjectFingerprint);
        if (endpoint.TransportKind != IpcTransportKind.UnixDomainSocket)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
        File.WriteAllText(endpoint.Address, string.Empty);
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

        public ValueTask PingAsync (
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
