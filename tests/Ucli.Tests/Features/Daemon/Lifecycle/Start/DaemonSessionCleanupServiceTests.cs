namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
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

public sealed class DaemonSessionCleanupServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenStopTargetIsAvailable_StopsThenCleansUp ()
    {
        var context = CreateContext("fingerprint-cleanup-invalid");
        var invalidSession = CreateSession(processId: 3131, projectFingerprint: context.ProjectFingerprint);
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            invalidSession);
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);

        var result = await service.CleanupInvalidSessionArtifacts(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(3131, processTerminationService.LastProcessId);
        Assert.Equal(invalidSession.IssuedAtUtc, processTerminationService.LastExpectedIssuedAtUtc);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenStopTargetIsNotAvailable_CleansUpOnly ()
    {
        var context = CreateContext("fingerprint-cleanup-invalid-no-stop");
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            new DaemonSession(
                SchemaVersion: DaemonSession.CurrentSchemaVersion,
                SessionToken: "session-token",
                ProjectFingerprint: "different-fingerprint",
                IssuedAtUtc: DateTimeOffset.UtcNow,
                RuntimeKind: DaemonSession.RuntimeKindBatchmode,
                OwnerKind: DaemonSession.OwnerKindSupervisor,
                CanShutdownProcess: true,
                EndpointTransportKind: "namedPipe",
                EndpointAddress: "ucli-daemon-test-endpoint",
                ProcessId: 7171,
                OwnerProcessId: 9876));
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);

        var result = await service.CleanupInvalidSessionArtifacts(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, processTerminationService.CallCount);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenOwnerProcessIdIsMissing_ReturnsFailureWithoutCleanup ()
    {
        var context = CreateContext("fingerprint-cleanup-invalid-legacy");
        var legacySession = CreateSession(
            processId: 8181,
            projectFingerprint: context.ProjectFingerprint,
            ownerProcessId: null);
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            legacySession);
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);

        var result = await service.CleanupInvalidSessionArtifacts(
            context,
            readResult,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("cannot be safely replaced", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, processTerminationService.CallCount);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupStaleSessionArtifacts_WhenSessionExists_StopsThenCleansUp ()
    {
        var context = CreateContext("fingerprint-cleanup-stale");
        var session = CreateSession(processId: 4242, projectFingerprint: context.ProjectFingerprint);
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);

        var result = await service.CleanupStaleSessionArtifacts(context, session, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(4242, processTerminationService.LastProcessId);
        Assert.Equal(session.IssuedAtUtc, processTerminationService.LastExpectedIssuedAtUtc);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenStopFails_PropagatesFailureWithoutCleanup ()
    {
        var context = CreateContext("fingerprint-cleanup-stop-fail");
        var invalidSession = CreateSession(processId: 5151, projectFingerprint: context.ProjectFingerprint);
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            invalidSession);
        var expectedError = ExecutionError.InternalError("stop failed");
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);

        var result = await service.CleanupInvalidSessionArtifacts(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (
        int? processId,
        string projectFingerprint = "fingerprint",
        int? ownerProcessId = 9876,
        string ownerKind = DaemonSession.OwnerKindSupervisor,
        bool canShutdownProcess = true)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: ownerKind,
            CanShutdownProcess: canShutdownProcess,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: processId,
            OwnerProcessId: ownerProcessId);
    }

    private sealed class StubDaemonProcessTerminationService : IDaemonProcessTerminationService
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public int? LastProcessId { get; private set; }

        public DateTimeOffset? LastExpectedIssuedAtUtc { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> EnsureStopped (
            int? processId,
            DateTimeOffset? expectedIssuedAtUtc,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastProcessId = processId;
            LastExpectedIssuedAtUtc = expectedIssuedAtUtc;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonArtifactCleaner : IDaemonArtifactCleaner
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> Cleanup (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }
}