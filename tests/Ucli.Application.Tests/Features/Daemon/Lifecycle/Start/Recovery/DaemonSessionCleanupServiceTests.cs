using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonSessionCleanupServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenArtifactIdentityIsAvailable_UsesConditionalCleanup ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-cleanup-invalid-identity");
        var artifactIdentity = DaemonSessionArtifactIdentity.Create("{ invalid session json");
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            session: null,
            artifactIdentity: artifactIdentity);
        var processTerminationService = new RecordingDaemonProcessTerminationService();
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var service = new DaemonSessionCleanupService(
            processTerminationService,
            artifactCleaner,
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

        var result = await service.CleanupInvalidSessionArtifactsAsync(
            context,
            readResult,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(processTerminationService.Invocations);
        var invocation = Assert.Single(artifactCleaner.Invocations);
        Assert.Null(invocation.ExpectedSession);
        Assert.Equal(artifactIdentity, invocation.ExpectedArtifactIdentity);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenStopTargetIsAvailable_StopsThenCleansUp ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-cleanup-invalid");
        var invalidSession = DaemonSessionTestFactory.Create(processId: 3131, projectFingerprint: context.ProjectFingerprint);
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            invalidSession,
            artifactIdentity: null);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonSessionCleanupService(
            processTerminationService,
            artifactCleaner,
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

        var result = await service.CleanupInvalidSessionArtifactsAsync(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonCleanupInvocationAssert.AssertProcessTerminationAttemptedThenArtifactsInvalidated(
            processTerminationService,
            artifactCleaner,
            context,
            3131,
            invalidSession.ProcessStartedAtUtc);
        var cleanupInvocation = Assert.Single(artifactCleaner.Invocations);
        Assert.Null(cleanupInvocation.ExpectedSession);
        Assert.Equal(
            new DaemonProcessTerminationTarget(3131, invalidSession.ProcessStartedAtUtc),
            cleanupInvocation.ExpectedStoppedProcess);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenStopTargetIsNotAvailable_CleansUpOnly ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-cleanup-invalid-no-stop");
        var artifactIdentity = DaemonSessionArtifactIdentity.Create("{ invalid project session json");
        var readResult = DaemonSessionReadResult.Failure(
            ExecutionError.InvalidArgument("invalid session"),
            DaemonSessionReadFailureKind.InvalidSession,
            DaemonSessionTestFactory.Create(
                processId: 7171,
                projectFingerprint: "different-fingerprint"),
            artifactIdentity);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonSessionCleanupService(
            processTerminationService,
            artifactCleaner,
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

        var result = await service.CleanupInvalidSessionArtifactsAsync(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonCleanupInvocationAssert.AssertSessionArtifactsInvalidatedWithoutProcessTermination(
            processTerminationService,
            artifactCleaner,
            context);
        Assert.Equal(artifactIdentity, Assert.Single(artifactCleaner.Invocations).ExpectedArtifactIdentity);
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
            legacySession,
            artifactIdentity: null);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonSessionCleanupService(
            processTerminationService,
            artifactCleaner,
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

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
        var service = new DaemonSessionCleanupService(
            processTerminationService,
            artifactCleaner,
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

        var result = await service.CleanupStaleSessionArtifactsAsync(context, session, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonCleanupInvocationAssert.AssertProcessTerminationAttemptedThenArtifactsInvalidated(
            processTerminationService,
            artifactCleaner,
            context,
            4242,
            session.ProcessStartedAtUtc);
        var cleanupInvocation = Assert.Single(artifactCleaner.Invocations);
        Assert.Null(cleanupInvocation.ExpectedSession);
        Assert.Equal(
            new DaemonProcessTerminationTarget(4242, session.ProcessStartedAtUtc),
            cleanupInvocation.ExpectedStoppedProcess);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupStaleSessionArtifacts_WhenSessionDisallowsShutdown_CleansUpWithoutStopping ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-cleanup-stale-user");
        var session = DaemonSessionTestFactory.Create(
            processId: 4343,
            projectFingerprint: context.ProjectFingerprint,
            editorMode: "gui",
            ownerKind: "user",
            canShutdownProcess: false);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonSessionCleanupService(
            processTerminationService,
            artifactCleaner,
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

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
            invalidSession,
            artifactIdentity: null);
        var expectedError = ExecutionError.InternalError("stop failed");
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonSessionCleanupService(
            processTerminationService,
            artifactCleaner,
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

        var result = await service.CleanupInvalidSessionArtifactsAsync(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        DaemonCleanupInvocationAssert.AssertProcessTerminationAttemptedWithoutArtifactCleanup(
            processTerminationService,
            artifactCleaner,
            5151,
            invalidSession.ProcessStartedAtUtc);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Size", "Small")]
    public async Task CleanupSessionArtifacts_WhenCleanupIgnoresCancellation_TimesOutAndRetainsOwnershipUntilQuiescence (
        bool invalidSession)
    {
        var timeout = TimeSpan.FromMilliseconds(100);
        var timeProvider = new ManualTimeProvider();
        var owner = new DaemonCompensationOperationOwner();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            invalidSession
                ? "fingerprint-invalid-cleanup-ownership"
                : "fingerprint-stale-cleanup-ownership");
        var session = DaemonSessionTestFactory.Create(
            processId: 6161,
            projectFingerprint: context.ProjectFingerprint);
        var cleanupStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            CleanupHandler = async (_, _) =>
            {
                cleanupStarted.TrySetResult();
                await releaseCleanup.Task.ConfigureAwait(false);
                return DaemonArtifactCleanupResult.Success();
            },
        };
        var service = new DaemonSessionCleanupService(
            new RecordingDaemonProcessTerminationService
            {
                NextResult = DaemonSessionStoreOperationResult.Success(),
            },
            artifactCleaner,
            owner,
            timeProvider);
        var cleanupTask = invalidSession
            ? service.CleanupInvalidSessionArtifactsAsync(
                    context,
                    DaemonSessionReadResult.Failure(
                        ExecutionError.InvalidArgument("invalid session"),
                        DaemonSessionReadFailureKind.InvalidSession,
                        session,
                        artifactIdentity: null),
                    timeout,
                    CancellationToken.None)
                .AsTask()
            : service.CleanupStaleSessionArtifactsAsync(
                    context,
                    session,
                    timeout,
                    CancellationToken.None)
                .AsTask();

        await TestAwaiter.WaitAsync(
            cleanupStarted.Task,
            "Session artifact cleanup start",
            TimeSpan.FromSeconds(5));
        await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(timeout);
        var result = await TestAwaiter.WaitAsync(
            cleanupTask,
            "Session artifact cleanup timeout",
            TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);

        var admissionTimeout = TimeSpan.FromMilliseconds(50);
        var admissionTask = owner.WaitForQuiescenceAsync(
                context,
                ExecutionDeadline.Start(admissionTimeout, timeProvider),
                CancellationToken.None,
                "Timed out waiting for session cleanup to quiesce.")
            .AsTask();
        await timeProvider.WaitForTimerDueWithinAsync(admissionTimeout).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(admissionTimeout);
        var admissionError = await TestAwaiter.WaitAsync(
            admissionTask,
            "Owned session cleanup admission timeout",
            TimeSpan.FromSeconds(5));
        Assert.Equal(ExecutionErrorKind.Timeout, admissionError!.Kind);

        releaseCleanup.TrySetResult();
        var quiescenceError = await TestAwaiter.WaitAsync(
            owner.WaitForQuiescenceAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                    CancellationToken.None,
                    "Timed out waiting for released session cleanup to quiesce.")
                .AsTask(),
            "Released session cleanup quiescence",
            TimeSpan.FromSeconds(5));
        Assert.Null(quiescenceError);
    }
}
