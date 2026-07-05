using MackySoft.Tests;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests;

public sealed class UnityProjectLockFileProbeTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Probe_WhenUnityLockFileDoesNotExist_ReturnsUnlocked ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-file-probe", "unlocked");
        var projectPath = scope.CreateDirectory("UnityProject");
        var probe = new UnityProjectLockFileProbe();

        var result = probe.Probe(projectPath);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsLocked);
        Assert.EndsWith(Path.Combine("Temp", "UnityLockfile"), result.LockFilePath, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Probe_WhenUnityLockFileExists_ReturnsLocked ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-file-probe", "locked");
        var projectPath = scope.CreateDirectory("UnityProject");
        var lockFilePath = scope.WriteFile(Path.Combine("UnityProject", "Temp", "UnityLockfile"), string.Empty);
        var probe = new UnityProjectLockFileProbe();

        var result = probe.Probe(projectPath);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsLocked);
        Assert.Equal(lockFilePath, result.LockFilePath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Probe_WhenUnityLockPathIsDirectory_ReturnsLocked ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-file-probe", "locked-directory");
        var projectPath = scope.CreateDirectory("UnityProject");
        var lockFilePath = scope.CreateDirectory(Path.Combine("UnityProject", "Temp", "UnityLockfile"));
        var probe = new UnityProjectLockFileProbe();

        var result = probe.Probe(projectPath);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsLocked);
        Assert.Equal(lockFilePath, result.LockFilePath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Probe_WhenUnityProjectRootPathIsInvalid_ReturnsFailure ()
    {
        var probe = new UnityProjectLockFileProbe();

        var result = probe.Probe("invalid\0project");

        Assert.False(result.IsSuccess);
        Assert.False(result.IsLocked);
        Assert.Contains("invalid", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
