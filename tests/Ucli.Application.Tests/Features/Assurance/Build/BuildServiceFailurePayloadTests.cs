using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceFailurePayloadTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithDirtySceneResponse_ReturnsCommandFailureWithProbePayload ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var dirtyState = new IpcBuildDirtyState(
            Checked: true,
            Dirty: true,
            Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
            Items:
            [
                new IpcBuildDirtyStateItem(
                    ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene),
                    "Assets/Scenes/Main.unity"),
            ]);
        var input = CreateInputProbe();
        var errorPayload = new IpcBuildRunErrorPayload(
            Project: new IpcProjectIdentity("/workspace/UnityProject", DefaultProjectFingerprint, "6000.1.4f1"),
            LifecycleBefore: CreateLifecycleSnapshot(10),
            DirtyState: dirtyState,
            Input: input);
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new OperationExecutionError(BuildErrorCodes.BuildDirtyStatePresent, "Dirty scene state is present.", null)],
            HasFailureStatus: true,
            FailureStatus: IpcProtocol.StatusError);
        var service = CreateService(
            requestExecutor: new RecordingUnityRequestExecutor(_ => UnityRequestExecutionResult.Success(response)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildDirtyStatePresent, error.Code);
        Assert.NotNull(result.DirtyState);
        Assert.True(result.DirtyState!.Checked);
        Assert.True(result.DirtyState.Dirty);
        var item = Assert.Single(result.DirtyState.Items);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene), item.Kind);
        Assert.Equal("Assets/Scenes/Main.unity", item.Path);
        Assert.Null(result.Output);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithDirtyStateIndeterminateResponse_ReturnsCommandFailureWithProbePayload ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var dirtyState = new IpcBuildDirtyState(
            Checked: true,
            Dirty: false,
            Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Partial),
            Items: []);
        var errorPayload = new IpcBuildRunErrorPayload(
            Project: new IpcProjectIdentity("/workspace/UnityProject", DefaultProjectFingerprint, "6000.1.4f1"),
            LifecycleBefore: CreateLifecycleSnapshot(10),
            DirtyState: dirtyState,
            Input: CreateInputProbe());
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new OperationExecutionError(BuildErrorCodes.BuildDirtyStateIndeterminate, "Dirty state coverage is partial.", null)],
            HasFailureStatus: true,
            FailureStatus: IpcProtocol.StatusError);
        var service = CreateService(
            requestExecutor: new RecordingUnityRequestExecutor(_ => UnityRequestExecutionResult.Success(response)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildDirtyStateIndeterminate, error.Code);
        Assert.NotNull(result.DirtyState);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Partial), result.DirtyState!.Coverage);
        Assert.Empty(result.DirtyState.Items);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithNonDirtyFailurePayload_DoesNotReturnDirtyState ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var dirtyState = new IpcBuildDirtyState(
            Checked: true,
            Dirty: true,
            Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
            Items:
            [
                new IpcBuildDirtyStateItem(
                    ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene),
                    "Assets/Scenes/Main.unity"),
            ]);
        var errorPayload = new IpcBuildRunErrorPayload(
            Project: new IpcProjectIdentity("/workspace/UnityProject", DefaultProjectFingerprint, "6000.1.4f1"),
            LifecycleBefore: CreateLifecycleSnapshot(10),
            DirtyState: dirtyState,
            Input: CreateInputProbe());
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new OperationExecutionError(BuildErrorCodes.BuildArtifactWriteFailed, "Artifact write failed.", null)],
            HasFailureStatus: true,
            FailureStatus: IpcProtocol.StatusError);
        var service = CreateService(
            requestExecutor: new RecordingUnityRequestExecutor(_ => UnityRequestExecutionResult.Success(response)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
        Assert.Null(result.DirtyState);
        Assert.Null(result.Output);
    }
}
