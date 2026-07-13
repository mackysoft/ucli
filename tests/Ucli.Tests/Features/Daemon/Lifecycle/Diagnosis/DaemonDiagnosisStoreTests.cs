using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Infrastructure.Storage;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

public sealed class DaemonDiagnosisStoreTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteReadDelete_RoundTripsDiagnosisJson ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-diagnosis-store", "roundtrip");
        var store = new DaemonDiagnosisStore();
        var diagnosis = CreateDiagnosis(processId: 1234);

        var writeResult = await store.WriteAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-roundtrip"), diagnosis, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.Null(writeResult.Error);

        var readResult = await store.ReadAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-roundtrip"), CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.True(readResult.Exists);
        Assert.Equal(diagnosis, readResult.Diagnosis);

        var deleteResult = await store.DeleteAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-roundtrip"), CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        var readAfterDeleteResult = await store.ReadAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-roundtrip"), CancellationToken.None);
        Assert.True(readAfterDeleteResult.IsSuccess);
        Assert.False(readAfterDeleteResult.Exists);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenDiagnosisJsonIsMalformed_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-diagnosis-store", "malformed-json");
        var store = new DaemonDiagnosisStore();
        var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-malformed"));
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosisPath)!);
        await File.WriteAllTextAsync(diagnosisPath, "{", CancellationToken.None);

        var readResult = await store.ReadAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-malformed"), CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenSessionIssuedAtUtcIsDefault_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-diagnosis-store", "invalid-session-issued-at");
        var store = new DaemonDiagnosisStore();
        var diagnosis = CreateDiagnosis(processId: 1234) with
        {
            SessionIssuedAtUtc = default,
        };

        var writeResult = await store.WriteAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-invalid"), diagnosis, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("sessionIssuedAtUtc", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Write_WhenStartupPhaseIsUnknown_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-diagnosis-store", "invalid-startup-phase");
        var store = new DaemonDiagnosisStore();
        var diagnosis = CreateDiagnosis(processId: 1234) with
        {
            StartupPhase = (DaemonDiagnosisStartupPhase)int.MaxValue,
        };

        var writeResult = await store.WriteAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-invalid"), diagnosis, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("startupPhase", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenPrimaryDiagnosticKindIsUnknown_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-diagnosis-store", "invalid-primary-diagnostic-kind");
        var store = new DaemonDiagnosisStore();
        var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-invalid"));
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosisPath)!);
        var contract = new DaemonDiagnosisJsonContract(
            Reason: DaemonDiagnosisReasonValues.ShutdownRequested,
            Message: "daemon shutdown completed",
            ReportedBy: DaemonDiagnosisReportedByValues.Unity,
            IsInferred: false,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            ProcessId: 1234,
            EditorInstancePath: null,
            SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero),
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 2, TimeSpan.Zero),
            UnityLogPath: null,
            StartupPhase: ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.ScriptCompilation),
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            PrimaryDiagnostic: new DaemonDiagnosisPrimaryDiagnosticJsonContract(
                Kind: "unknownDiagnosticKind",
                Code: "CS1739",
                File: "Assets/Foo.cs",
                Line: 74,
                Column: 17,
                Message: "Missing parameter"));
        await File.WriteAllTextAsync(
            diagnosisPath,
            DaemonDiagnosisJsonContractSerializer.Serialize(contract) + Environment.NewLine,
            CancellationToken.None);

        var readResult = await store.ReadAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-invalid"), CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("primaryDiagnostic.kind", error.Message, StringComparison.Ordinal);
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
            EditorInstancePath: null,
            SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero),
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 2, TimeSpan.Zero),
            UnityLogPath: "/repo/.ucli/local/fingerprints/fingerprint-roundtrip/unity.log",
            StartupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            PrimaryDiagnostic: new DaemonPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
                Code: "CS1739",
                File: "Assets/Foo.cs",
                Line: 74,
                Column: 17,
                Message: "Missing parameter"));
    }
}
