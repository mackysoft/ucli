using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Daemon;

using static DaemonLaunchAttemptStoreTestSupport;

public sealed class DaemonLaunchAttemptStoreReadTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadLastFailure_WhenFailureWasWritten_ReturnsStartupDiagnosisWithUnityLogPathReference ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "roundtrip");
        var store = new DaemonLaunchAttemptStore();
        var attempt = CreateAttempt(
            "20260312_000000Z_00000001",
            scope.FullPath,
            DaemonStartupStatus.Blocked);

        var writeResult = await store.WriteFailureAsync(scope.FullPath, ProjectFingerprint, attempt, CancellationToken.None);
        var readResult = await store.ReadLastFailureAsync(scope.FullPath, ProjectFingerprint, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.True(readResult.IsSuccess);
        var actual = Assert.IsType<DaemonLaunchAttempt>(readResult.LaunchAttempt);
        Assert.Equal(attempt.LaunchAttemptId, actual.LaunchAttemptId);
        Assert.Equal(DaemonStartupStatus.Blocked, actual.StartupStatus);
        Assert.Equal(attempt.UnityLogPath, actual.UnityLogPath);
        Assert.Equal(attempt.UnityLogPath, actual.Diagnosis.UnityLogPath);
        Assert.EndsWith(
            Path.Combine("launch-attempts", attempt.LaunchAttemptId, "startup-diagnosis.json"),
            actual.ArtifactPath,
            StringComparison.Ordinal);
        Assert.True(File.Exists(actual.ArtifactPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadLastFailure_WhenLatestAttemptIsCompleted_ReturnsLatestPublicFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "completed-hidden");
        var store = new DaemonLaunchAttemptStore();
        var failed = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, DaemonStartupStatus.Failed);
        var completed = CreateAttempt("20260312_000001Z_00000002", scope.FullPath, DaemonStartupStatus.Completed);

        await WriteAttemptAsync(store, scope.FullPath, failed);
        await WriteAttemptAsync(store, scope.FullPath, completed);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, failed.LaunchAttemptId),
            failed.UpdatedAtUtc.UtcDateTime);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, completed.LaunchAttemptId),
            completed.UpdatedAtUtc.UtcDateTime);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, ProjectFingerprint, CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal(failed.LaunchAttemptId, readResult.LaunchAttempt!.LaunchAttemptId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadLastFailure_WhenDirectoryTimestampIsNewer_UsesLaunchAttemptIdOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "stable-order");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, DaemonStartupStatus.Failed);
        var newer = CreateAttempt("20260312_000001Z_00000002", scope.FullPath, DaemonStartupStatus.Failed, minuteOffset: 1);

        await WriteAttemptAsync(store, scope.FullPath, newer);
        await WriteAttemptAsync(store, scope.FullPath, older);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, older.LaunchAttemptId),
            newer.UpdatedAtUtc.AddMinutes(10).UtcDateTime);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(scope.FullPath, ProjectFingerprint, newer.LaunchAttemptId),
            older.UpdatedAtUtc.UtcDateTime);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, ProjectFingerprint, CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal(newer.LaunchAttemptId, readResult.LaunchAttempt!.LaunchAttemptId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadLastFailure_WhenAttemptsShareSameSecond_UsesUpdatedAtOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "same-second-order");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt("20260312_000000Z_ffffffff", scope.FullPath, DaemonStartupStatus.Failed);
        var newer = CreateAttempt("20260312_000000Z_00000000", scope.FullPath, DaemonStartupStatus.Failed, minuteOffset: 1);

        await WriteAttemptAsync(store, scope.FullPath, older);
        await WriteAttemptAsync(store, scope.FullPath, newer);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, ProjectFingerprint, CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal(newer.LaunchAttemptId, readResult.LaunchAttempt!.LaunchAttemptId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadLastFailure_WhenStartupDiagnosisJsonIsInvalid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "invalid-json");
        var store = new DaemonLaunchAttemptStore();
        var attemptId = "20260312_000000Z_00000001";
        var diagnosisPath = UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
            scope.FullPath,
            ProjectFingerprint,
            attemptId);
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosisPath)!);
        await File.WriteAllTextAsync(diagnosisPath, "{ invalid json", CancellationToken.None);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, ProjectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    [Theory]
    [Trait("Size", "Medium")]
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
        var attempt = CreateAttempt("20260312_000000Z_00000001", scope.FullPath, DaemonStartupStatus.Failed);
        await WriteAttemptAsync(store, scope.FullPath, attempt);
        var json = await File.ReadAllTextAsync(attempt.ArtifactPath, CancellationToken.None);
        Assert.Contains(oldValue, json, StringComparison.Ordinal);
        await File.WriteAllTextAsync(attempt.ArtifactPath, json.Replace(oldValue, newValue, StringComparison.Ordinal), CancellationToken.None);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, ProjectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains(expectedMessage, error.Message, StringComparison.Ordinal);
    }
}
