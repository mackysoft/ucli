namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Start;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

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

        var result = await service.CleanupFailedLaunch(
            context,
            processId: 2468,
            expectedIssuedAtUtc: DateTimeOffset.UtcNow,
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

        var result = await service.CleanupFailedLaunch(
            CreateContext("fingerprint-compensation-stop-fail"),
            processId: 8642,
            expectedIssuedAtUtc: DateTimeOffset.UtcNow,
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

        var result = await service.CleanupFailedLaunch(
            CreateContext("fingerprint-compensation-cleanup-fail"),
            processId: 1010,
            expectedIssuedAtUtc: DateTimeOffset.UtcNow,
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

        var result = await service.CleanupFailedLaunch(
            CreateContext("fingerprint-compensation-timeout-cap"),
            processId: 4040,
            expectedIssuedAtUtc: DateTimeOffset.UtcNow,
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

    private sealed class StubDaemonProcessTerminationService : IDaemonProcessTerminationService
    {
        public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

        public int CallCount { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public ValueTask<DaemonSessionStoreOperationResult> EnsureStopped (
            int? processId,
            DateTimeOffset? expectedIssuedAtUtc,
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

        public ValueTask<DaemonSessionStoreOperationResult> Cleanup (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(NextResult);
        }
    }
}