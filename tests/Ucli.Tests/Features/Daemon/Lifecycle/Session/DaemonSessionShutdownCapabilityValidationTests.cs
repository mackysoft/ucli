using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
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
        var sessionToken = IpcSessionTokenTestFactory.Create("missing-can-shutdown-process").GetEncodedValue();
        await DaemonSessionStorageTestSupport.WriteJsonAsync(
            scope.FullPath,
            projectFingerprint,
            $$"""
            {
              "schemaVersion": {{DaemonSessionStorageContract.CurrentSchemaVersion}},
              "sessionToken": "{{sessionToken}}",
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
    [Trait("Size", "Small")]
    public void Constructor_WhenBatchmodeCannotShutdownProcess_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() => DaemonSessionTestFactory.Create(
            projectFingerprint: "fingerprint-can-shutdown-process-false",
            canShutdownProcess: false));
    }
}
