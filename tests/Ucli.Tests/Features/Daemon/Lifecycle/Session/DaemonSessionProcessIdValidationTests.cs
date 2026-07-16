using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonSessionProcessIdValidationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenProcessIdIsNotPositive_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "invalid-process-id-read");
        var store = DaemonSessionStorageTestSupport.CreateStore();
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-invalid-process-id-read");
        var sessionToken = IpcSessionTokenTestFactory.Create("invalid-process-id-read").GetEncodedValue();
        await DaemonSessionStorageTestSupport.WriteJsonAsync(
            scope.FullPath,
            projectFingerprint,
            $$"""
            {
              "schemaVersion": {{DaemonSessionStorageContract.CurrentSchemaVersion}},
              "sessionGenerationId": "11111111-1111-1111-1111-111111111111",
              "sessionToken": "{{sessionToken}}",
              "projectFingerprint": "{{projectFingerprint}}",
              "issuedAtUtc": "2026-01-01T00:00:00+00:00",
              "editorMode": "batchmode",
              "ownerKind": "cli",
              "canShutdownProcess": true,
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-daemon-test",
              "processId": 0,
              "ownerProcessId": 9876
            }
            """,
            CancellationToken.None);

        var readResult = await store.ReadAsync(scope.FullPath, projectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("processId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenProcessStartedAtUtcIsMissingWithProcessId_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "missing-process-started-at-read");
        var store = DaemonSessionStorageTestSupport.CreateStore();
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-missing-process-started-at-read");
        var sessionToken = IpcSessionTokenTestFactory.Create("missing-process-started-at-read").GetEncodedValue();
        await DaemonSessionStorageTestSupport.WriteJsonAsync(
            scope.FullPath,
            projectFingerprint,
            $$"""
            {
              "schemaVersion": {{DaemonSessionStorageContract.CurrentSchemaVersion}},
              "sessionGenerationId": "11111111-1111-1111-1111-111111111111",
              "sessionToken": "{{sessionToken}}",
              "projectFingerprint": "{{projectFingerprint}}",
              "issuedAtUtc": "2026-01-01T00:00:00+00:00",
              "editorMode": "batchmode",
              "ownerKind": "cli",
              "canShutdownProcess": true,
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-daemon-test",
              "processId": 1234,
              "ownerProcessId": 9876
            }
            """,
            CancellationToken.None);

        var readResult = await store.ReadAsync(scope.FullPath, projectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("processStartedAtUtc", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Size", "Small")]
    public void Constructor_WhenProcessIdIsNotPositive_ThrowsArgumentOutOfRangeException (int processId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DaemonSessionTestFactory.Create(
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-invalid-process-id-write"),
            processId: processId));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenProcessStartedAtUtcIsMissingWithProcessId_ThrowsArgumentException ()
    {
        var validSession = DaemonSessionTestFactory.Create();

        Assert.Throws<ArgumentException>(() => new DaemonSession(
            validSession.SessionGenerationId,
            validSession.SessionToken,
            ProjectFingerprintTestFactory.Create("fingerprint-missing-process-started-at-write"),
            validSession.IssuedAtUtc,
            validSession.EditorMode,
            validSession.OwnerKind,
            validSession.CanShutdownProcess,
            validSession.Endpoint,
            processId: 1234,
            processStartedAtUtc: null,
            ownerProcessId: validSession.OwnerProcessId,
            editorInstanceId: validSession.EditorInstanceId));
    }
}
