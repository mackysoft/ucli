using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorStabilityVerifierTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStable_WhenRemainingTimeoutIsExhausted_ReturnsTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingClient = new StubDaemonPingClient
        {
            PingHandler = (_, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                return ValueTask.CompletedTask;
            },
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var verifier = new SupervisorStabilityVerifier(
            pingClient,
            new SupervisorDiagnosisWriter(diagnosisStore),
            timeProvider);
        var unityProject = CreateUnityProject();
        var session = CreateSession();

        var result = await verifier.EnsureStableAsync(
            unityProject,
            session,
            TimeSpan.FromMilliseconds(180),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error.Kind);
        Assert.NotNull(diagnosisStore.LastDiagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.StartupUnstable, diagnosisStore.LastDiagnosis.Reason);
        Assert.Equal("Unity daemon stability verification exceeded the remaining timeout.", diagnosisStore.LastDiagnosis.Message);
        Assert.NotEmpty(pingClient.Timeouts);
        if (pingClient.Timeouts.Count >= 2)
        {
            Assert.True(pingClient.Timeouts[^1] < pingClient.Timeouts[0]);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStable_WhenSuccessfulPingsCompleteWithinBudget_UsesCommandTimeoutBudget ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingClient = new StubDaemonPingClient
        {
            PingHandler = (_, timeout, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.Equal(TimeSpan.FromSeconds(1), timeout);
                return ValueTask.CompletedTask;
            },
        };
        var verifier = new SupervisorStabilityVerifier(
            pingClient,
            new SupervisorDiagnosisWriter(new StubDaemonDiagnosisStore()),
            timeProvider);

        var verificationTask = verifier.EnsureStableAsync(
                CreateUnityProject(),
                CreateSession(),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        for (var i = 0; i < 8 && !verificationTask.IsCompleted; i++)
        {
            await WaitForActiveTimerAsync(timeProvider, verificationTask, SignalWaitTimeout);
            timeProvider.Advance(TimeSpan.FromMilliseconds(700));
            await Task.Yield();
        }

        var result = await TestAwaiter.WaitAsync(
            verificationTask,
            "Supervisor stability verification",
            TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, pingClient.Timeouts.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStable_WhenPingFails_ReturnsFailureWithoutCompensationStop ()
    {
        var pingClient = new StubDaemonPingClient
        {
            PingHandler = static (_, _, _) => ValueTask.FromException(new InvalidOperationException("ping failed")),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var verifier = new SupervisorStabilityVerifier(
            pingClient,
            new SupervisorDiagnosisWriter(diagnosisStore));

        var result = await verifier.EnsureStableAsync(
            CreateUnityProject(),
            CreateSession(),
            TimeSpan.FromMilliseconds(400),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
        Assert.Contains("Unity daemon failed the supervisor stability window. ping failed", result.Error.Message, StringComparison.Ordinal);
        Assert.NotNull(diagnosisStore.LastDiagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStable_WhenDiagnosisWriteFails_ReturnsAugmentedFailure ()
    {
        var pingClient = new StubDaemonPingClient
        {
            PingHandler = static (_, _, _) => ValueTask.FromException(new InvalidOperationException("ping failed")),
        };
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(
                ExecutionError.InternalError("diagnosis failed")),
        };
        var verifier = new SupervisorStabilityVerifier(
            pingClient,
            new SupervisorDiagnosisWriter(diagnosisStore));

        var result = await verifier.EnsureStableAsync(
            CreateUnityProject(),
            CreateSession(),
            TimeSpan.FromMilliseconds(400),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("DiagnosisError=diagnosis failed", result.Error.Message, StringComparison.Ordinal);
    }

    private static ResolvedUnityProjectContext CreateUnityProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero),
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);
    }

    private static async Task WaitForActiveTimerAsync (
        ManualTimeProvider timeProvider,
        Task observedTask,
        TimeSpan timeout)
    {
        // NOTE: The verifier creates one timer for each retry delay. Waiting for the timer
        // before advancing manual time keeps this test independent from scheduler timing.
        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        while (timeProvider.ActiveTimerCount == 0 && !observedTask.IsCompleted)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1), timeoutCancellationTokenSource.Token).ConfigureAwait(false);
        }
    }

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        public Func<ResolvedUnityProjectContext, TimeSpan, CancellationToken, ValueTask>? PingHandler { get; set; }

        public List<TimeSpan> Timeouts { get; } = [];

        public async ValueTask PingAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            CancellationToken cancellationToken = default)
        {
            Timeouts.Add(timeout);
            if (PingHandler == null)
            {
                throw new InvalidOperationException("Ping handler is not configured.");
            }

            await PingHandler(unityProject, timeout, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public DaemonDiagnosis? LastDiagnosis { get; private set; }

        public DaemonDiagnosisStoreOperationResult WriteResult { get; set; } =
            DaemonDiagnosisStoreOperationResult.Success();

        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            LastDiagnosis = diagnosis;
            return ValueTask.FromResult(WriteResult);
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }
    }
}
