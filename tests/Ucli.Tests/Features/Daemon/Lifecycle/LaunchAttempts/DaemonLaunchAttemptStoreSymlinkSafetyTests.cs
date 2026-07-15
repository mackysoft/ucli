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
        var attemptId = CreateLaunchAttemptId(1);
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
        var targetAttemptDirectory = Path.Combine(targetScope.FullPath, "target-attempt");
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
        var attemptDirectory = UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            scope.FullPath,
            ProjectFingerprint,
            CreateLaunchAttemptId(1));
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
        var attemptDirectory = UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            scope.FullPath,
            ProjectFingerprint,
            CreateLaunchAttemptId(1));
        Directory.CreateSymbolicLink(attemptDirectory, targetAttemptDirectory);

        var pruneResult = await store.PruneAsync(scope.FullPath, ProjectFingerprint, keepCount: 0, CancellationToken.None);

        Assert.False(pruneResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(pruneResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.True(Directory.Exists(targetAttemptDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenStartupDiagnosisJsonIsSymbolicLink_ReturnsFailureWithoutDeletingTargetOrLink ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "prune-file-symlink");
        using var targetScope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "prune-file-symlink-target");
        var store = new DaemonLaunchAttemptStore();
        var attemptId = CreateLaunchAttemptId(1);
        var diagnosisPath = UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
            scope.FullPath,
            ProjectFingerprint,
            attemptId);
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosisPath)!);
        var targetPath = Path.Combine(targetScope.FullPath, "target.json");
        await File.WriteAllTextAsync(targetPath, "{}", CancellationToken.None);
        File.CreateSymbolicLink(diagnosisPath, targetPath);

        var pruneResult = await store.PruneAsync(scope.FullPath, ProjectFingerprint, keepCount: 0, CancellationToken.None);

        Assert.False(pruneResult.IsSuccess);
        Assert.True(File.Exists(targetPath));
        Assert.True(File.Exists(diagnosisPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenForeignDirectoryIsSymbolicLink_PreservesLinkAndDeletesOwnedAttempt ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "prune-foreign-symlink");
        using var targetScope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "prune-foreign-symlink-target");
        var store = new DaemonLaunchAttemptStore();
        var attempt = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
        await WriteAttemptAsync(store, scope.FullPath, attempt);
        var targetMarkerPath = Path.Combine(targetScope.FullPath, "marker.txt");
        await File.WriteAllTextAsync(targetMarkerPath, "foreign", CancellationToken.None);
        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(scope.FullPath, ProjectFingerprint);
        var foreignDirectory = Path.Combine(attemptsDirectory, "foreign-link");
        Directory.CreateSymbolicLink(foreignDirectory, targetScope.FullPath);

        var pruneResult = await store.PruneAsync(scope.FullPath, ProjectFingerprint, keepCount: 0, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            scope.FullPath,
            ProjectFingerprint,
            attempt.LaunchAttemptId)));
        Assert.True(Directory.Exists(foreignDirectory));
        Assert.True(File.Exists(targetMarkerPath));
    }
}
