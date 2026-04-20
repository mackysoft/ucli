namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Shared.Foundation;

public sealed class DaemonSessionProcessIdValidationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenProcessIdIsNotPositive_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "invalid-process-id-read");
        var store = new DaemonSessionStore();
        var projectFingerprint = "fingerprint-invalid-process-id-read";
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await File.WriteAllTextAsync(
            sessionPath,
            $$"""
            {
              "schemaVersion": {{DaemonSession.CurrentSchemaVersion}},
              "sessionToken": "token-1",
              "projectFingerprint": "{{projectFingerprint}}",
              "issuedAtUtc": "2026-01-01T00:00:00+00:00",
              "runtimeKind": "batchmode",
              "ownerKind": "cli",
              "canShutdownProcess": true,
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-daemon-test",
              "processId": 0
            }
            """,
            CancellationToken.None);

        var readResult = await store.Read(scope.FullPath, projectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("processId", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Size", "Small")]
    public async Task Write_WhenProcessIdIsNotPositive_ReturnsInvalidArgument (int processId)
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "invalid-process-id-write");
        var store = new DaemonSessionStore();
        var session = new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "token-1",
            ProjectFingerprint: "fingerprint-invalid-process-id-write",
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test",
            ProcessId: processId,

            OwnerProcessId: 9876);

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("processId", error.Message, StringComparison.Ordinal);
    }
}