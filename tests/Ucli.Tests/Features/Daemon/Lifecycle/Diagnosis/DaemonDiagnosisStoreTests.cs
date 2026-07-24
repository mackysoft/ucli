using System.Text.Json.Nodes;
using MackySoft.FileSystem;
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
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var store = new DaemonDiagnosisStore();
        var diagnosis = CreateDiagnosis(processId: 1234);

        var writeResult = await store.WriteAsync(storageRoot, ProjectFingerprintTestFactory.Create("fingerprint-roundtrip"), diagnosis, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        Assert.Null(writeResult.Error);

        var readResult = await store.ReadAsync(storageRoot, ProjectFingerprintTestFactory.Create("fingerprint-roundtrip"), CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.True(readResult.Exists);
        Assert.Equal(diagnosis, readResult.Diagnosis);

        var deleteResult = await store.DeleteAsync(storageRoot, ProjectFingerprintTestFactory.Create("fingerprint-roundtrip"), CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        var readAfterDeleteResult = await store.ReadAsync(storageRoot, ProjectFingerprintTestFactory.Create("fingerprint-roundtrip"), CancellationToken.None);
        Assert.True(readAfterDeleteResult.IsSuccess);
        Assert.False(readAfterDeleteResult.Exists);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenDiagnosisJsonIsMalformed_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-diagnosis-store", "malformed-json");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var store = new DaemonDiagnosisStore();
        var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(storageRoot, ProjectFingerprintTestFactory.Create("fingerprint-malformed"));
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosisPath.Value)!);
        await File.WriteAllTextAsync(diagnosisPath.Value, "{", CancellationToken.None);

        var readResult = await store.ReadAsync(storageRoot, ProjectFingerprintTestFactory.Create("fingerprint-malformed"), CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.False(readResult.Exists);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenUnityLogPathIsRelative_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-diagnosis-store", "relative-unity-log");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint-relative-path");
        var store = new DaemonDiagnosisStore();
        var writeResult = await store.WriteAsync(
            storageRoot,
            fingerprint,
            CreateDiagnosis(processId: 1234),
            CancellationToken.None);
        Assert.True(writeResult.IsSuccess);
        var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(storageRoot, fingerprint);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(diagnosisPath.Value, CancellationToken.None))!.AsObject();
        json["unityLogPath"] = "relative/unity.log";
        await File.WriteAllTextAsync(diagnosisPath.Value, json.ToJsonString(), CancellationToken.None);

        var readResult = await store.ReadAsync(storageRoot, fingerprint, CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, readResult.Error!.Kind);
        Assert.Contains("unityLogPath", readResult.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenSessionIssuedAtUtcIsDefault_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateDiagnosis(processId: 1234, sessionIssuedAtUtc: default(DateTimeOffset)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenStartupPhaseIsUndefined_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateDiagnosis(processId: 1234, startupPhase: (DaemonDiagnosisStartupPhase)0));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenPrimaryDiagnosticKindIsUnknown_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-diagnosis-store", "invalid-primary-diagnostic-kind");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var store = new DaemonDiagnosisStore();
        var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(storageRoot, ProjectFingerprintTestFactory.Create("fingerprint-invalid"));
        Directory.CreateDirectory(Path.GetDirectoryName(diagnosisPath.Value)!);
        var contract = new DaemonDiagnosisJsonContract(
            Reason: DaemonDiagnosisReason.ShutdownRequested,
            Message: "daemon shutdown completed",
            ReportedBy: DaemonDiagnosisReportedBy.Unity,
            IsInferred: false,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            ProcessId: 1234,
            EditorInstancePath: null,
            SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero),
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 2, TimeSpan.Zero),
            UnityLogPath: null,
            StartupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
            ActionRequired: DaemonDiagnosisActionRequired.FixCompileErrors,
            PrimaryDiagnostic: new DaemonDiagnosisPrimaryDiagnosticJsonContract(
                Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                Code: "CS1739",
                File: "Assets/Foo.cs",
                Line: 74,
                Column: 17,
                Message: "Missing parameter"));
        var json = JsonNode.Parse(DaemonDiagnosisJsonContractSerializer.Serialize(contract))!.AsObject();
        json["primaryDiagnostic"]!.AsObject()["kind"] = "unknownDiagnosticKind";
        await File.WriteAllTextAsync(
            diagnosisPath.Value,
            json.ToJsonString() + Environment.NewLine,
            CancellationToken.None);

        var readResult = await store.ReadAsync(storageRoot, ProjectFingerprintTestFactory.Create("fingerprint-invalid"), CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("primaryDiagnostic.kind", error.Message, StringComparison.Ordinal);
    }

    private static DaemonDiagnosis CreateDiagnosis (
        int? processId,
        DateTimeOffset? sessionIssuedAtUtc = null,
        DaemonDiagnosisStartupPhase startupPhase = DaemonDiagnosisStartupPhase.ScriptCompilation)
    {
        return new DaemonDiagnosis(
            Reason: DaemonDiagnosisReason.ShutdownRequested,
            Message: "daemon shutdown completed",
            ReportedBy: DaemonDiagnosisReportedBy.Unity,
            IsInferred: false,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            ProcessId: processId,
            EditorInstancePath: null,
            SessionIssuedAtUtc: sessionIssuedAtUtc ?? new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero),
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 2, TimeSpan.Zero),
            UnityLogPath: AbsolutePath.Parse(Path.Combine(
                ProjectPathTestValues.RepositoryRoot,
                ".ucli",
                "local",
                "fingerprints",
                "fingerprint-roundtrip",
                "unity.log")),
            StartupPhase: startupPhase,
            ActionRequired: DaemonDiagnosisActionRequired.FixCompileErrors,
            PrimaryDiagnostic: new DaemonPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                Code: "CS1739",
                File: "Assets/Foo.cs",
                Line: 74,
                Column: 17,
                Message: "Missing parameter"));
    }
}
