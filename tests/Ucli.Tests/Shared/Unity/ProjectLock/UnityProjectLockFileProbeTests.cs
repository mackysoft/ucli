using MackySoft.FileSystem;
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

        var result = probe.Probe(AbsolutePath.Parse(projectPath));

        Assert.True(result.IsSuccess);
        Assert.False(result.IsLocked);
        Assert.EndsWith(Path.Combine("Temp", "UnityLockfile"), result.LockFilePath!.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Probe_WhenUnityLockFileExists_ReturnsLocked ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-file-probe", "locked");
        var projectPath = scope.CreateDirectory("UnityProject");
        var lockFilePath = scope.WriteFile(Path.Combine("UnityProject", "Temp", "UnityLockfile"), string.Empty);
        var probe = new UnityProjectLockFileProbe();

        var result = probe.Probe(AbsolutePath.Parse(projectPath));

        Assert.True(result.IsSuccess);
        Assert.True(result.IsLocked);
        Assert.Equal(AbsolutePath.Parse(lockFilePath), result.LockFilePath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Probe_WhenUnityLockPathIsDirectory_ReturnsLocked ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-lock-file-probe", "locked-directory");
        var projectPath = scope.CreateDirectory("UnityProject");
        var lockFilePath = scope.CreateDirectory(Path.Combine("UnityProject", "Temp", "UnityLockfile"));
        var probe = new UnityProjectLockFileProbe();

        var result = probe.Probe(AbsolutePath.Parse(projectPath));

        Assert.True(result.IsSuccess);
        Assert.True(result.IsLocked);
        Assert.Equal(AbsolutePath.Parse(lockFilePath), result.LockFilePath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Probe_WhenUnityProjectRootIsNull_ThrowsArgumentNullException ()
    {
        var probe = new UnityProjectLockFileProbe();

        var exception = Assert.Throws<ArgumentNullException>(() => probe.Probe(null!));

        Assert.Equal("unityProjectRoot", exception.ParamName);
    }
}
