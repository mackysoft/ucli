namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Foundation;

public sealed class DaemonDiagnosisStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteReadDelete_RoundTripsDiagnosisJson ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-diagnosis-store", "roundtrip");
        var store = new DaemonDiagnosisStore();
        var diagnosis = CreateDiagnosis(processId: 1234);

        var writeResult = await store.Write(scope.FullPath, "fingerprint-roundtrip", diagnosis, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.Null(writeResult.Error);

        var readResult = await store.Read(scope.FullPath, "fingerprint-roundtrip", CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.True(readResult.Exists);
        Assert.Equal(diagnosis, readResult.Diagnosis);

        var deleteResult = await store.Delete(scope.FullPath, "fingerprint-roundtrip", CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        var readAfterDeleteResult = await store.Read(scope.FullPath, "fingerprint-roundtrip", CancellationToken.None);
        Assert.True(readAfterDeleteResult.IsSuccess);
        Assert.False(readAfterDeleteResult.Exists);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenDiagnosisJsonIsMalformed_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-diagnosis-store", "malformed-json");
        var store = new DaemonDiagnosisStore();
        var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(scope.FullPath, "fingerprint-malformed");
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosisPath)!);
        await File.WriteAllTextAsync(diagnosisPath, "{", CancellationToken.None);

        var readResult = await store.Read(scope.FullPath, "fingerprint-malformed", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_WhenSessionIssuedAtUtcIsDefault_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-diagnosis-store", "invalid-session-issued-at");
        var store = new DaemonDiagnosisStore();
        var diagnosis = CreateDiagnosis(processId: 1234) with
        {
            SessionIssuedAtUtc = default,
        };

        var writeResult = await store.Write(scope.FullPath, "fingerprint-invalid", diagnosis, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("sessionIssuedAtUtc", error.Message, StringComparison.Ordinal);
    }

    private static DaemonDiagnosis CreateDiagnosis (int? processId)
    {
        return new DaemonDiagnosis(
            Reason: DaemonDiagnosisReasonValues.ShutdownRequested,
            Message: "daemon shutdown completed",
            ReportedBy: DaemonDiagnosisReportedByValues.Unity,
            IsInferred: false,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            ProcessId: processId,
            SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero));
    }
}