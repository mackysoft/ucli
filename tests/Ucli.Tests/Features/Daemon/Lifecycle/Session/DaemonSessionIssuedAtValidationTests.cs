using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonSessionIssuedAtValidationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenIssuedAtUtcIsMissing_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-store", "missing-issued-at");
        var store = DaemonSessionStorageTestSupport.CreateStore();
        var sessionToken = IpcSessionTokenTestFactory.Create("missing-issued-at").GetEncodedValue();
        await DaemonSessionStorageTestSupport.WriteJsonAsync(
            scope.FullPath,
            "fingerprint-missing-issued-at",
            $$"""
            {
              "schemaVersion": {{DaemonSessionStorageContract.CurrentSchemaVersion}},
              "sessionToken": "{{sessionToken}}",
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
    public void Constructor_WhenIssuedAtUtcIsDefault_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() => DaemonSessionTestFactory.Create(
            projectFingerprint: "fingerprint-default-issued-at",
            issuedAtUtc: default(DateTimeOffset)));
    }
}
