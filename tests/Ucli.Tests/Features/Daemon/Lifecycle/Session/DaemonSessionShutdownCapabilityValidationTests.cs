using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonSessionShutdownCapabilityValidationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenCanShutdownProcessIsMissing_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "missing-can-shutdown-process");
        var store = DaemonSessionStorageTestSupport.CreateStore();
        var projectFingerprint = "fingerprint-missing-can-shutdown-process";
        await DaemonSessionStorageTestSupport.WriteJsonAsync(
            scope.FullPath,
            projectFingerprint,
            $$"""
            {
              "schemaVersion": {{DaemonSession.CurrentSchemaVersion}},
              "sessionToken": "token-1",
              "projectFingerprint": "{{projectFingerprint}}",
              "issuedAtUtc": "2026-01-01T00:00:00+00:00",
              "editorMode": "batchmode",
              "ownerKind": "cli",
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-daemon-test",
              "processId": 1234,
              "processStartedAtUtc": "2026-01-01T00:00:01+00:00",
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
        Assert.Contains("canShutdownProcess", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenCanShutdownProcessIsFalse_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "can-shutdown-process-false");
        var store = DaemonSessionStorageTestSupport.CreateStore();
        var session = DaemonSessionTestFactory.Create() with
        {
            ProjectFingerprint = "fingerprint-can-shutdown-process-false",
            CanShutdownProcess = false,
        };

        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("canShutdownProcess", error.Message, StringComparison.Ordinal);
    }
}
