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
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-cleanup-invalid-identity"));
        var artifactIdentity = DaemonSessionArtifactIdentity.Create("{ invalid session json");
        var readResult = DaemonSessionReadResultTestFactory.Invalid(artifactIdentity: artifactIdentity);
        var processTerminationService = new RecordingDaemonProcessTerminationService();
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var service = new DaemonSessionCleanupService(
            processTerminationService,
            artifactCleaner,
            new DaemonInvalidSessionCleanupSafetyEvaluator(new RecordingDaemonProcessIdentityAssessor()),
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

    [Theory]
    [InlineData(nameof(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess))]
    [InlineData(nameof(DaemonProcessIdentityAssessmentStatus.Uncertain))]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenInvalidEvidenceClaimsShutdownAuthorityAndProcessMayBeLive_BlocksWithoutTermination (
        string assessmentStatusName)
    {
        var assessmentStatus = Enum.Parse<DaemonProcessIdentityAssessmentStatus>(assessmentStatusName);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-cleanup-invalid"));
        var processStartedAtUtc = new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero);
        var artifactIdentity = DaemonSessionArtifactIdentity.Create("invalid-session-stop-target");
        var evidence = DaemonInvalidSessionEvidenceTestFactory.Create(
            context.ProjectFingerprint,
            processId: 3131,
            processStartedAtUtc);
        var readResult = DaemonSessionReadResultTestFactory.Invalid(evidence, artifactIdentity);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var processIdentityAssessor = new RecordingDaemonProcessIdentityAssessor(assessmentStatus);
        var service = new DaemonSessionCleanupService(
            processTerminationService,
            artifactCleaner,
            new DaemonInvalidSessionCleanupSafetyEvaluator(processIdentityAssessor),
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

        var result = await service.CleanupInvalidSessionArtifactsAsync(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Contains("cannot be safely replaced", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(processTerminationService.Invocations);
        Assert.Empty(artifactCleaner.Invocations);
        var assessmentInvocation = Assert.Single(processIdentityAssessor.Invocations);
        Assert.Equal(3131, assessmentInvocation.ProcessId);
        Assert.Equal(processStartedAtUtc, assessmentInvocation.ExpectedProcessStartedAtUtc);
    }

    [Theory]
    [InlineData(nameof(DaemonProcessIdentityAssessmentStatus.NotRunning))]
    [InlineData(nameof(DaemonProcessIdentityAssessmentStatus.DifferentProcess))]
    [Trait("Size", "Small")]
    public async Task CleanupInvalidSessionArtifacts_WhenClaimedProcessIsInactive_UsesArtifactIdentityCleanupOnly (
        string assessmentStatusName)
    {
        var assessmentStatus = Enum.Parse<DaemonProcessIdentityAssessmentStatus>(assessmentStatusName);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-cleanup-invalid-no-stop"));
        var artifactIdentity = DaemonSessionArtifactIdentity.Create("{ invalid project session json");
        var evidence = DaemonInvalidSessionEvidenceTestFactory.Create(
            context.ProjectFingerprint,
            processId: 7171);
        var readResult = DaemonSessionReadResultTestFactory.Invalid(evidence, artifactIdentity);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var processIdentityAssessor = new RecordingDaemonProcessIdentityAssessor(assessmentStatus);
        var service = new DaemonSessionCleanupService(
            processTerminationService,
            artifactCleaner,
            new DaemonInvalidSessionCleanupSafetyEvaluator(processIdentityAssessor),
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

        var result = await service.CleanupInvalidSessionArtifactsAsync(context, readResult, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonCleanupInvocationAssert.AssertSessionArtifactsInvalidatedWithoutProcessTermination(
            processTerminationService,
            artifactCleaner,
            context);
        Assert.Equal(artifactIdentity, Assert.Single(artifactCleaner.Invocations).ExpectedArtifactIdentity);
        Assert.Single(processIdentityAssessor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupStaleSessionArtifacts_WhenSessionExists_StopsThenCleansUp ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-cleanup-stale"));
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
            new DaemonInvalidSessionCleanupSafetyEvaluator(new RecordingDaemonProcessIdentityAssessor()),
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
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-cleanup-stale-user"));
        var session = DaemonSessionTestFactory.Create(
            processId: 4343,
            projectFingerprint: context.ProjectFingerprint,
            editorMode: "gui",
            ownerKind: "user",
            canShutdownProcess: false,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
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
            new DaemonInvalidSessionCleanupSafetyEvaluator(new RecordingDaemonProcessIdentityAssessor()),
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
    public async Task CleanupStaleSessionArtifacts_WhenStopFails_PropagatesFailureWithoutCleanup ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-cleanup-stop-fail"));
        var processStartedAtUtc = new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero);
        var session = DaemonSessionTestFactory.Create(
            processId: 5151,
            processStartedAtUtc: processStartedAtUtc,
            projectFingerprint: context.ProjectFingerprint);
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
            new DaemonInvalidSessionCleanupSafetyEvaluator(new RecordingDaemonProcessIdentityAssessor()),
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

        var result = await service.CleanupStaleSessionArtifactsAsync(context, session, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        DaemonCleanupInvocationAssert.AssertProcessTerminationAttemptedWithoutArtifactCleanup(
            processTerminationService,
            artifactCleaner,
            5151,
            processStartedAtUtc);
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
            ProjectFingerprintTestFactory.Create(invalidSession
                ? "fingerprint-invalid-cleanup-ownership"
                : "fingerprint-stale-cleanup-ownership"));
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
            new DaemonInvalidSessionCleanupSafetyEvaluator(new RecordingDaemonProcessIdentityAssessor()),
            owner,
            timeProvider);
        var cleanupTask = invalidSession
            ? service.CleanupInvalidSessionArtifactsAsync(
                    context,
                    DaemonSessionReadResultTestFactory.Invalid(
                        DaemonInvalidSessionEvidenceTestFactory.Create(
                            context.ProjectFingerprint,
                            processId: session.ProcessId,
                            processStartedAtUtc: session.ProcessStartedAtUtc)),
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
