using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Infrastructure.Storage;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonSessionIssuedAtValidationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenIssuedAtUtcIsMissing_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "missing-issued-at");
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(scope.FullPath, "fingerprint-missing-issued-at");
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        await File.WriteAllTextAsync(
            sessionPath,
            $$"""
            {
              "schemaVersion": {{DaemonSession.CurrentSchemaVersion}},
              "sessionToken": "token-1",
              "projectFingerprint": "fingerprint-missing-issued-at",
              "editorMode": "batchmode",
              "ownerKind": "cli",
              "canShutdownProcess": true,
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-daemon-test",
              "processId": 1234
            }
            """,
            CancellationToken.None);

        var readResult = await store.ReadAsync(scope.FullPath, "fingerprint-missing-issued-at", CancellationToken.None);

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
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var session = new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "token-1",
            ProjectFingerprint: "fingerprint-default-issued-at",
            IssuedAtUtc: default,
            EditorMode: "batchmode",
            OwnerKind: "cli",
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("issuedAtUtc", error.Message, StringComparison.Ordinal);
    }
}
