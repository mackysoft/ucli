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
        var service = new DaemonLaunchCompensationService(processTerminationService, artifactCleaner);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-compensation-success");
        var target = CreateTarget(2468);

        var result = await service.CleanupFailedLaunchAsync(
            context,
            target: target,
            timeout: TimeSpan.FromMilliseconds(250),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertProcessTerminationAttemptedThenArtifactsInvalidated(
            processTerminationService,
            artifactCleaner,
            context,
            processId: 2468,
            processStartedAtUtc: target.ProcessStartedAtUtc,
            timeout: TimeSpan.FromMilliseconds(250));
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
        var service = new DaemonLaunchCompensationService(processTerminationService, artifactCleaner);
        var target = CreateTarget(8642);

        var result = await service.CleanupFailedLaunchAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-compensation-stop-fail"),
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
        var service = new DaemonLaunchCompensationService(processTerminationService, artifactCleaner);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-compensation-cleanup-fail");
        var target = CreateTarget(1010);

        var result = await service.CleanupFailedLaunchAsync(
            context,
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
        var service = new DaemonLaunchCompensationService(processTerminationService, artifactCleaner);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-compensation-timeout-cap");
        var target = CreateTarget(4040);

        var result = await service.CleanupFailedLaunchAsync(
            context,
            target: target,
            timeout: TimeSpan.FromSeconds(15),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertProcessTerminationAttemptedThenArtifactsInvalidated(
            processTerminationService,
            artifactCleaner,
            context,
            processId: 4040,
            processStartedAtUtc: target.ProcessStartedAtUtc,
            timeout: TimeSpan.FromSeconds(10));
    }

    private static DaemonProcessTerminationTarget CreateTarget (int processId)
    {
        return new DaemonProcessTerminationTarget(
            ProcessId: processId,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow);
    }

}
