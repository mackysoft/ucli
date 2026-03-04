namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Start;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

public sealed class DaemonStartRecoveryServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverInvalidSession_WhenStopTargetIsRecoverable_StopsThenCleansUp ()
    {
        var context = CreateContext("fingerprint-recover-invalid");
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
        var service = new DaemonStartRecoveryService(processTerminationService, artifactCleaner);

        var result = await service.RecoverInvalidSession(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(3131, processTerminationService.LastProcessId);
        Assert.Equal(invalidSession.IssuedAtUtc, processTerminationService.LastExpectedIssuedAtUtc);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverInvalidSession_WhenStopTargetIsNotRecoverable_CleansUpOnly ()
    {
        var context = CreateContext("fingerprint-recover-invalid-no-stop");
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            new DaemonSession(
                SchemaVersion: DaemonSession.CurrentSchemaVersion,
                SessionToken: "session-token",
                ProjectFingerprint: "different-fingerprint",
                IssuedAtUtc: DateTimeOffset.UtcNow,
                RuntimeKind: DaemonSession.RuntimeKindBatchmode,
                OwnerKind: DaemonSession.OwnerKindCli,
                CanShutdownProcess: true,
                EndpointTransportKind: "namedPipe",
                EndpointAddress: "ucli-test-endpoint",
                ProcessId: 7171));
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonStartRecoveryService(processTerminationService, artifactCleaner);

        var result = await service.RecoverInvalidSession(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, processTerminationService.CallCount);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverStaleSession_WhenSessionExists_StopsThenCleansUp ()
    {
        var context = CreateContext("fingerprint-recover-stale");
        var session = CreateSession(processId: 4242, projectFingerprint: context.ProjectFingerprint);
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonStartRecoveryService(processTerminationService, artifactCleaner);

        var result = await service.RecoverStaleSession(context, session, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(4242, processTerminationService.LastProcessId);
        Assert.Equal(session.IssuedAtUtc, processTerminationService.LastExpectedIssuedAtUtc);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RecoverInvalidSession_WhenStopFails_PropagatesFailureWithoutCleanup ()
    {
        var context = CreateContext("fingerprint-recover-stop-fail");
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
        var service = new DaemonStartRecoveryService(processTerminationService, artifactCleaner);

        var result = await service.RecoverInvalidSession(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
        string projectFingerprint = "fingerprint")
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindCli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-test-endpoint",
            ProcessId: processId);
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