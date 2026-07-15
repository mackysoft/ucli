using System.Text.Json.Nodes;
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
            CreateLaunchAttemptId(1),
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
            Path.Combine("launch-attempts", attempt.LaunchAttemptId.ToString("N"), "startup-diagnosis.json"),
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
        var failed = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
        var completed = CreateAttempt(CreateLaunchAttemptId(2), scope.FullPath, DaemonStartupStatus.Completed);

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
    public async Task ReadLastFailure_WhenDirectoryTimestampIsNewer_UsesPersistedUpdatedAtOrder ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "stable-order");
        var store = new DaemonLaunchAttemptStore();
        var older = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
        var newer = CreateAttempt(CreateLaunchAttemptId(2), scope.FullPath, DaemonStartupStatus.Failed, minuteOffset: 1);

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
    public async Task ReadLastFailure_WhenStartupDiagnosisJsonIsInvalid_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "invalid-json");
        var store = new DaemonLaunchAttemptStore();
        var attemptId = CreateLaunchAttemptId(1);
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

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadLastFailure_WhenContractIdentifierDiffersFromDirectoryIdentifier_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "mismatched-id");
        var store = new DaemonLaunchAttemptStore();
        var attempt = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
        await WriteAttemptAsync(store, scope.FullPath, attempt);
        var mismatchedId = CreateLaunchAttemptId(2);
        var json = await File.ReadAllTextAsync(attempt.ArtifactPath, CancellationToken.None);
        await File.WriteAllTextAsync(
            attempt.ArtifactPath,
            json.Replace(
                attempt.LaunchAttemptId.ToString("D"),
                mismatchedId.ToString("D"),
                StringComparison.Ordinal),
            CancellationToken.None);

        var readResult = await store.ReadLastFailureAsync(
            scope.FullPath,
            ProjectFingerprint,
            CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("does not match its directory", error.Message, StringComparison.Ordinal);
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
        var attempt = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
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

    [Theory]
    [InlineData(InvalidPersistedField.ProcessId, "processId")]
    [InlineData(InvalidPersistedField.StartedAtOffset, "startedAtUtc")]
    [InlineData(InvalidPersistedField.UpdatedAtOffset, "updatedAtUtc")]
    [InlineData(InvalidPersistedField.ProcessStartedAtOffset, "processStartedAtUtc")]
    [InlineData(InvalidPersistedField.UpdatedBeforeStarted, "updatedAtUtc")]
    [InlineData(InvalidPersistedField.ProcessStartedAfterUpdated, "processStartedAtUtc")]
    [Trait("Size", "Medium")]
    public async Task ReadLastFailure_WhenPersistedRuntimeInvariantIsInvalid_ReturnsInvalidArgument (
        InvalidPersistedField field,
        string expectedMessage)
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "invalid-runtime-invariant");
        var store = new DaemonLaunchAttemptStore();
        var attempt = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
        await WriteAttemptAsync(store, scope.FullPath, attempt);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(attempt.ArtifactPath, CancellationToken.None))!.AsObject();
        MutateInvalidField(root, field);
        await File.WriteAllTextAsync(attempt.ArtifactPath, root.ToJsonString(), CancellationToken.None);

        var readResult = await store.ReadLastFailureAsync(scope.FullPath, ProjectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains(expectedMessage, error.Message, StringComparison.Ordinal);
    }

    private static void MutateInvalidField (JsonObject root, InvalidPersistedField field)
    {
        var nonUtcTimestamp = new DateTimeOffset(2026, 3, 12, 9, 0, 0, TimeSpan.FromHours(9));
        switch (field)
        {
            case InvalidPersistedField.ProcessId:
                root["processId"] = 0;
                return;
            case InvalidPersistedField.StartedAtOffset:
                root["startedAtUtc"] = JsonValue.Create(nonUtcTimestamp);
                return;
            case InvalidPersistedField.UpdatedAtOffset:
                root["updatedAtUtc"] = JsonValue.Create(nonUtcTimestamp);
                return;
            case InvalidPersistedField.ProcessStartedAtOffset:
                root["processStartedAtUtc"] = JsonValue.Create(nonUtcTimestamp);
                return;
            case InvalidPersistedField.UpdatedBeforeStarted:
                root["updatedAtUtc"] = JsonValue.Create(
                    new DateTimeOffset(2026, 3, 11, 23, 59, 59, TimeSpan.Zero));
                return;
            case InvalidPersistedField.ProcessStartedAfterUpdated:
                root["processStartedAtUtc"] = JsonValue.Create(
                    new DateTimeOffset(2026, 3, 12, 0, 0, 1, TimeSpan.Zero));
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }
    }

    public enum InvalidPersistedField
    {
        ProcessId,
        StartedAtOffset,
        UpdatedAtOffset,
        ProcessStartedAtOffset,
        UpdatedBeforeStarted,
        ProcessStartedAfterUpdated,
    }
}
