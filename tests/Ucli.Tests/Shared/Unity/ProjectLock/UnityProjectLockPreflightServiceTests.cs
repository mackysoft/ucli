using MackySoft.Tests;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Unity;

namespace MackySoft.Ucli.Tests;

public sealed class UnityProjectLockPreflightServiceTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Prepare_WhenLockFileDoesNotExist_ReturnsUnlocked ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "unlocked");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope);
        var service = new UnityProjectLockPreflightService(
            new UnityProjectLockFileProbe(),
            new StubUnityProjectLockOwnerProbe(UnityProjectLockOwnerProbeResult.NoOwner()),
            new UnityProjectLockFileCleaner());

        var result = await service.PrepareForUnityProcessStartAsync(unityProject, CancellationToken.None);

        Assert.Equal(UnityProjectLockPreflightStatus.Unlocked, result.Status);
        Assert.False(File.Exists(scope.GetPath("UnityProject/Temp/UnityLockfile")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Prepare_WhenOwnerIsActive_ReturnsActiveLockWithoutDeletingLockFile ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "active");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope);
        var lockFilePath = scope.WriteFile("UnityProject/Temp/UnityLockfile", string.Empty);
        var service = new UnityProjectLockPreflightService(
            new UnityProjectLockFileProbe(),
            new StubUnityProjectLockOwnerProbe(UnityProjectLockOwnerProbeResult.ActiveOwner(
                UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProject.UnityProjectRoot, lockFilePath))),
            new UnexpectedUnityProjectLockFileCleaner());

        var result = await service.PrepareForUnityProcessStartAsync(unityProject, CancellationToken.None);

        Assert.Equal(UnityProjectLockPreflightStatus.ActiveLock, result.Status);
        Assert.True(File.Exists(lockFilePath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Prepare_WhenOwnerIsAbsent_DeletesStaleLockFile ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "stale");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope);
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
    [Trait("Size", "Medium")]
    public async Task Prepare_WhenOwnershipIsAmbiguous_ReturnsAmbiguousWithoutDeletingLockFile ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "ambiguous");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope);
        var lockFilePath = scope.WriteFile("UnityProject/Temp/UnityLockfile", string.Empty);
        var service = new UnityProjectLockPreflightService(
            new UnityProjectLockFileProbe(),
            new StubUnityProjectLockOwnerProbe(UnityProjectLockOwnerProbeResult.Ambiguous("process scan failed")),
            new UnexpectedUnityProjectLockFileCleaner());

        var result = await service.PrepareForUnityProcessStartAsync(unityProject, CancellationToken.None);

        Assert.Equal(UnityProjectLockPreflightStatus.Ambiguous, result.Status);
        Assert.True(File.Exists(lockFilePath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Prepare_WhenStaleLockCleanupFails_ReturnsCleanupFailedWithoutDeletingLockFile ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "cleanup-failed");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope);
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
    [Trait("Size", "Medium")]
    public async Task Prepare_WhenLockInspectionFails_ReturnsInspectionFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-preflight", "inspection-failed");
        var service = new UnityProjectLockPreflightService(
            new StubUnityProjectLockFileProbe(UnityProjectLockFileProbeResult.Failure("probe failed")),
            new StubUnityProjectLockOwnerProbe(UnityProjectLockOwnerProbeResult.NoOwner()),
            new UnityProjectLockFileCleaner());

        var result = await service.PrepareForUnityProcessStartAsync(ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope), CancellationToken.None);

        Assert.Equal(UnityProjectLockPreflightStatus.InspectionFailed, result.Status);
        Assert.Contains("probe failed", result.Message, StringComparison.Ordinal);
    }

}
