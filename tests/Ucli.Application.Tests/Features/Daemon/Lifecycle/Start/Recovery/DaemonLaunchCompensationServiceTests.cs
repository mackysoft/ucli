using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonLaunchCompensationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenStopAndCleanupSucceed_ReturnsSuccess ()
    {
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonLaunchCompensationService(processTerminationService, artifactCleaner);
        var context = CreateContext("fingerprint-compensation-success");

        var result = await service.CleanupFailedLaunchAsync(
            context,
            target: CreateTarget(2468),
            timeout: TimeSpan.FromMilliseconds(250),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(1, artifactCleaner.CallCount);
        Assert.Equal(TimeSpan.FromMilliseconds(250), processTerminationService.LastTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenStopFails_ReturnsFailureWithoutCleanup ()
    {
        var expectedError = ExecutionError.InternalError("stop failed");
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonLaunchCompensationService(processTerminationService, artifactCleaner);

        var result = await service.CleanupFailedLaunchAsync(
            CreateContext("fingerprint-compensation-stop-fail"),
            target: CreateTarget(8642),
            timeout: TimeSpan.FromMilliseconds(500),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(0, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenArtifactCleanupFails_ReturnsFailure ()
    {
        var expectedError = ExecutionError.InternalError("cleanup failed");
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var service = new DaemonLaunchCompensationService(processTerminationService, artifactCleaner);

        var result = await service.CleanupFailedLaunchAsync(
            CreateContext("fingerprint-compensation-cleanup-fail"),
            target: CreateTarget(1010),
            timeout: TimeSpan.FromMilliseconds(400),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        Assert.Equal(1, processTerminationService.CallCount);
        Assert.Equal(1, artifactCleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CleanupFailedLaunch_WhenTimeoutExceedsCompensationCap_UsesTenSecondBudget ()
    {
        var processTerminationService = new StubDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new StubDaemonArtifactCleaner
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var service = new DaemonLaunchCompensationService(processTerminationService, artifactCleaner);

        var result = await service.CleanupFailedLaunchAsync(
            CreateContext("fingerprint-compensation-timeout-cap"),
            target: CreateTarget(4040),
            timeout: TimeSpan.FromSeconds(15),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(10), processTerminationService.LastTimeout);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonProcessTerminationTarget CreateTarget (int processId)
    {
        return new DaemonProcessTerminationTarget(
            ProcessId: processId,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow);
    }

    private sealed class StubDaemonProcessTerminationService : IDaemonProcessTerminationService
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> EnsureStoppedAsync (
            DaemonProcessTerminationTarget? target,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastTimeout = timeout;
            return ValueTask.FromResult(NextResult);
        }
    }

    private sealed class StubDaemonArtifactCleaner : IDaemonArtifactCleaner
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> CleanupAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }
}
