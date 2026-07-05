using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Daemon;

using static DaemonLaunchAttemptStoreTestSupport;

public sealed class DaemonLaunchAttemptStoreSymlinkSafetyTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadLastFailure_WhenStartupDiagnosisJsonIsSymbolicLink_ReturnsFailureWithoutReadingTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "read-file-symlink");
        using var targetScope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "read-file-symlink-target");
        var store = new DaemonLaunchAttemptStore();
        var attemptId = "20260312_000000Z_00000001";
        var diagnosisPath = UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
            scope.FullPath,
            ProjectFingerprint,
            attemptId);
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosisPath)!);
        var targetPath = Path.Combine(targetScope.FullPath, "target.json");
        await File.WriteAllTextAsync(targetPath, "{}", CancellationToken.None);
        File.CreateSymbolicLink(diagnosisPath, targetPath);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, ProjectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.True(File.Exists(targetPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenLaunchAttemptsDirectoryIsSymbolicLink_ReturnsFailureWithoutDeletingTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-symlink");
        using var targetScope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-symlink-target");
        var store = new DaemonLaunchAttemptStore();
        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(scope.FullPath, ProjectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(attemptsDirectory)!);
        var targetAttemptDirectory = Path.Combine(targetScope.FullPath, "20260312_000000Z_00000001");
        Directory.CreateDirectory(targetAttemptDirectory);
        Directory.CreateSymbolicLink(attemptsDirectory, targetScope.FullPath);

        var pruneResult = await store.PruneAsync(scope.FullPath, ProjectFingerprint, keepCount: 0, CancellationToken.None);

        Assert.False(pruneResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(pruneResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.True(Directory.Exists(targetAttemptDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadLastFailure_WhenAttemptDirectoryIsSymbolicLink_ReturnsFailureWithoutReadingTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "read-attempt-symlink");
        using var targetScope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "read-attempt-symlink-target");
        var store = new DaemonLaunchAttemptStore();
        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(scope.FullPath, ProjectFingerprint);
        Directory.CreateDirectory(attemptsDirectory);
        var targetAttemptDirectory = Path.Combine(targetScope.FullPath, "target-attempt");
        Directory.CreateDirectory(targetAttemptDirectory);
        var attemptDirectory = Path.Combine(attemptsDirectory, "20260312_000000Z_00000001");
        Directory.CreateSymbolicLink(attemptDirectory, targetAttemptDirectory);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, ProjectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.True(Directory.Exists(targetAttemptDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenAttemptDirectoryIsSymbolicLink_ReturnsFailureWithoutDeletingTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "prune-attempt-symlink");
        using var targetScope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "prune-attempt-symlink-target");
        var store = new DaemonLaunchAttemptStore();
        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(scope.FullPath, ProjectFingerprint);
        Directory.CreateDirectory(attemptsDirectory);
        var targetAttemptDirectory = Path.Combine(targetScope.FullPath, "target-attempt");
        Directory.CreateDirectory(targetAttemptDirectory);
        var attemptDirectory = Path.Combine(attemptsDirectory, "20260312_000000Z_00000001");
        Directory.CreateSymbolicLink(attemptDirectory, targetAttemptDirectory);

        var pruneResult = await store.PruneAsync(scope.FullPath, ProjectFingerprint, keepCount: 0, CancellationToken.None);

        Assert.False(pruneResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(pruneResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.True(Directory.Exists(targetAttemptDirectory));
    }
}
