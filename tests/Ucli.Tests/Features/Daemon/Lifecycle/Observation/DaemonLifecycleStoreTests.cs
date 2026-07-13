using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonLifecycleStoreTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData(IpcEditorBlockingReason.Startup)]
    public async Task Read_WhenBlockingReasonDoesNotMatchLifecycleState_ReturnsInvalidArgument (
        IpcEditorBlockingReason? blockingReason)
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "inconsistent-blocking-reason");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(
            scope.FullPath,
            "fingerprint-invalid",
            CreateContract() with
            {
                BlockingReason = blockingReason.HasValue
                    ? ContractLiteralCodec.ToValue(blockingReason.Value)
                    : null,
            });

        var readResult = await store.ReadAsync(scope.FullPath, "fingerprint-invalid", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("blockingReason", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenBlockingReasonIsNotCanonical_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "noncanonical-blocking-reason");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(
            scope.FullPath,
            "fingerprint-invalid",
            CreateContract() with
            {
                BlockingReason = $" {ContractLiteralCodec.ToValue(IpcEditorBlockingReason.CompileFailed)} ",
            });

        var readResult = await store.ReadAsync(scope.FullPath, "fingerprint-invalid", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("blockingReason", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenExplicitCanAcceptExecutionRequestsDoesNotMatchLifecycleState_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "inconsistent-request-acceptance");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(
            scope.FullPath,
            "fingerprint-invalid",
            CreateContract() with
            {
                CanAcceptExecutionRequests = true,
            });

        var readResult = await store.ReadAsync(scope.FullPath, "fingerprint-invalid", CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("canAcceptExecutionRequests", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenCanAcceptExecutionRequestsIsOmitted_UsesLifecycleSemantics ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "derived-request-acceptance");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(scope.FullPath, "fingerprint-valid", CreateContract());

        var readResult = await store.ReadAsync(scope.FullPath, "fingerprint-valid", CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal(IpcEditorBlockingReason.CompileFailed, readResult.Observation!.BlockingReason);
        Assert.False(readResult.Observation.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenLifecycleJsonContainsEditorInstanceId_NormalizesField ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "editor-instance-id");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(
            scope.FullPath,
            "fingerprint-editor-instance",
            CreateContract() with
            {
                EditorInstanceId = " editor-instance-1 ",
            });

        var readResult = await store.ReadAsync(scope.FullPath, "fingerprint-editor-instance", CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal("editor-instance-1", readResult.Observation!.EditorInstanceId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenLifecycleJsonContainsPlayMode_NormalizesSnapshot ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "play-mode");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(
            scope.FullPath,
            "fingerprint-play-mode",
            CreateContract() with
            {
                ServerVersion = " 0.5.0 ",
                CanAcceptExecutionRequests = false,
                PlayMode = new IpcPlayModeSnapshot(
                    State: $" {"playing"} ",
                    Transition: $" {"none"} ",
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true,
                    Generation: " 3 "),
            });

        var readResult = await store.ReadAsync(scope.FullPath, "fingerprint-play-mode", CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal("0.5.0", readResult.Observation!.ServerVersion);
        Assert.False(readResult.Observation!.CanAcceptExecutionRequests);
        var playMode = Assert.IsType<IpcPlayModeSnapshot>(readResult.Observation.PlayMode);
        Assert.Equal("playing", playMode.State);
        Assert.Equal("none", playMode.Transition);
        Assert.True(playMode.IsPlaying);
        Assert.True(playMode.IsPlayingOrWillChangePlaymode);
        Assert.Equal("3", playMode.Generation);
    }

    private static DaemonLifecycleJsonContract CreateContract ()
    {
        return new DaemonLifecycleJsonContract(
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero),
            EditorMode: "gui",
            LifecycleState: ContractLiteralCodec.ToValue(IpcEditorLifecycleState.CompileFailed),
            BlockingReason: ContractLiteralCodec.ToValue(IpcEditorBlockingReason.CompileFailed),
            CompileState: ContractLiteralCodec.ToValue(IpcCompileState.Failed),
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
