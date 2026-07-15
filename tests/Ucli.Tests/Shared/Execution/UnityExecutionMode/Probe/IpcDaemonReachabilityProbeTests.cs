using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonReachabilityProbeTests
{
    public static TheoryData<UcliCode> SessionAuthenticationErrorCodes =>
    [
        IpcSessionErrorCodes.SessionTokenRequired,
        IpcSessionErrorCodes.SessionTokenInvalid,
    ];

    private static readonly ProbeExceptionCase[] TimeoutProbeExceptionCases =
    [
        new("ping-timeout", static () => new TimeoutException("timeout")),
        new("connect-timeout", static () => new IpcConnectTimeoutException("connect timeout")),
    ];

    private static readonly ProbeExceptionCase[] NotRunningConnectivityExceptionCases =
    [
        new(
            "connection-refused",
            static () => IpcConnectExceptionTestFactory.FromSocketError(SocketError.ConnectionRefused)),
    ];

    private static readonly ProbeExceptionCase[] InternalFailureExceptionCases =
    [
        new("unexpected", static () => new InvalidOperationException("boom")),
        new("io-failure", static () => new IOException("io")),
        new("truncated-frame", static () => new EndOfStreamException("truncated frame")),
        new("connection-reset", static () => new SocketException((int)SocketError.ConnectionReset)),
        new("ping-response-rejected", static () => new DaemonPingResponseException("status=error", UcliCoreErrorCodes.InternalError)),
        new("unauthorized", static () => new UnauthorizedAccessException("unauthorized")),
    ];

    private static readonly int[] NonPositiveTimeoutMilliseconds =
    [
        0,
        -1,
    ];

    private static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan ProbeAttemptTimeoutCap = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan TimeoutClassificationProbeTimeout = TimeSpan.FromMilliseconds(50);

    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenUnixSocketFileDoesNotExist_ReturnsNotRunningWithoutSendingPing ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("mode-probe", "unix-socket-missing");
        var daemonPingClient = new UnexpectedDaemonPingClient("Missing Unix socket must return not running before ping.");
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            recoveryWaiter: null,
            TimeProvider.System);

        var result = await probe.ProbeAsync(
            ResolvedUnityProjectContextTestFactory.CreateWithPaths(
                unityProjectRoot: scope.FullPath,
                repositoryRoot: scope.FullPath,
                projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint")),
            DefaultProbeTimeout,
            CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenPingSucceeds_ReturnsRunning ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "ping-success");
        var daemonPingClient = new RecordingDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            recoveryWaiter: null,
            TimeProvider.System);

        var context = CreateReadyContext(scope);
        var result = await probe.ProbeAsync(context, DefaultProbeTimeout, CancellationToken.None);

        Assert.True(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        DaemonPingClientAssert.PingedOnceFor(
            daemonPingClient,
            context,
            ProbeAttemptTimeoutCap);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenTimeoutExceptionOccurs_ReturnsTimeoutFailure ()
    {
        foreach (ProbeExceptionCase exceptionCase in TimeoutProbeExceptionCases)
        {
            using var scope = TestDirectories.CreateTempScope("mode-probe", exceptionCase.ScopeName);
            var timeProvider = new ManualTimeProvider();
            var daemonPingClient = new RecordingDaemonPingClient((_, _, _, _) => throw exceptionCase.CreateException());
            var probe = new IpcDaemonReachabilityProbe(
                daemonPingClient,
                recoveryWaiter: null,
                timeProvider);

            var resultTask = probe.ProbeAsync(CreateReadyContext(scope), TimeoutClassificationProbeTimeout, CancellationToken.None).AsTask();
            await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
                timeProvider,
                resultTask,
                TimeoutClassificationProbeTimeout,
                TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
            var result = await resultTask;

            Assert.False(result.IsRunning);
            Assert.True(result.HasError);
            var error = Assert.IsType<ExecutionError>(result.Error);
            Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
            Assert.Contains("Timed out while probing daemon reachability.", error.Message, StringComparison.Ordinal);
            DaemonPingClientAssert.PingedAtLeastOnce(daemonPingClient);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenTimeoutOccursBeforeDeadlineAndSubsequentPingSucceeds_ReturnsRunning ()
    {
        foreach (ProbeExceptionCase exceptionCase in TimeoutProbeExceptionCases)
        {
            using var scope = TestDirectories.CreateTempScope("mode-probe", exceptionCase.ScopeName + "-retry-success");
            var timeProvider = new ManualTimeProvider();
            var pingAttemptCount = 0;
            var daemonPingClient = new RecordingDaemonPingClient((_, _, _, _) =>
            {
                pingAttemptCount++;
                if (pingAttemptCount < 2)
                {
                    throw exceptionCase.CreateException();
                }

                return ValueTask.CompletedTask;
            });
            var probe = new IpcDaemonReachabilityProbe(
                daemonPingClient,
                recoveryWaiter: null,
                timeProvider);

            var resultTask = probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, CancellationToken.None).AsTask();
            await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
                timeProvider,
                resultTask,
                DefaultProbeTimeout,
                TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
            var result = await resultTask;

            Assert.True(result.IsRunning);
            Assert.False(result.HasError);
            Assert.Null(result.Error);
            DaemonPingClientAssert.PingAttemptsUseTimeoutAtMost(
                daemonPingClient,
                ProbeAttemptTimeoutCap);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenConnectivityExceptionOccurs_ReturnsNotRunning ()
    {
        foreach (ProbeExceptionCase exceptionCase in NotRunningConnectivityExceptionCases)
        {
            using var scope = TestDirectories.CreateTempScope("mode-probe", exceptionCase.ScopeName);
            var daemonPingClient = new RecordingDaemonPingClient((_, _, _, _) => throw exceptionCase.CreateException());
            var probe = new IpcDaemonReachabilityProbe(
                daemonPingClient,
                recoveryWaiter: null,
                TimeProvider.System);

            var result = await probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, CancellationToken.None);

            Assert.False(result.IsRunning);
            Assert.False(result.HasError);
            Assert.Null(result.Error);
            DaemonPingClientAssert.PingedAtLeastOnce(daemonPingClient);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenLocalSessionMetadataIsUnavailable_ReturnsNotRunningWithoutTransportDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "session-unavailable");
        var transportClient = new UnexpectedIpcTransportClient(
            "Missing local session metadata must stop before sending IPC requests.");
        var daemonPingClient = new IpcDaemonPingClient(
            transportClient,
            new StaticDaemonSessionConnectionProvider(DaemonSessionConnectionResolutionResult.SessionNotAvailable()),
            TimeProvider.System);
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            recoveryWaiter: null,
            TimeProvider.System);

        var result = await probe.ProbeAsync(
            CreateReadyContext(scope),
            DefaultProbeTimeout,
            CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
    }

    [Theory]
    [MemberData(nameof(SessionAuthenticationErrorCodes))]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenReachableDaemonRejectsSessionAuthentication_ReturnsInternalError (UcliCode errorCode)
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", errorCode.Value.ToLowerInvariant());
        var transportClient = new RecordingIpcTransportClient(request =>
            IpcDaemonPingClientTestSupport.CreateResponse(
                request,
                IpcResponseStatus.Error,
                [
                    new IpcError(
                        errorCode,
                        "Session authentication rejected.",
                        OpId: null),
                ]));
        var daemonPingClient = new IpcDaemonPingClient(
            transportClient,
            IpcDaemonPingClientTestSupport.CreateResolvedSessionProvider(),
            TimeProvider.System);
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            recoveryWaiter: null,
            TimeProvider.System);

        var result = await probe.ProbeAsync(
            CreateReadyContext(scope),
            DefaultProbeTimeout,
            CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.True(result.HasError);
        Assert.Equal(ExecutionErrorKind.InternalError, Assert.IsType<ExecutionError>(result.Error).Kind);
        Assert.Single(transportClient.Requests);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Probe_WhenRecoveryReadConsumesDeadline_ReturnsTimeoutInsteadOfNotRunning (bool endpointMissing)
    {
        if (endpointMissing && OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("mode-probe", endpointMissing ? "missing-recovery-timeout" : "ping-recovery-timeout");
        var timeProvider = new ManualTimeProvider();
        var readStartedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadAsyncHandler = async (_, _, _) =>
            {
                readStartedSource.TrySetResult(true);
                await readReleaseSource.Task;
                return DaemonSessionReadResult.Missing();
            },
        };
        var recoveryWaiter = new UnityDaemonRecoveryWaiter(
            sessionStore,
            new RecordingDaemonLifecycleStore
            {
                ReadResult = DaemonLifecycleObservationReadResult.Success(null),
            },
            new RecordingDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess));
        IDaemonPingClient daemonPingClient = endpointMissing
            ? new UnexpectedDaemonPingClient("A missing endpoint must enter recovery before ping.")
            : new RecordingDaemonPingClient((_, _, _, _) =>
                throw IpcConnectExceptionTestFactory.FromSocketError(SocketError.ConnectionRefused));
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            recoveryWaiter,
            timeProvider);
        var context = endpointMissing
            ? ResolvedUnityProjectContextTestFactory.CreateWithPaths(
                unityProjectRoot: scope.FullPath,
                repositoryRoot: scope.FullPath,
                projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"))
            : CreateReadyContext(scope);
        var timeout = TimeSpan.FromSeconds(5);
        var probeTask = probe.ProbeAsync(context, timeout, CancellationToken.None).AsTask();

        try
        {
            await readStartedSource.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));

            timeProvider.Advance(timeout);

            var completedTask = await Task.WhenAny(probeTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(probeTask, completedTask);
            var result = await probeTask;
            Assert.False(result.IsRunning);
            Assert.True(result.HasError);
            Assert.Equal(ExecutionErrorKind.Timeout, Assert.IsType<ExecutionError>(result.Error).Kind);
        }
        finally
        {
            readReleaseSource.TrySetResult(true);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenInternalFailureOccurs_ReturnsInternalError ()
    {
        foreach (ProbeExceptionCase exceptionCase in InternalFailureExceptionCases)
        {
            using var scope = TestDirectories.CreateTempScope("mode-probe", exceptionCase.ScopeName);
            var daemonPingClient = new RecordingDaemonPingClient((_, _, _, _) => throw exceptionCase.CreateException());
            var probe = new IpcDaemonReachabilityProbe(
                daemonPingClient,
                recoveryWaiter: null,
                TimeProvider.System);

            var result = await probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, CancellationToken.None);

            Assert.False(result.IsRunning);
            Assert.True(result.HasError);
            var error = Assert.IsType<ExecutionError>(result.Error);
            Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
            Assert.Contains("Failed to probe daemon reachability.", error.Message, StringComparison.Ordinal);
            DaemonPingClientAssert.PingedAtLeastOnce(daemonPingClient);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenCanceled_ThrowsOperationCanceledException ()
    {
        var daemonPingClient = new UnexpectedDaemonPingClient("Canceled probe must stop before ping.");
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            recoveryWaiter: null,
            TimeProvider.System);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var projectRoot = Path.GetFullPath(".");

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                probe.ProbeAsync(
                    ResolvedUnityProjectContextTestFactory.CreateWithPaths(
                        unityProjectRoot: projectRoot,
                        repositoryRoot: projectRoot,
                        projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint")),
                    DefaultProbeTimeout,
                    cancellationTokenSource.Token).AsTask(),
                "Canceled daemon reachability probe",
                SignalWaitTimeout);
        });
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenCanceledDuringPing_ThrowsOperationCanceledException ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "canceled-during-ping");
        var pingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var daemonPingClient = new RecordingDaemonPingClient((_, _, _, cancellationToken) =>
        {
            pingStarted.TrySetResult();
            return new ValueTask(Task.Delay(System.Threading.Timeout.Infinite, cancellationToken));
        });
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            recoveryWaiter: null,
            TimeProvider.System);
        using var cancellationTokenSource = new CancellationTokenSource();

        var probeTask = probe.ProbeAsync(CreateReadyContext(scope), DefaultProbeTimeout, cancellationTokenSource.Token).AsTask();
        await TestAwaiter.WaitAsync(pingStarted.Task, "Daemon reachability ping start", SignalWaitTimeout);
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(probeTask, "Daemon reachability probe cancellation", SignalWaitTimeout);
        });
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WithNestedUnityProject_UsesRepositoryRootForEndpointResolution ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "nested-project");
        var daemonPingClient = new RecordingDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            recoveryWaiter: null,
            TimeProvider.System);
        var repositoryRoot = scope.CreateDirectory("Repo");
        var unityProjectRoot = Path.Combine(repositoryRoot, "UnityProject");
        var context = ResolvedUnityProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: unityProjectRoot,
            repositoryRoot: repositoryRoot,
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"));
        EnsureEndpointAllowsPing(context);

        var result = await probe.ProbeAsync(context, DefaultProbeTimeout, CancellationToken.None);

        Assert.True(result.IsRunning);
        DaemonPingClientAssert.PingedOnceFor(
            daemonPingClient,
            context,
            ProbeAttemptTimeoutCap);
        Assert.NotEqual(unityProjectRoot, context.RepositoryRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException ()
    {
        foreach (int timeoutMilliseconds in NonPositiveTimeoutMilliseconds)
        {
            var daemonPingClient = new UnexpectedDaemonPingClient("Invalid timeout must stop before ping.");
            var probe = new IpcDaemonReachabilityProbe(
                daemonPingClient,
                recoveryWaiter: null,
                TimeProvider.System);
            var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
            var projectRoot = Path.GetFullPath(".");

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await TestAwaiter.WaitAsync(
                    probe.ProbeAsync(
                        ResolvedUnityProjectContextTestFactory.CreateWithPaths(
                            unityProjectRoot: projectRoot,
                            repositoryRoot: projectRoot,
                            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint")),
                        timeout,
                        CancellationToken.None).AsTask(),
                    "Invalid timeout daemon reachability probe",
                    SignalWaitTimeout);
            });
        }
    }

    private static ResolvedUnityProjectContext CreateReadyContext (TestDirectoryScope scope)
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: scope.FullPath,
            repositoryRoot: scope.FullPath,
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"));
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

    private readonly record struct ProbeExceptionCase (
        string ScopeName,
        Func<Exception> CreateException);
}
