using System.Text.Json.Nodes;
using MackySoft.FileSystem;
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

        var writeResult = await store.WriteFailureAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, attempt, CancellationToken.None);
        var readResult = await store.ReadLastFailureAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.True(readResult.IsSuccess);
        var actual = Assert.IsType<DaemonLaunchAttempt>(readResult.LaunchAttempt);
        Assert.Equal(attempt.LaunchAttemptId, actual.LaunchAttemptId);
        Assert.Equal(DaemonStartupStatus.Blocked, actual.StartupStatus);
        Assert.Equal(attempt.UnityLogPath, actual.UnityLogPath);
        Assert.Equal(attempt.UnityLogPath, actual.Diagnosis.UnityLogPath);
        Assert.Equal(
            UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
                AbsolutePath.Parse(scope.FullPath),
                ProjectFingerprint,
                attempt.LaunchAttemptId),
            actual.ArtifactPath);
        Assert.True(File.Exists(actual.ArtifactPath.Value));
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
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, failed.LaunchAttemptId).Value,
            failed.UpdatedAtUtc.UtcDateTime);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, completed.LaunchAttemptId).Value,
            completed.UpdatedAtUtc.UtcDateTime);

        var readResult = await store.ReadLastFailureAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, CancellationToken.None);

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
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, older.LaunchAttemptId).Value,
            newer.UpdatedAtUtc.AddMinutes(10).UtcDateTime);
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveLaunchAttemptDirectory(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, newer.LaunchAttemptId).Value,
            older.UpdatedAtUtc.UtcDateTime);

        var readResult = await store.ReadLastFailureAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, CancellationToken.None);

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
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprint,
            attemptId);
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosisPath.Value)!);
        await File.WriteAllTextAsync(diagnosisPath.Value, "{ invalid json", CancellationToken.None);

        var readResult = await store.ReadLastFailureAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadLastFailure_WhenUnityLogPathIsRelative_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-launch-attempt-store", "relative-unity-log");
        var store = new DaemonLaunchAttemptStore();
        var attempt = CreateAttempt(CreateLaunchAttemptId(1), scope.FullPath, DaemonStartupStatus.Failed);
        await WriteAttemptAsync(store, scope.FullPath, attempt);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(attempt.ArtifactPath.Value, CancellationToken.None))!.AsObject();
        json["unityLogPath"] = "relative/unity.log";
        await File.WriteAllTextAsync(attempt.ArtifactPath.Value, json.ToJsonString(), CancellationToken.None);

        var readResult = await store.ReadLastFailureAsync(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprint,
            CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, readResult.Error!.Kind);
        Assert.Contains("unityLogPath", readResult.Error.Message, StringComparison.Ordinal);
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
        var json = await File.ReadAllTextAsync(attempt.ArtifactPath.Value, CancellationToken.None);
        await File.WriteAllTextAsync(
            attempt.ArtifactPath.Value,
            json.Replace(
                attempt.LaunchAttemptId.ToString("D"),
                mismatchedId.ToString("D"),
                StringComparison.Ordinal),
            CancellationToken.None);

        var readResult = await store.ReadLastFailureAsync(
            AbsolutePath.Parse(scope.FullPath),
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
    [InlineData("\"startupBlockingReason\": \"unknown\"", "\"startupBlockingReason\": \" unknown \"", "startupBlockingReason")]
    [InlineData("\"editorMode\": \"gui\"", "\"editorMode\": \" gui \"", "editorMode")]
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
        var json = await File.ReadAllTextAsync(attempt.ArtifactPath.Value, CancellationToken.None);
        Assert.Contains(oldValue, json, StringComparison.Ordinal);
        await File.WriteAllTextAsync(attempt.ArtifactPath.Value, json.Replace(oldValue, newValue, StringComparison.Ordinal), CancellationToken.None);

        var readResult = await store.ReadLastFailureAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, CancellationToken.None);

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
        var root = JsonNode.Parse(await File.ReadAllTextAsync(attempt.ArtifactPath.Value, CancellationToken.None))!.AsObject();
        MutateInvalidField(root, field);
        await File.WriteAllTextAsync(attempt.ArtifactPath.Value, root.ToJsonString(), CancellationToken.None);

        var readResult = await store.ReadLastFailureAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint, CancellationToken.None);

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
