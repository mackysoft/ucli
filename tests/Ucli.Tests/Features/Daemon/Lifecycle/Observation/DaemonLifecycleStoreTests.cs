using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonLifecycleStoreTests
{
    private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenLifecycleJsonContainsInvalidActionRequired_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "invalid-action-required");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint-invalid"),
            CreateContract() with
            {
                ActionRequired = "unknownAction",
            });

        var readResult = await store.ReadAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-invalid"), CancellationToken.None);

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
            ProjectFingerprintTestFactory.Create("fingerprint-invalid"),
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

        var readResult = await store.ReadAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-invalid"), CancellationToken.None);

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
            ProjectFingerprintTestFactory.Create("fingerprint-diagnostic"),
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

        var readResult = await store.ReadAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-diagnostic"), CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        var diagnostic = Assert.IsType<IpcPrimaryDiagnostic>(readResult.Observation!.PrimaryDiagnostic);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, diagnostic.Kind);
        Assert.Equal("CS1739", diagnostic.Code);
        Assert.Equal("Assets/Foo.cs", diagnostic.File);
        Assert.Equal(74, diagnostic.Line);
        Assert.Equal(17, diagnostic.Column);
        Assert.Equal("Missing parameter", diagnostic.Message);
        Assert.Equal(EditorInstanceId, readResult.Observation.EditorInstanceId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("editor-instance")]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    [InlineData(" 11111111111111111111111111111111 ")]
    [InlineData("1111111111111111111111111111111")]
    [Trait("Size", "Medium")]
    public async Task Read_WhenLifecycleJsonContainsInvalidEditorInstanceId_ReturnsInvalidArgument (
        string? editorInstanceId)
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "editor-instance-id");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint-editor-instance"),
            CreateContract() with
            {
                EditorInstanceId = editorInstanceId,
            });

        var readResult = await store.ReadAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-editor-instance"), CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("editorInstanceId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenLifecycleJsonContainsPlayMode_NormalizesSnapshot ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "play-mode");
        var store = new DaemonLifecycleStore();
        await WriteContractAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint-play-mode"),
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

        var readResult = await store.ReadAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-play-mode"), CancellationToken.None);

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
            LifecycleState: IpcEditorLifecycleStateCodec.CompileFailed,
            BlockingReason: null,
            CompileState: IpcCompileStateCodec.Failed,
            CompileGeneration: "compile-1",
            DomainReloadGeneration: "reload-1",
            ObservedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 2, TimeSpan.Zero),
            ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
            PrimaryDiagnostic: null)
        {
            EditorInstanceId = EditorInstanceId.ToString("N"),
        };
    }

    private static async Task WriteContractAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
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
