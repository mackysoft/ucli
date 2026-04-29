using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Shared.Foundation;

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
