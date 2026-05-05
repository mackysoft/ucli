using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Infrastructure.Storage;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

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
