using MackySoft.Tests;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests;

public sealed class UnityProjectLockPreflightServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenLockFileDoesNotExist_ReturnsUnlocked ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "unlocked");
        var unityProject = CreateContext(scope);
        var service = new UnityProjectLockPreflightService(
            new UnityProjectLockFileProbe(),
            new StubUnityProjectLockOwnerProbe(UnityProjectLockOwnerProbeResult.NoOwner()),
            new UnityProjectLockFileCleaner());

        var result = await service.PrepareForUnityProcessStartAsync(unityProject, CancellationToken.None);

        Assert.Equal(UnityProjectLockPreflightStatus.Unlocked, result.Status);
        Assert.False(File.Exists(scope.GetPath("UnityProject/Temp/UnityLockfile")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenOwnerIsActive_ReturnsActiveLockWithoutDeletingLockFile ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "active");
        var unityProject = CreateContext(scope);
        var lockFilePath = scope.WriteFile("UnityProject/Temp/UnityLockfile", string.Empty);
        var cleaner = new StubUnityProjectLockFileCleaner(UnityProjectLockFileCleanupResult.Success());
        var service = new UnityProjectLockPreflightService(
            new UnityProjectLockFileProbe(),
            new StubUnityProjectLockOwnerProbe(UnityProjectLockOwnerProbeResult.ActiveOwner(
                UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProject.UnityProjectRoot, lockFilePath))),
            cleaner);

        var result = await service.PrepareForUnityProcessStartAsync(unityProject, CancellationToken.None);

        Assert.Equal(UnityProjectLockPreflightStatus.ActiveLock, result.Status);
        Assert.True(File.Exists(lockFilePath));
        Assert.Equal(0, cleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenOwnerIsAbsent_DeletesStaleLockFile ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "stale");
        var unityProject = CreateContext(scope);
        var lockFilePath = scope.WriteFile("UnityProject/Temp/UnityLockfile", string.Empty);
        var service = new UnityProjectLockPreflightService(
            new UnityProjectLockFileProbe(),
            new StubUnityProjectLockOwnerProbe(UnityProjectLockOwnerProbeResult.NoOwner()),
            new UnityProjectLockFileCleaner());

        var result = await service.PrepareForUnityProcessStartAsync(unityProject, CancellationToken.None);

        Assert.Equal(UnityProjectLockPreflightStatus.StaleLockCleared, result.Status);
        Assert.False(File.Exists(lockFilePath));
        Assert.Contains("Stale Unity project lock file was removed", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenOwnershipIsAmbiguous_ReturnsAmbiguousWithoutDeletingLockFile ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "ambiguous");
        var unityProject = CreateContext(scope);
        var lockFilePath = scope.WriteFile("UnityProject/Temp/UnityLockfile", string.Empty);
        var cleaner = new StubUnityProjectLockFileCleaner(UnityProjectLockFileCleanupResult.Success());
        var service = new UnityProjectLockPreflightService(
            new UnityProjectLockFileProbe(),
            new StubUnityProjectLockOwnerProbe(UnityProjectLockOwnerProbeResult.Ambiguous("process scan failed")),
            cleaner);

        var result = await service.PrepareForUnityProcessStartAsync(unityProject, CancellationToken.None);

        Assert.Equal(UnityProjectLockPreflightStatus.Ambiguous, result.Status);
        Assert.True(File.Exists(lockFilePath));
        Assert.Equal(0, cleaner.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenStaleLockCleanupFails_ReturnsCleanupFailedWithoutDeletingLockFile ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "cleanup-failed");
        var unityProject = CreateContext(scope);
        var lockFilePath = scope.WriteFile("UnityProject/Temp/UnityLockfile", string.Empty);
        var service = new UnityProjectLockPreflightService(
            new UnityProjectLockFileProbe(),
            new StubUnityProjectLockOwnerProbe(UnityProjectLockOwnerProbeResult.NoOwner()),
            new StubUnityProjectLockFileCleaner(UnityProjectLockFileCleanupResult.Failure(
                UnityProjectLockFailureMessage.CreateCleanupFailed(lockFilePath, "access denied"))));

        var result = await service.PrepareForUnityProcessStartAsync(unityProject, CancellationToken.None);

        Assert.Equal(UnityProjectLockPreflightStatus.CleanupFailed, result.Status);
        Assert.True(File.Exists(lockFilePath));
        Assert.Contains("access denied", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenLockInspectionFails_ReturnsInspectionFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "inspection-failed");
        var service = new UnityProjectLockPreflightService(
            new StubUnityProjectLockFileProbe(UnityProjectLockFileProbeResult.Failure("probe failed")),
            new StubUnityProjectLockOwnerProbe(UnityProjectLockOwnerProbeResult.NoOwner()),
            new UnityProjectLockFileCleaner());

        var result = await service.PrepareForUnityProcessStartAsync(CreateContext(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockPreflightStatus.InspectionFailed, result.Status);
        Assert.Contains("probe failed", result.Message, StringComparison.Ordinal);
    }

    private static ResolvedUnityProjectContext CreateContext (TestDirectoryScope scope)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: scope.CreateDirectory("UnityProject"),
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private sealed class StubUnityProjectLockOwnerProbe : IUnityProjectLockOwnerProbe
    {
        private readonly UnityProjectLockOwnerProbeResult result;

        public StubUnityProjectLockOwnerProbe (UnityProjectLockOwnerProbeResult result)
        {
            this.result = result;
        }

        public ValueTask<UnityProjectLockOwnerProbeResult> ProbeOwnerAsync (
            ResolvedUnityProjectContext unityProject,
            string lockFilePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubUnityProjectLockFileCleaner : IUnityProjectLockFileCleaner
    {
        private readonly UnityProjectLockFileCleanupResult result;

        public StubUnityProjectLockFileCleaner (UnityProjectLockFileCleanupResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public UnityProjectLockFileCleanupResult Delete (string lockFilePath)
        {
            CallCount++;
            return result;
        }
    }

    private sealed class StubUnityProjectLockFileProbe : IUnityProjectLockFileProbe
    {
        private readonly UnityProjectLockFileProbeResult result;

        public StubUnityProjectLockFileProbe (UnityProjectLockFileProbeResult result)
        {
            this.result = result;
        }

        public UnityProjectLockFileProbeResult Probe (string unityProjectRoot)
        {
            return result;
        }
    }
}
