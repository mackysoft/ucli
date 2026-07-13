using System.Text.Json.Nodes;
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
    [Trait("Size", "Small")]
    public async Task Read_WhenStateIsCompileFailed_DerivesLifecycleSemantics ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "derived-lifecycle-semantics");
        var store = new DaemonLifecycleStore();
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-valid");
        await WriteContractAsync(scope.FullPath, projectFingerprint, CreateContract());

        var readResult = await store.ReadAsync(scope.FullPath, projectFingerprint, CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal(IpcEditorLifecycleState.CompileFailed, readResult.Observation!.State.LifecycleState);
        Assert.Equal(IpcEditorBlockingReason.CompileFailed, readResult.Observation.BlockingReason);
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
            ProjectFingerprintTestFactory.Create("fingerprint-invalid"),
            CreateContract(actionRequired: "unknownAction"));

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
            CreateContract(primaryDiagnostic: new IpcPrimaryDiagnostic(
                Kind: "unknownDiagnosticKind",
                Code: "CS1739",
                File: "Assets/Foo.cs",
                Line: 74,
                Column: 17,
                Message: "Missing parameter")));

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
            CreateContract(primaryDiagnostic: new IpcPrimaryDiagnostic(
                Kind: $" {DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler} ",
                Code: " CS1739 ",
                File: " Assets/Foo.cs ",
                Line: 74,
                Column: 17,
                Message: " Missing parameter ")));

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
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData(" 11111111-1111-1111-1111-111111111111 ")]
    [InlineData("11111111-1111-1111-1111-11111111111")]
    [Trait("Size", "Medium")]
    public async Task Read_WhenLifecycleJsonContainsInvalidEditorInstanceId_ReturnsInvalidArgument (
        string? editorInstanceId)
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "editor-instance-id");
        var store = new DaemonLifecycleStore();
        var json = JsonNode.Parse(DaemonLifecycleJsonContractSerializer.Serialize(CreateContract()))!.AsObject();
        json["editorInstanceId"] = editorInstanceId;
        await WriteRawJsonAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint-editor-instance"),
            json.ToJsonString());

        var readResult = await store.ReadAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-editor-instance"), CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("editorInstanceId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenLifecycleJsonContainsPlayMode_PreservesTypedSnapshot ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lifecycle-store", "play-mode");
        var store = new DaemonLifecycleStore();
        var state = new UnityEditorStateSnapshot(
            editorMode: DaemonEditorMode.Gui,
            lifecycleState: IpcEditorLifecycleState.PlayMode,
            compileState: IpcCompileState.Ready,
            generations: new IpcUnityGenerationSnapshot(1, 2, 0, 3),
            playMode: new IpcPlayModeSnapshot(
                IpcPlayModeState.Playing,
                IpcPlayModeTransition.None,
                IsPlaying: true,
                IsPlayingOrWillChangePlaymode: true));
        await WriteContractAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint-play-mode"),
            CreateContract(state: state, serverVersion: " 0.5.0 "));

        var readResult = await store.ReadAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-play-mode"), CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal("0.5.0", readResult.Observation!.ServerVersion);
        Assert.False(readResult.Observation.CanAcceptExecutionRequests);
        Assert.Equal(IpcPlayModeState.Playing, readResult.Observation.State.PlayMode.State);
        Assert.Equal(IpcPlayModeTransition.None, readResult.Observation.State.PlayMode.Transition);
        Assert.True(readResult.Observation.State.PlayMode.IsPlaying);
        Assert.True(readResult.Observation.State.PlayMode.IsPlayingOrWillChangePlaymode);
        Assert.Equal(3, readResult.Observation.State.Generations.PlayModeGeneration);
    }

    private static DaemonLifecycleJsonContract CreateContract (
        UnityEditorStateSnapshot? state = null,
        string? actionRequired = DaemonDiagnosisActionRequiredValues.FixCompileErrors,
        IpcPrimaryDiagnostic? primaryDiagnostic = null,
        string? serverVersion = null,
        Guid? editorInstanceId = null)
    {
        return new DaemonLifecycleJsonContract(
            processId: 1234,
            processStartedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero),
            state: state ?? new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Gui,
                lifecycleState: IpcEditorLifecycleState.CompileFailed,
                compileState: IpcCompileState.Failed,
                generations: new IpcUnityGenerationSnapshot(1, 1, 0, 0),
                playMode: new IpcPlayModeSnapshot(
                    IpcPlayModeState.Stopped,
                    IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 2, TimeSpan.Zero),
            actionRequired: actionRequired,
            primaryDiagnostic: primaryDiagnostic,
            serverVersion: serverVersion,
            editorInstanceId: editorInstanceId ?? EditorInstanceId);
    }

    private static async Task WriteContractAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        DaemonLifecycleJsonContract contract)
    {
        await WriteRawJsonAsync(
            storageRoot,
            projectFingerprint,
            DaemonLifecycleJsonContractSerializer.Serialize(contract));
    }

    private static async Task WriteRawJsonAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        string json)
    {
        var lifecyclePath = UcliStoragePathResolver.ResolveDaemonLifecyclePath(storageRoot, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(lifecyclePath)!);
        await File.WriteAllTextAsync(
            lifecyclePath,
            json + Environment.NewLine,
            CancellationToken.None);
    }
}
