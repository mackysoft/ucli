using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonSessionCleanupServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenStopTargetIsAvailable_StopsThenCleansUp ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-cleanup-invalid");
        var invalidSession = DaemonSessionTestFactory.Create(processId: 3131, projectFingerprint: context.ProjectFingerprint);
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            invalidSession);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);

        var result = await service.CleanupInvalidSessionArtifactsAsync(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonCleanupInvocationAssert.AssertProcessTerminationAttemptedThenArtifactsInvalidated(
            processTerminationService,
            artifactCleaner,
            context,
            3131,
            invalidSession.ProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenStopTargetIsNotAvailable_CleansUpOnly ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-cleanup-invalid-no-stop");
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            DaemonSessionTestFactory.Create(
                processId: 7171,
                projectFingerprint: "different-fingerprint"));
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);

        var result = await service.CleanupInvalidSessionArtifactsAsync(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonCleanupInvocationAssert.AssertSessionArtifactsInvalidatedWithoutProcessTermination(
            processTerminationService,
            artifactCleaner,
            context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenOwnerProcessIdIsMissing_ReturnsFailureWithoutCleanup ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-cleanup-invalid-legacy");
        var legacySession = DaemonSessionTestFactory.Create(
            processId: 8181,
            projectFingerprint: context.ProjectFingerprint,
            ownerProcessId: null);
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            legacySession);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);

        var result = await service.CleanupInvalidSessionArtifactsAsync(
            context,
            readResult,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("cannot be safely replaced", error.Message, StringComparison.Ordinal);
        DaemonCleanupInvocationAssert.AssertProcessTerminationAndArtifactCleanupSkipped(
            processTerminationService,
            artifactCleaner);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupStaleSessionArtifacts_WhenSessionExists_StopsThenCleansUp ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-cleanup-stale");
        var session = DaemonSessionTestFactory.Create(processId: 4242, projectFingerprint: context.ProjectFingerprint);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);

        var result = await service.CleanupStaleSessionArtifactsAsync(context, session, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonCleanupInvocationAssert.AssertProcessTerminationAttemptedThenArtifactsInvalidated(
            processTerminationService,
            artifactCleaner,
            context,
            4242,
            session.ProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupStaleSessionArtifacts_WhenSessionDisallowsShutdown_CleansUpWithoutStopping ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-cleanup-stale-user");
        var session = DaemonSessionTestFactory.Create(
            processId: 4343,
            projectFingerprint: context.ProjectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);

        var result = await service.CleanupStaleSessionArtifactsAsync(context, session, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonCleanupInvocationAssert.AssertSessionArtifactsInvalidatedWithoutProcessTermination(
            processTerminationService,
            artifactCleaner,
            context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenStopFails_PropagatesFailureWithoutCleanup ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-cleanup-stop-fail");
        var invalidSession = DaemonSessionTestFactory.Create(processId: 5151, projectFingerprint: context.ProjectFingerprint);
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            invalidSession);
        var expectedError = ExecutionError.InternalError("stop failed");
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonSessionCleanupService(processTerminationService, artifactCleaner);

        var result = await service.CleanupInvalidSessionArtifactsAsync(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        DaemonCleanupInvocationAssert.AssertProcessTerminationAttemptedWithoutArtifactCleanup(
            processTerminationService,
            artifactCleaner,
            5151,
            invalidSession.ProcessStartedAtUtc);
    }
}
