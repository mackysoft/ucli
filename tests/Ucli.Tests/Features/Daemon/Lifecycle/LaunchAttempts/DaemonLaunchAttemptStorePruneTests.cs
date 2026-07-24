using MackySoft.FileSystem;
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
            var id = CreateLaunchAttemptId(i + 1);
            var attempt = CreateAttempt(id, scope.FullPath, DaemonStartupStatus.Failed, minuteOffset: i);
            await WriteAttemptAsync(store, scope.FullPath, attempt);
            Directory.SetLastWriteTimeUtc(
                UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, id).Value,
                attempt.UpdatedAtUtc.UtcDateTime);
        }

        var pruneResult = await store.PruneAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, keepCount: 20, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint);
        Assert.Equal(20, Directory.EnumerateDirectories(attemptsDirectory.Value).Count());
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, CreateLaunchAttemptId(1)).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenOldAttemptDirectoryTimestampIsNewer_UsesPersistedUpdatedAtOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-stable-order");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
        var newer = CreateAttempt(CreateLaunchAttemptId(2), scope.FullPath, DaemonStartupStatus.Failed, minuteOffset: 1);
        await WriteAttemptAsync(store, scope.FullPath, newer);
        await WriteAttemptAsync(store, scope.FullPath, older);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, older.LaunchAttemptId).Value,
            newer.UpdatedAtUtc.AddMinutes(10).UtcDateTime);

        var pruneResult = await store.PruneAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, keepCount: 1, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, older.LaunchAttemptId).Value));
        Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, newer.LaunchAttemptId).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenOldStartupDiagnosisJsonIsInvalid_DeletesOldAttemptWithoutFailingRetention ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-invalid-json");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
        var newer = CreateAttempt(CreateLaunchAttemptId(2), scope.FullPath, DaemonStartupStatus.Failed, minuteOffset: 1);
        await WriteAttemptAsync(store, scope.FullPath, older);
        await WriteAttemptAsync(store, scope.FullPath, newer);
        await File.WriteAllTextAsync(older.ArtifactPath.Value, "{ invalid json", CancellationToken.None);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, older.LaunchAttemptId).Value,
            older.UpdatedAtUtc.UtcDateTime);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, newer.LaunchAttemptId).Value,
            newer.UpdatedAtUtc.UtcDateTime);

        var pruneResult = await store.PruneAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, keepCount: 1, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, older.LaunchAttemptId).Value));
        Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, newer.LaunchAttemptId).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenOldContractIdentifierDiffersFromDirectoryIdentifier_DeletesOldAttemptWithoutStoppingMaintenance ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-mismatched-id");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
        var newer = CreateAttempt(CreateLaunchAttemptId(2), scope.FullPath, DaemonStartupStatus.Failed, minuteOffset: 1);
        await WriteAttemptAsync(store, scope.FullPath, older);
        await WriteAttemptAsync(store, scope.FullPath, newer);
        var olderJson = await File.ReadAllTextAsync(older.ArtifactPath.Value, CancellationToken.None);
        await File.WriteAllTextAsync(
            older.ArtifactPath.Value,
            olderJson.Replace(
                older.LaunchAttemptId.ToString("D"),
                newer.LaunchAttemptId.ToString("D"),
                StringComparison.Ordinal),
            CancellationToken.None);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, older.LaunchAttemptId).Value,
            older.UpdatedAtUtc.UtcDateTime);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, newer.LaunchAttemptId).Value,
            newer.UpdatedAtUtc.UtcDateTime);

        var pruneResult = await store.PruneAsync(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprint,
            keepCount: 1,
            CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprint,
            older.LaunchAttemptId).Value));
        Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprint,
            newer.LaunchAttemptId).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenForeignDirectoryNameIsNotCanonicalGuid_PreservesForeignDirectoryAndDeletesOwnedAttempt ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-invalid-directory-id");
        var store = new DaemonLaunchAttemptStore();
        var validAttempt = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
        await WriteAttemptAsync(store, scope.FullPath, validAttempt);
        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint);
        var invalidAttemptDirectory = Path.Combine(attemptsDirectory.Value, "invalid-but-safe");
        Directory.CreateDirectory(invalidAttemptDirectory);
        Directory.SetLastWriteTimeUtc(invalidAttemptDirectory, validAttempt.UpdatedAtUtc.AddDays(-1).UtcDateTime);

        var pruneResult = await store.PruneAsync(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprint,
            keepCount: 0,
            CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        Assert.True(Directory.Exists(invalidAttemptDirectory));
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprint,
            validAttempt.LaunchAttemptId).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PruneAsync_WhenOwnedAttemptContainsForeignEntry_ReturnsFailureWithoutDeletingDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-foreign-entry");
        var store = new DaemonLaunchAttemptStore();
        var attempt = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
        await WriteAttemptAsync(store, scope.FullPath, attempt);
        var attemptDirectory = UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprint,
            attempt.LaunchAttemptId);
        var foreignPath = Path.Combine(attemptDirectory.Value, "foreign.txt");
        await File.WriteAllTextAsync(foreignPath, "foreign", CancellationToken.None);

        var pruneResult = await store.PruneAsync(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprint,
            keepCount: 0,
            CancellationToken.None);

        Assert.False(pruneResult.IsSuccess);
        Assert.True(Directory.Exists(attemptDirectory.Value));
        Assert.True(File.Exists(attempt.ArtifactPath.Value));
        Assert.True(File.Exists(foreignPath));
    }
}
