using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorStabilityVerifierTests
{
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

        var result = await verifier.EnsureStable(
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

        var result = await verifier.EnsureStable(
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

        var result = await verifier.EnsureStable(
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
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 1234,
            OwnerProcessId: 9876);
    }

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        public Func<ResolvedUnityProjectContext, TimeSpan, CancellationToken, ValueTask>? PingHandler { get; set; }

        public List<TimeSpan> Timeouts { get; } = [];

        public async ValueTask Ping (
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

        public ValueTask<DaemonDiagnosisReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Write (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            LastDiagnosis = diagnosis;
            return ValueTask.FromResult(WriteResult);
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }
    }
}