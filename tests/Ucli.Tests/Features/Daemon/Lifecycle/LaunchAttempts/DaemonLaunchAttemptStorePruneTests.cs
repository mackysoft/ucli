using MackySoft.Tests;
using MackySoft.Ucli.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Daemon;

using static DaemonLaunchAttemptStoreTestSupport;

public sealed class DaemonLaunchAttemptStorePruneTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenMoreThanKeepCountAttemptsExist_DeletesOldAttempts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention");
        var store = new DaemonLaunchAttemptStore();
        for (var i = 0; i < 21; i++)
        {
            var id = $"20260312_0000{i:00}Z_{i:00000000}";
            var attempt = CreateAttempt(id, scope.FullPath, DaemonStartupStatus.Failed, minuteOffset: i);
            await WriteAttemptAsync(store, scope.FullPath, attempt);
            Directory.SetLastWriteTimeUtc(
                UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, id),
                attempt.UpdatedAtUtc.UtcDateTime);
        }

        var pruneResult = await store.PruneAsync(scope.FullPath, ProjectFingerprint, keepCount: 20, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(scope.FullPath, ProjectFingerprint);
        Assert.Equal(20, Directory.EnumerateDirectories(attemptsDirectory).Count());
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, "20260312_000000Z_00000000")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenOldAttemptDirectoryTimestampIsNewer_DeletesOldAttemptByLaunchAttemptIdOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-stable-order");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, DaemonStartupStatus.Failed);
        var newer = CreateAttempt("20260312_000001Z_00000002", scope.FullPath, DaemonStartupStatus.Failed, minuteOffset: 1);
        await WriteAttemptAsync(store, scope.FullPath, newer);
        await WriteAttemptAsync(store, scope.FullPath, older);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, older.LaunchAttemptId),
            newer.UpdatedAtUtc.AddMinutes(10).UtcDateTime);

        var pruneResult = await store.PruneAsync(scope.FullPath, ProjectFingerprint, keepCount: 1, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, older.LaunchAttemptId)));
        Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, newer.LaunchAttemptId)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenAttemptsShareSameSecond_DeletesOldAttemptByUpdatedAtOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-same-second-order");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt("20260312_000000Z_ffffffff", scope.FullPath, DaemonStartupStatus.Failed);
        var newer = CreateAttempt("20260312_000000Z_00000000", scope.FullPath, DaemonStartupStatus.Failed, minuteOffset: 1);
        await WriteAttemptAsync(store, scope.FullPath, older);
        await WriteAttemptAsync(store, scope.FullPath, newer);

        var pruneResult = await store.PruneAsync(scope.FullPath, ProjectFingerprint, keepCount: 1, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, older.LaunchAttemptId)));
        Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, newer.LaunchAttemptId)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenOldStartupDiagnosisJsonIsInvalid_DeletesOldAttemptWithoutFailingRetention ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-invalid-json");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, DaemonStartupStatus.Failed);
        var newer = CreateAttempt("20260312_000001Z_00000002", scope.FullPath, DaemonStartupStatus.Failed, minuteOffset: 1);
        await WriteAttemptAsync(store, scope.FullPath, older);
        await WriteAttemptAsync(store, scope.FullPath, newer);
        await File.WriteAllTextAsync(older.ArtifactPath, "{ invalid json", CancellationToken.None);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, older.LaunchAttemptId),
            older.UpdatedAtUtc.UtcDateTime);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, newer.LaunchAttemptId),
            newer.UpdatedAtUtc.UtcDateTime);

        var pruneResult = await store.PruneAsync(scope.FullPath, ProjectFingerprint, keepCount: 1, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, older.LaunchAttemptId)));
        Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, newer.LaunchAttemptId)));
    }
}
