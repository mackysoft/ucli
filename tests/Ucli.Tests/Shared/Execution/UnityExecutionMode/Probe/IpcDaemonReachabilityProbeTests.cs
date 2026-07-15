using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
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
    public async Task Probe_WhenUnixSocketFileDoesNotExist_StillDelegatesRecoveryDecisionToPingClient ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("mode-probe", "unix-socket-missing");
        var daemonPingClient = new RecordingDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            TimeProvider.System);

        var result = await probe.ProbeAsync(
            ResolvedUnityProjectContextTestFactory.CreateWithPaths(
                unityProjectRoot: scope.FullPath,
                repositoryRoot: scope.FullPath,
                projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint")),
            DefaultProbeTimeout,
            CancellationToken.None);

        Assert.True(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        DaemonPingClientAssert.PingedAtLeastOnce(daemonPingClient);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenPingSucceeds_ReturnsRunning ()
    {
        using var scope = TestDirectories.CreateTempScope("mode-probe", "ping-success");
        var timeProvider = new ManualTimeProvider();
        var daemonPingClient = new RecordingDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            timeProvider);

        var context = CreateReadyContext(scope);
        var result = await probe.ProbeAsync(context, DefaultProbeTimeout, CancellationToken.None);

        Assert.True(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        DaemonPingClientAssert.PingedOnceFor(
            daemonPingClient,
            context,
            DefaultProbeTimeout);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenTimeoutExceptionOccurs_ReturnsTimeoutFailure ()
    {
        foreach (ProbeExceptionCase exceptionCase in TimeoutProbeExceptionCases)
        {
            using var scope = TestDirectories.CreateTempScope("mode-probe", exceptionCase.ScopeName);
            var timeProvider = new ManualTimeProvider();
            var pingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var daemonPingClient = new RecordingDaemonPingClient((_, _, _, _) =>
            {
                pingStarted.TrySetResult();
                throw exceptionCase.CreateException();
            });
            var probe = new IpcDaemonReachabilityProbe(
                daemonPingClient,
                timeProvider);

            var resultTask = probe.ProbeAsync(CreateReadyContext(scope), TimeoutClassificationProbeTimeout, CancellationToken.None).AsTask();
            await TestAwaiter.WaitAsync(
                pingStarted.Task,
                "daemon reachability ping start",
                SignalWaitTimeout);
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
                DefaultProbeTimeout);
            DaemonPingClientAssert.StabilityVerificationAttemptedBeforeRemainingTimeoutExhausted(
                daemonPingClient);
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
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing())),
            TimeProvider.System);
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            TimeProvider.System);

        var result = await probe.ProbeAsync(
            CreateReadyContext(scope),
            DefaultProbeTimeout,
            CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenFixedSessionEndpointRefusesConnections_ReturnsNotRunningAfterEndpointWindow ()
    {
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var fixedSession = DaemonSessionTestFactory.CreateForToken(
            "fixed-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-fixed-session");
        var transportClient = new RecordingIpcTransportClient(_ =>
            throw IpcConnectExceptionTestFactory.FromSocketError(SocketError.ConnectionRefused));
        var daemonPingClient = new IpcDaemonPingClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(fixedSession))),
            timeProvider);
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient, timeProvider);

        var resultTask = probe.ProbeAsync(
                ResolvedUnityProjectContextTestFactory.Create(
                    projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint")),
                DefaultProbeTimeout,
                CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(
            ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
                    timeProvider,
                    resultTask,
                    ProbeAttemptTimeoutCap,
                    TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds))
                .AsTask(),
            "daemon reachability endpoint window manual time",
            SignalWaitTimeout);

        var result = await resultTask;

        Assert.False(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Null(result.Error);
        Assert.Equal(startedAtUtc + ProbeAttemptTimeoutCap, timeProvider.GetUtcNow());
        Assert.True(timeProvider.GetUtcNow() < startedAtUtc + DefaultProbeTimeout);
        Assert.NotEmpty(transportClient.Requests);
        Assert.All(
            transportClient.Timeouts,
            timeout => Assert.InRange(timeout, TimeSpan.FromTicks(1), ProbeAttemptTimeoutCap));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Probe_WhenResponseAttemptTimesOutAndSuccessorPublishes_RetriesSuccessorWithinOuterDeadline ()
    {
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var initialSession = DaemonSessionTestFactory.CreateForToken(
            "initial-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-initial-session");
        var successorSession = DaemonSessionTestFactory.CreateForToken(
            "successor-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-successor-session");
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count == 1
                ? DaemonSessionReadResultTestFactory.Found(initialSession)
                : DaemonSessionReadResultTestFactory.Found(successorSession),
        };
        var transportClient = new FirstResponseTimeoutTransportClient(timeProvider);
        var daemonPingClient = new IpcDaemonPingClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore),
            timeProvider);
        var probe = new IpcDaemonReachabilityProbe(daemonPingClient, timeProvider);

        var resultTask = probe.ProbeAsync(
                ResolvedUnityProjectContextTestFactory.Create(
                    projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint")),
                DefaultProbeTimeout,
                CancellationToken.None)
            .AsTask();
        await timeProvider
            .WaitForTimerDueWithinAsync(ProbeAttemptTimeoutCap)
            .WaitAsync(SignalWaitTimeout);
        timeProvider.Advance(ProbeAttemptTimeoutCap);

        var result = await resultTask.WaitAsync(SignalWaitTimeout);

        Assert.True(result.IsRunning);
        Assert.False(result.HasError);
        Assert.Collection(
            transportClient.Requests,
            request => Assert.Equal(initialSession.SessionToken.GetEncodedValue(), request.SessionToken),
            request => Assert.Equal(successorSession.SessionToken.GetEncodedValue(), request.SessionToken));
        Assert.All(
            transportClient.Timeouts,
            timeout => Assert.InRange(timeout, TimeSpan.FromTicks(1), ProbeAttemptTimeoutCap));
        Assert.Equal(transportClient.Requests[0].RequestId, transportClient.Requests[1].RequestId);
        Assert.Equal(transportClient.Requests[0].RequestDeadlineUtc, transportClient.Requests[1].RequestDeadlineUtc);
        Assert.Equal(startedAtUtc + DefaultProbeTimeout, transportClient.Requests[0].RequestDeadlineUtc);
        Assert.Equal(startedAtUtc + ProbeAttemptTimeoutCap, timeProvider.GetUtcNow());
        Assert.True(timeProvider.GetUtcNow() < startedAtUtc + DefaultProbeTimeout);
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
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(new RecordingDaemonSessionStore
            {
                ReadHandler = invocations => invocations.Count == 1
                    ? DaemonSessionReadResultTestFactory.FoundForToken("resolved-token")
                    : DaemonSessionReadResultTestFactory.FoundForToken("replacement-token"),
            }),
            TimeProvider.System);
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            TimeProvider.System);

        var result = await probe.ProbeAsync(
            CreateReadyContext(scope),
            DefaultProbeTimeout,
            CancellationToken.None);

        Assert.False(result.IsRunning);
        Assert.True(result.HasError);
        Assert.Equal(ExecutionErrorKind.InternalError, Assert.IsType<ExecutionError>(result.Error).Kind);
        var expectedAttemptCount = errorCode == IpcSessionErrorCodes.SessionTokenInvalid ? 2 : 1;
        Assert.Equal(expectedAttemptCount, transportClient.Requests.Count);
        _ = IpcRequestAssert.SingleRequestId(transportClient.Requests);
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
        var timeProvider = new ManualTimeProvider();
        var daemonPingClient = new RecordingDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask);
        var probe = new IpcDaemonReachabilityProbe(
            daemonPingClient,
            timeProvider);
        var repositoryRoot = scope.CreateDirectory("Repo");
        var unityProjectRoot = Path.Combine(repositoryRoot, "UnityProject");
        var context = ResolvedUnityProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: unityProjectRoot,
            repositoryRoot: repositoryRoot,
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"));
        var result = await probe.ProbeAsync(context, DefaultProbeTimeout, CancellationToken.None);

        Assert.True(result.IsRunning);
        DaemonPingClientAssert.PingedOnceFor(
            daemonPingClient,
            context,
            DefaultProbeTimeout);
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
        return ResolvedUnityProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: scope.FullPath,
            repositoryRoot: scope.FullPath,
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"));
    }

    private sealed class FirstResponseTimeoutTransportClient : IIpcTransportClient
    {
        private readonly TimeProvider timeProvider;

        private readonly List<IpcRequestEnvelope> requests = [];

        private readonly List<TimeSpan> timeouts = [];

        public FirstResponseTimeoutTransportClient (TimeProvider timeProvider)
        {
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public IReadOnlyList<IpcRequestEnvelope> Requests => requests;

        public IReadOnlyList<TimeSpan> Timeouts => timeouts;

        public async ValueTask<IpcResponse> SendAsync (
            IpcEndpoint endpoint,
            IpcRequestEnvelope request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            requests.Add(request);
            timeouts.Add(timeout);

            if (requests.Count == 1)
            {
                await TimeProviderDelay.DelayAsync(timeout, timeProvider, cancellationToken).ConfigureAwait(false);
                throw new TimeoutException("The first daemon ping response did not arrive within the transport attempt timeout.");
            }

            return IpcDaemonPingClientTestSupport.CreateResponse(
                request,
                IpcResponseStatus.Ok,
                [],
                IpcUnityEditorObservationTestFactory.Create(
                    projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint")));
        }

        public ValueTask<IpcResponse> SendStreamingAsync (
            IpcEndpoint endpoint,
            IpcRequestEnvelope request,
            TimeSpan timeout,
            Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The reachability probe does not use streaming IPC.");
        }

        public ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
            IpcEndpoint endpoint,
            IpcRequestEnvelope request,
            TimeSpan sendTimeout,
            Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The reachability probe does not use streaming IPC.");
        }

        public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
            IpcEndpoint endpoint,
            IpcRequestEnvelope request,
            TimeSpan sendTimeout,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The reachability probe uses bounded IPC dispatch.");
        }
    }

    private readonly record struct ProbeExceptionCase (
        string ScopeName,
        Func<Exception> CreateException);
}
