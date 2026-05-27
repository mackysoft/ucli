using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonLaunchAttemptStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteFailureAndReadLastFailure_RoundTripsStartupDiagnosisWithUnityLogPathReference ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "roundtrip");
        var store = new DaemonLaunchAttemptStore();
        var attempt = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, startupStatus: DaemonStartupStatusValues.Blocked);

        var writeResult = await store.WriteFailureAsync(scope.FullPath, "fingerprint", attempt, CancellationToken.None);
        var readResult = await store.ReadLastFailureAsync(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.True(readResult.IsSuccess);
        var actual = Assert.IsType<DaemonLaunchAttempt>(readResult.LaunchAttempt);
        Assert.Equal(attempt.LaunchAttemptId, actual.LaunchAttemptId);
        Assert.Equal(DaemonStartupStatusValues.Blocked, actual.StartupStatus);
        Assert.Equal(attempt.UnityLogPath, actual.UnityLogPath);
        Assert.Equal(attempt.UnityLogPath, actual.Diagnosis.UnityLogPath);
        Assert.EndsWith(
            Path.Combine("launch-attempts", attempt.LaunchAttemptId, "startup-diagnosis.json"),
            actual.ArtifactPath,
            StringComparison.Ordinal);
        Assert.True(File.Exists(actual.ArtifactPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadLastFailure_WhenLatestAttemptIsCompleted_ReturnsLatestPublicFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "completed-hidden");
        var store = new DaemonLaunchAttemptStore();
        var failed = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed);
        var completed = CreateAttempt("20260312_000001Z_00000002", scope.FullPath, startupStatus: DaemonStartupStatusValues.Completed);

        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", failed, CancellationToken.None)).IsSuccess);
        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", completed, CancellationToken.None)).IsSuccess);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", failed.LaunchAttemptId),
            failed.UpdatedAtUtc.UtcDateTime);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", completed.LaunchAttemptId),
            completed.UpdatedAtUtc.UtcDateTime);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal(failed.LaunchAttemptId, readResult.LaunchAttempt!.LaunchAttemptId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadLastFailure_WhenDirectoryTimestampIsNewer_UsesLaunchAttemptIdOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "stable-order");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed);
        var newer = CreateAttempt("20260312_000001Z_00000002", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed, minuteOffset: 1);

        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", newer, CancellationToken.None)).IsSuccess);
        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", older, CancellationToken.None)).IsSuccess);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", older.LaunchAttemptId),
            newer.UpdatedAtUtc.AddMinutes(10).UtcDateTime);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", newer.LaunchAttemptId),
            older.UpdatedAtUtc.UtcDateTime);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal(newer.LaunchAttemptId, readResult.LaunchAttempt!.LaunchAttemptId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadLastFailure_WhenAttemptsShareSameSecond_UsesUpdatedAtOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "same-second-order");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt("20260312_000000Z_ffffffff", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed);
        var newer = CreateAttempt("20260312_000000Z_00000000", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed, minuteOffset: 1);

        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", older, CancellationToken.None)).IsSuccess);
        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", newer, CancellationToken.None)).IsSuccess);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal(newer.LaunchAttemptId, readResult.LaunchAttempt!.LaunchAttemptId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadLastFailure_WhenStartupDiagnosisJsonIsInvalid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "invalid-json");
        var store = new DaemonLaunchAttemptStore();
        var attemptId = "20260312_000000Z_00000001";
        var diagnosisPath = UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
            scope.FullPath,
            "fingerprint",
            attemptId);
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosisPath)!);
        await File.WriteAllTextAsync(diagnosisPath, "{ invalid json", CancellationToken.None);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
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
            "fingerprint",
            attemptId);
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosisPath)!);
        var targetPath = Path.Combine(targetScope.FullPath, "target.json");
        await File.WriteAllTextAsync(targetPath, "{}", CancellationToken.None);
        File.CreateSymbolicLink(diagnosisPath, targetPath);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.True(File.Exists(targetPath));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("\"startupBlockingReason\": \"unknown\"", "\"startupBlockingReason\": \"notSupported\"", "startupBlockingReason")]
    [InlineData("\"reportedBy\": \"cli\"", "\"reportedBy\": \"notSupported\"", "diagnosis.reportedBy")]
    [InlineData("\"startupPhase\": \"endpointRegistration\"", "\"startupPhase\": \"notSupported\"", "diagnosis.startupPhase")]
    [InlineData("\"actionRequired\": \"inspectUnityLog\"", "\"actionRequired\": \"notSupported\"", "diagnosis.actionRequired")]
    [InlineData("\"kind\": \"compiler\"", "\"kind\": \"notSupported\"", "diagnosis.primaryDiagnostic.kind")]
    public async Task ReadLastFailure_WhenContractVocabularyIsInvalid_ReturnsInvalidArgument (
        string oldValue,
        string newValue,
        string expectedMessage)
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "invalid-vocabulary");
        var store = new DaemonLaunchAttemptStore();
        var attempt = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed);
        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", attempt, CancellationToken.None)).IsSuccess);
        var json = await File.ReadAllTextAsync(attempt.ArtifactPath, CancellationToken.None);
        Assert.Contains(oldValue, json, StringComparison.Ordinal);
        await File.WriteAllTextAsync(attempt.ArtifactPath, json.Replace(oldValue, newValue, StringComparison.Ordinal), CancellationToken.None);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains(expectedMessage, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteFailure_WhenLaunchAttemptIdIsInvalid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "invalid-id");
        var store = new DaemonLaunchAttemptStore();
        var attempt = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed) with
        {
            LaunchAttemptId = " ",
        };

        var writeResult = await store.WriteFailureAsync(scope.FullPath, "fingerprint", attempt, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenMoreThanKeepCountAttemptsExist_DeletesOldAttempts ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention");
        var store = new DaemonLaunchAttemptStore();
        for (var i = 0; i < 21; i++)
        {
            var id = $"20260312_0000{i:00}Z_{i:00000000}";
            var attempt = CreateAttempt(id, scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed, minuteOffset: i);
            Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", attempt, CancellationToken.None)).IsSuccess);
            Directory.SetLastWriteTimeUtc(
                UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", id),
                attempt.UpdatedAtUtc.UtcDateTime);
        }

        var pruneResult = await store.PruneAsync(scope.FullPath, "fingerprint", keepCount: 20, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(scope.FullPath, "fingerprint");
        Assert.Equal(20, Directory.EnumerateDirectories(attemptsDirectory).Count());
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", "20260312_000000Z_00000000")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenOldAttemptDirectoryTimestampIsNewer_DeletesOldAttemptByLaunchAttemptIdOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-stable-order");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed);
        var newer = CreateAttempt("20260312_000001Z_00000002", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed, minuteOffset: 1);
        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", newer, CancellationToken.None)).IsSuccess);
        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", older, CancellationToken.None)).IsSuccess);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", older.LaunchAttemptId),
            newer.UpdatedAtUtc.AddMinutes(10).UtcDateTime);

        var pruneResult = await store.PruneAsync(scope.FullPath, "fingerprint", keepCount: 1, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", older.LaunchAttemptId)));
        Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", newer.LaunchAttemptId)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenAttemptsShareSameSecond_DeletesOldAttemptByUpdatedAtOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-same-second-order");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt("20260312_000000Z_ffffffff", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed);
        var newer = CreateAttempt("20260312_000000Z_00000000", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed, minuteOffset: 1);
        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", older, CancellationToken.None)).IsSuccess);
        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", newer, CancellationToken.None)).IsSuccess);

        var pruneResult = await store.PruneAsync(scope.FullPath, "fingerprint", keepCount: 1, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", older.LaunchAttemptId)));
        Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", newer.LaunchAttemptId)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenOldStartupDiagnosisJsonIsInvalid_DeletesOldAttemptWithoutFailingRetention ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-invalid-json");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed);
        var newer = CreateAttempt("20260312_000001Z_00000002", scope.FullPath, startupStatus: DaemonStartupStatusValues.Failed, minuteOffset: 1);
        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", older, CancellationToken.None)).IsSuccess);
        Assert.True((await store.WriteFailureAsync(scope.FullPath, "fingerprint", newer, CancellationToken.None)).IsSuccess);
        await File.WriteAllTextAsync(older.ArtifactPath, "{ invalid json", CancellationToken.None);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", older.LaunchAttemptId),
            older.UpdatedAtUtc.UtcDateTime);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", newer.LaunchAttemptId),
            newer.UpdatedAtUtc.UtcDateTime);

        var pruneResult = await store.PruneAsync(scope.FullPath, "fingerprint", keepCount: 1, CancellationToken.None);

        Assert.True(pruneResult.IsSuccess);
        Assert.Equal(1, pruneResult.DeletedCount);
        Assert.False(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", older.LaunchAttemptId)));
        Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, "fingerprint", newer.LaunchAttemptId)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenLaunchAttemptsDirectoryIsSymbolicLink_ReturnsFailureWithoutDeletingTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-symlink");
        using var targetScope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "retention-symlink-target");
        var store = new DaemonLaunchAttemptStore();
        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(scope.FullPath, "fingerprint");
        Directory.CreateDirectory(Path.GetDirectoryName(attemptsDirectory)!);
        var targetAttemptDirectory = Path.Combine(targetScope.FullPath, "20260312_000000Z_00000001");
        Directory.CreateDirectory(targetAttemptDirectory);
        Directory.CreateSymbolicLink(attemptsDirectory, targetScope.FullPath);

        var pruneResult = await store.PruneAsync(scope.FullPath, "fingerprint", keepCount: 0, CancellationToken.None);

        Assert.False(pruneResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(pruneResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.True(Directory.Exists(targetAttemptDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadLastFailure_WhenAttemptDirectoryIsSymbolicLink_ReturnsFailureWithoutReadingTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "read-attempt-symlink");
        using var targetScope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "read-attempt-symlink-target");
        var store = new DaemonLaunchAttemptStore();
        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(scope.FullPath, "fingerprint");
        Directory.CreateDirectory(attemptsDirectory);
        var targetAttemptDirectory = Path.Combine(targetScope.FullPath, "target-attempt");
        Directory.CreateDirectory(targetAttemptDirectory);
        var attemptDirectory = Path.Combine(attemptsDirectory, "20260312_000000Z_00000001");
        Directory.CreateSymbolicLink(attemptDirectory, targetAttemptDirectory);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.True(Directory.Exists(targetAttemptDirectory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PruneAsync_WhenAttemptDirectoryIsSymbolicLink_ReturnsFailureWithoutDeletingTarget ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "prune-attempt-symlink");
        using var targetScope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "prune-attempt-symlink-target");
        var store = new DaemonLaunchAttemptStore();
        var attemptsDirectory = UcliStoragePathResolver.ResolveLaunchAttemptsDirectory(scope.FullPath, "fingerprint");
        Directory.CreateDirectory(attemptsDirectory);
        var targetAttemptDirectory = Path.Combine(targetScope.FullPath, "target-attempt");
        Directory.CreateDirectory(targetAttemptDirectory);
        var attemptDirectory = Path.Combine(attemptsDirectory, "20260312_000000Z_00000001");
        Directory.CreateSymbolicLink(attemptDirectory, targetAttemptDirectory);

        var pruneResult = await store.PruneAsync(scope.FullPath, "fingerprint", keepCount: 0, CancellationToken.None);

        Assert.False(pruneResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(pruneResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.True(Directory.Exists(targetAttemptDirectory));
    }

    private static DaemonLaunchAttempt CreateAttempt (
        string launchAttemptId,
        string storageRoot,
        string startupStatus,
        int minuteOffset = 0)
    {
        var updatedAtUtc = new DateTimeOffset(2026, 03, 12, 0, minuteOffset, 0, TimeSpan.Zero);
        var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(storageRoot, "fingerprint");
        var diagnosis = new DaemonDiagnosis(
            Reason: DaemonDiagnosisReasonValues.StartupFailed,
            Message: "startup failed",
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: 1234,
            EditorInstancePath: null,
            SessionIssuedAtUtc: updatedAtUtc,
            ProcessStartedAtUtc: updatedAtUtc,
            UnityLogPath: unityLogPath,
            StartupPhase: DaemonDiagnosisStartupPhaseValues.EndpointRegistration,
            ActionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
            PrimaryDiagnostic: new DaemonPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
                Code: "CS0103",
                File: "Assets/Foo.cs",
                Line: 12,
                Column: 34,
                Message: "Missing type"));
        return new DaemonLaunchAttempt(
            LaunchAttemptId: launchAttemptId,
            StartedAtUtc: updatedAtUtc,
            UpdatedAtUtc: updatedAtUtc,
            StartupStatus: startupStatus,
            StartupBlockingReason: DaemonStartupBlockingReasonValues.Unknown,
            RetryDisposition: DaemonStartupRetryDispositionValues.Unknown,
            ProcessAction: DaemonStartupProcessActionValues.None,
            EditorMode: "gui",
            ProcessId: 1234,
            ProcessStartedAtUtc: updatedAtUtc,
            UnityLogPath: unityLogPath,
            ArtifactPath: UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(storageRoot, "fingerprint", launchAttemptId),
            Diagnosis: diagnosis);
    }
}
