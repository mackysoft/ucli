using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceFailurePayloadTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithDirtySceneResponse_ReturnsCommandFailureWithProbePayload ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var dirtyState = new IpcBuildDirtyState(
            Dirty: true,
            Coverage: IpcBuildDirtyStateCoverage.Full,
            Items:
            [
                new IpcBuildDirtyStateItem(
                    IpcBuildDirtyStateItemKind.Scene,
                    new ProjectMutationAuditPath("Assets/Scenes/Main.unity")),
            ]);
        var input = CreateInputProbe();
        var errorPayload = new IpcBuildRunErrorPayload(
            Project: new UnityProjectIdentity(ProjectContextTestFactory.UnityProjectRoot, DefaultProjectFingerprint, "6000.1.4f1"),
            LifecycleBefore: CreateLifecycleSnapshot(10),
            DirtyState: dirtyState,
            Input: input);
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new OperationExecutionError(BuildErrorCodes.BuildDirtyStatePresent, "Dirty scene state is present.", InstancePath: null)]);
        var service = CreateService(
            requestExecutor: new RecordingUnityRequestExecutor(_ => UnityRequestExecutionResult.Success(response)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        var failed = Assert.IsType<BuildExecutionResult.DirtyStateFailedResult>(result);
        var error = failed.Failure;
        Assert.Equal(BuildErrorCodes.BuildDirtyStatePresent, error.Code);
        Assert.True(failed.DirtyState.Dirty);
        var item = Assert.Single(failed.DirtyState.Items);
        Assert.Equal(IpcBuildDirtyStateItemKind.Scene, item.Kind);
        Assert.Equal(new ProjectMutationAuditPath("Assets/Scenes/Main.unity"), item.Path);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithDirtyStateIndeterminateResponse_ReturnsCommandFailureWithProbePayload ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var dirtyState = new IpcBuildDirtyState(
            Dirty: false,
            Coverage: IpcBuildDirtyStateCoverage.Partial,
            Items: []);
        var errorPayload = new IpcBuildRunErrorPayload(
            Project: new UnityProjectIdentity(ProjectContextTestFactory.UnityProjectRoot, DefaultProjectFingerprint, "6000.1.4f1"),
            LifecycleBefore: CreateLifecycleSnapshot(10),
            DirtyState: dirtyState,
            Input: CreateInputProbe());
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new OperationExecutionError(BuildErrorCodes.BuildDirtyStateIndeterminate, "Dirty state coverage is partial.", InstancePath: null)]);
        var service = CreateService(
            requestExecutor: new RecordingUnityRequestExecutor(_ => UnityRequestExecutionResult.Success(response)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        var failed = Assert.IsType<BuildExecutionResult.DirtyStateFailedResult>(result);
        var error = failed.Failure;
        Assert.Equal(BuildErrorCodes.BuildDirtyStateIndeterminate, error.Code);
        Assert.Equal(IpcBuildDirtyStateCoverage.Partial, failed.DirtyState.Coverage);
        Assert.Empty(failed.DirtyState.Items);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithNonDirtyFailurePayload_DoesNotReturnDirtyState ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var dirtyState = new IpcBuildDirtyState(
            Dirty: true,
            Coverage: IpcBuildDirtyStateCoverage.Full,
            Items:
            [
                new IpcBuildDirtyStateItem(
                    IpcBuildDirtyStateItemKind.Scene,
                    new ProjectMutationAuditPath("Assets/Scenes/Main.unity")),
            ]);
        var errorPayload = new IpcBuildRunErrorPayload(
            Project: new UnityProjectIdentity(ProjectContextTestFactory.UnityProjectRoot, DefaultProjectFingerprint, "6000.1.4f1"),
            LifecycleBefore: CreateLifecycleSnapshot(10),
            DirtyState: dirtyState,
            Input: CreateInputProbe());
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new OperationExecutionError(BuildErrorCodes.BuildArtifactWriteFailed, "Artifact write failed.", InstancePath: null)]);
        var service = CreateService(
            requestExecutor: new RecordingUnityRequestExecutor(_ => UnityRequestExecutionResult.Success(response)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        var failed = Assert.IsType<BuildExecutionResult.FailedResult>(result);
        var error = failed.Failure;
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
    }
}
