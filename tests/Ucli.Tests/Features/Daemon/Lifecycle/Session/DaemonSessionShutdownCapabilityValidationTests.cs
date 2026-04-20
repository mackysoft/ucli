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

public sealed class DaemonSessionShutdownCapabilityValidationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenCanShutdownProcessIsMissing_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "missing-can-shutdown-process");
        var store = new DaemonSessionStore();
        var projectFingerprint = "fingerprint-missing-can-shutdown-process";
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
              "ownerKind": "supervisor",
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-daemon-test",
              "processId": 1234,
              "ownerProcessId": 9876
            }
            """,
            CancellationToken.None);

        var readResult = await store.Read(scope.FullPath, projectFingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        Assert.Equal(DaemonSessionReadFailureKind.InvalidSession, readResult.FailureKind);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("canShutdownProcess", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenCanShutdownProcessIsFalse_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "can-shutdown-process-false");
        var store = new DaemonSessionStore();
        var session = new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "token-1",
            ProjectFingerprint: "fingerprint-can-shutdown-process-false",
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: false,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test",
            ProcessId: 1234,

            OwnerProcessId: 9876);

        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("canShutdownProcess", error.Message, StringComparison.Ordinal);
    }
}