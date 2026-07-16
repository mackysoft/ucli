using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.DaemonCleanupInvocationAssert;

public sealed class DaemonLaunchCompensationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenStopAndCleanupSucceed_ReturnsSuccess ()
    {
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-compensation-success"));
        var target = CreateTarget(2468);
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: context.ProjectFingerprint,
            processId: 2468,
            processStartedAtUtc: target.ProcessStartedAtUtc);
        var service = new DaemonLaunchCompensationService(
            processTerminationService,
            artifactCleaner,
            new ManualTimeProvider());
        var result = await service.CleanupFailedLaunchAsync(
            context,
            expectedSession: observedSession,
            target: target,
            timeout: TimeSpan.FromMilliseconds(250),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertProcessTerminationAttemptedThenArtifactsInvalidated(
            processTerminationService,
            artifactCleaner,
            context,
            processId: 2468,
            processStartedAtUtc: target.ProcessStartedAtUtc);
        var terminationTimeout = Assert.Single(processTerminationService.Invocations).Deadline.Timeout;
        Assert.InRange(
            terminationTimeout,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(250));
        Assert.Equal(observedSession, Assert.Single(artifactCleaner.Invocations).ExpectedSession);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenLauncherFailsBeforeProcessIdentity_CleansExpectedInitialSession ()
    {
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-compensation-initial-session"));
        var initialSession = DaemonSessionTestFactory.Create(
            projectFingerprint: context.ProjectFingerprint,
            processId: null,
            processStartedAtUtc: null);
        var service = new DaemonLaunchCompensationService(
            processTerminationService,
            artifactCleaner,
            new ManualTimeProvider());

        var result = await service.CleanupFailedLaunchAsync(
            context,
            expectedSession: initialSession,
            target: null,
            timeout: TimeSpan.FromMilliseconds(250),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(initialSession, Assert.Single(artifactCleaner.Invocations).ExpectedSession);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenNoSessionOrProcessWasPublished_CleansOnlyWhileSessionRemainsMissing ()
    {
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-compensation-missing-session"));
        var service = new DaemonLaunchCompensationService(
            processTerminationService,
            artifactCleaner,
            new ManualTimeProvider());

        var result = await service.CleanupFailedLaunchAsync(
            context,
            expectedSession: null,
            target: null,
            timeout: TimeSpan.FromMilliseconds(250),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var cleanupInvocation = Assert.Single(artifactCleaner.Invocations);
        Assert.Null(cleanupInvocation.ExpectedSession);
        Assert.Null(cleanupInvocation.ExpectedArtifactIdentity);
        Assert.Null(cleanupInvocation.ExpectedStoppedProcess);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenExpectedSessionExists_BindsCleanupToExpectedGeneration ()
    {
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-compensation-successor"));
        var failedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: context.ProjectFingerprint,
            sessionToken: "failed-session-token",
            processId: 2468,
            processStartedAtUtc: DateTimeOffset.UtcNow);
        var service = new DaemonLaunchCompensationService(
            processTerminationService,
            artifactCleaner,
            new ManualTimeProvider());
        var failedLaunchTarget = CreateTarget(2468);

        var result = await service.CleanupFailedLaunchAsync(
            context,
            expectedSession: failedSession,
            target: failedLaunchTarget,
            timeout: TimeSpan.FromMilliseconds(250),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(failedSession, Assert.Single(artifactCleaner.Invocations).ExpectedSession);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenStopFails_ReturnsFailureWithoutCleanup ()
    {
        var expectedError = ExecutionError.InternalError("stop failed");
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonLaunchCompensationService(
            processTerminationService,
            artifactCleaner,
            new ManualTimeProvider());
        var target = CreateTarget(8642);

        var result = await service.CleanupFailedLaunchAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-compensation-stop-fail")),
            expectedSession: null,
            target: target,
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        AssertProcessTerminationAttemptedWithoutArtifactCleanup(
            processTerminationService,
            artifactCleaner,
            processId: 8642,
            processStartedAtUtc: target.ProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenArtifactCleanupFails_ReturnsFailure ()
    {
        var expectedError = ExecutionError.InternalError("cleanup failed");
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Failure(expectedError),
        };
        var service = new DaemonLaunchCompensationService(
            processTerminationService,
            artifactCleaner,
            new ManualTimeProvider());
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-compensation-cleanup-fail"));
        var target = CreateTarget(1010);

        var result = await service.CleanupFailedLaunchAsync(
            context,
            expectedSession: null,
            target: target,
            timeout: TimeSpan.FromMilliseconds(400),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        AssertProcessTerminationAttemptedThenArtifactsInvalidated(
            processTerminationService,
            artifactCleaner,
            context,
            processId: 1010,
            processStartedAtUtc: target.ProcessStartedAtUtc);
        var cleanupInvocation = Assert.Single(artifactCleaner.Invocations);
        Assert.Null(cleanupInvocation.ExpectedSession);
        Assert.Equal(target, cleanupInvocation.ExpectedStoppedProcess);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenTimeoutExceedsCompensationCap_UsesTenSecondBudget ()
    {
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var service = new DaemonLaunchCompensationService(
            processTerminationService,
            artifactCleaner,
            new ManualTimeProvider());
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-compensation-timeout-cap"));
        var target = CreateTarget(4040);

        var result = await service.CleanupFailedLaunchAsync(
            context,
            expectedSession: null,
            target: target,
            timeout: TimeSpan.FromSeconds(15),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertProcessTerminationAttemptedThenArtifactsInvalidated(
            processTerminationService,
            artifactCleaner,
            context,
            processId: 4040,
            processStartedAtUtc: target.ProcessStartedAtUtc);
        var terminationTimeout = Assert.Single(processTerminationService.Invocations).Deadline.Timeout;
        Assert.InRange(
            terminationTimeout,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(10));
        Assert.Equal(target, Assert.Single(artifactCleaner.Invocations).ExpectedStoppedProcess);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenProcessTerminationConsumesRemainingBudget_ReturnsTimeoutWithoutStartingArtifactCleanup ()
    {
        var timeProvider = new ManualTimeProvider();
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            Handler = (_, _, _) =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(250));
                return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
            },
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var service = new DaemonLaunchCompensationService(
            processTerminationService,
            artifactCleaner,
            timeProvider);

        var result = await service.CleanupFailedLaunchAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
                ProjectFingerprintTestFactory.Create("fingerprint-compensation-shared-deadline")),
            expectedSession: null,
            target: CreateTarget(5050),
            timeout: TimeSpan.FromMilliseconds(250),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Empty(artifactCleaner.Invocations);
        Assert.Equal(TimeSpan.FromMilliseconds(250), Assert.Single(processTerminationService.Invocations).Deadline.Timeout);
    }

    private static DaemonProcessTerminationTarget CreateTarget (int processId)
    {
        return new DaemonProcessTerminationTarget(
            ProcessId: processId,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow);
    }

}
