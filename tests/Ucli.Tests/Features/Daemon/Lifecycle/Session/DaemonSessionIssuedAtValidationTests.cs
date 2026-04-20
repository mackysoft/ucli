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

public sealed class DaemonSessionIssuedAtValidationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenIssuedAtUtcIsMissing_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "missing-issued-at");
        var store = new DaemonSessionStore();
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, "fingerprint-missing-issued-at");
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await File.WriteAllTextAsync(
            sessionPath,
            $$"""
            {
              "schemaVersion": {{DaemonSession.CurrentSchemaVersion}},
              "sessionToken": "token-1",
              "projectFingerprint": "fingerprint-missing-issued-at",
              "runtimeKind": "batchmode",
              "ownerKind": "cli",
              "canShutdownProcess": true,
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-daemon-test",
              "processId": 1234
            }
            """,
            CancellationToken.None);

        var readResult = await store.Read(scope.FullPath, "fingerprint-missing-issued-at", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("issuedAtUtc", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenIssuedAtUtcIsDefault_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "default-issued-at");
        var store = new DaemonSessionStore();
        var session = new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "token-1",
            ProjectFingerprint: "fingerprint-default-issued-at",
            IssuedAtUtc: default,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test",
            ProcessId: 1234,

            OwnerProcessId: 9876);

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("issuedAtUtc", error.Message, StringComparison.Ordinal);
    }
}