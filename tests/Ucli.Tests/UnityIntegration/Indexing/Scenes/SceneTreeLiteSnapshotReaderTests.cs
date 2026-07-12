using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

namespace MackySoft.Ucli.Tests.Scenes;

public sealed class SceneTreeLiteSnapshotReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_ForwardsRequestedModeAndReturnsValidatedPayload ()
    {
        var executor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateSuccessResponse(
            CreatePayload("Assets/Scenes/Main.unity", "Root"))));
        var reader = new SceneTreeLiteSnapshotReader(executor);

        var result = await reader.ReadAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            failFast: true,
            loadedSceneOnly: true,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var execution = UnityRequestExecutorAssert.RawPayloadExecutedOnce<IpcIndexSceneTreeLiteReadRequest>(
            executor,
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            IpcMethodNames.IndexSceneTreeLiteRead);
        Assert.Equal("Assets/Scenes/Main.unity", execution.Payload.ScenePath);
        Assert.True(execution.Payload.FailFast);
        Assert.True(execution.Payload.LoadedSceneOnly);
        Assert.Single(result.Response!.Roots!);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_ReturnsFailureStatusMessage_WhenFailureStatusHasNoErrors ()
    {
        var executor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(
            "busy",
            new { })));
        var reader = new SceneTreeLiteSnapshotReader(executor);

        var result = await reader.ReadAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal("index.scene-tree-lite.read failed with status 'busy'.", result.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_AcceptsWhitespaceOnlyNodeName_WhenPayloadIsOtherwiseValid ()
    {
        var executor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateSuccessResponse(
            CreatePayload("Assets/Scenes/Main.unity", " "))));
        var reader = new SceneTreeLiteSnapshotReader(executor);

        var result = await reader.ReadAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Daemon,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(" ", result.Response!.Roots![0].Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenResponseScenePathDoesNotMatch_ReturnsInternalError ()
    {
        var executor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateSuccessResponse(
            CreatePayload("Assets/Scenes/Other.unity", "Root"))));
        var reader = new SceneTreeLiteSnapshotReader(executor);

        var result = await reader.ReadAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("scenePath does not match", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenRootsAreInvalid_ReturnsInternalError ()
    {
        var executor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(CreateSuccessResponse(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.UtcNow,
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract("Root", string.Empty, null, IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                    ],
                    SourceState: CreateSourceState()))));
        var reader = new SceneTreeLiteSnapshotReader(executor);

        var result = await reader.ReadAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("payload is invalid", result.Message, StringComparison.Ordinal);
    }

    private static UnityRequestResponse CreateSuccessResponse (object payload)
    {
        return CreateResponse(IpcProtocol.StatusOk, payload);
    }

    private static IpcIndexSceneTreeLiteReadResponse CreatePayload (
        string scenePath,
        string rootName)
    {
        return new IpcIndexSceneTreeLiteReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
            ScenePath: scenePath,
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(rootName, "GlobalObjectId_V1-1-1-1", Array.Empty<IndexSceneTreeLiteNodeJsonContract>(), IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ],
            SourceState: CreateSourceState());
    }

    private static SceneTreeSourceState CreateSourceState ()
    {
        return new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false);
    }

    private static UnityRequestResponse CreateResponse (
        string status,
        object payload)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: status,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: Array.Empty<IpcError>()));
    }

}
