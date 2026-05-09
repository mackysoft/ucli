using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonLifecycleStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenLifecycleJsonContainsInvalidActionRequired_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "invalid-action-required");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(
            scope.FullPath,
            "fingerprint-invalid",
            CreateContract() with
            {
                ActionRequired = "unknownAction",
            });

        var readResult = await store.ReadAsync(scope.FullPath, "fingerprint-invalid", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("actionRequired", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenLifecycleJsonContainsInvalidPrimaryDiagnosticKind_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "invalid-primary-diagnostic-kind");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(
            scope.FullPath,
            "fingerprint-invalid",
            CreateContract() with
            {
                PrimaryDiagnostic = new IpcPrimaryDiagnostic(
                    Kind: "unknownDiagnosticKind",
                    Code: "CS1739",
                    File: "Assets/Foo.cs",
                    Line: 74,
                    Column: 17,
                    Message: "Missing parameter"),
            });

        var readResult = await store.ReadAsync(scope.FullPath, "fingerprint-invalid", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("primaryDiagnostic.kind", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenLifecycleJsonContainsPrimaryDiagnostic_NormalizesFields ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "primary-diagnostic");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(
            scope.FullPath,
            "fingerprint-diagnostic",
            CreateContract() with
            {
                PrimaryDiagnostic = new IpcPrimaryDiagnostic(
                    Kind: $" {DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler} ",
                    Code: " CS1739 ",
                    File: " Assets/Foo.cs ",
                    Line: 74,
                    Column: 17,
                    Message: " Missing parameter "),
            });

        var readResult = await store.ReadAsync(scope.FullPath, "fingerprint-diagnostic", CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        var diagnostic = Assert.IsType<IpcPrimaryDiagnostic>(readResult.Observation!.PrimaryDiagnostic);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, diagnostic.Kind);
        Assert.Equal("CS1739", diagnostic.Code);
        Assert.Equal("Assets/Foo.cs", diagnostic.File);
        Assert.Equal(74, diagnostic.Line);
        Assert.Equal(17, diagnostic.Column);
        Assert.Equal("Missing parameter", diagnostic.Message);
    }

    private static DaemonLifecycleJsonContract CreateContract ()
    {
        return new DaemonLifecycleJsonContract(
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero),
            EditorMode: DaemonEditorModeValues.Gui,
            LifecycleState: IpcEditorLifecycleStateCodec.CompileFailed,
            BlockingReason: null,
            CompileState: IpcCompileStateCodec.Failed,
            CompileGeneration: "compile-1",
            DomainReloadGeneration: "reload-1",
            ObservedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 2, TimeSpan.Zero),
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            PrimaryDiagnostic: null);
    }

    private static async Task WriteContractAsync (
        string storageRoot,
        string projectFingerprint,
        DaemonLifecycleJsonContract contract)
    {
        var lifecyclePath = UcliStoragePathResolver.ResolveDaemonLifecyclePath(storageRoot, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(lifecyclePath)!);
        await File.WriteAllTextAsync(
            lifecyclePath,
            DaemonLifecycleJsonContractSerializer.Serialize(contract) + Environment.NewLine,
            CancellationToken.None);
    }
}
