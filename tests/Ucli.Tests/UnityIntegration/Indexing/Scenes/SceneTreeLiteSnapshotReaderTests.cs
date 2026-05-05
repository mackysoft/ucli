using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;
using MackySoft.Ucli.UnityIntegration.Ipc;

namespace MackySoft.Ucli.Tests.Scenes;

public sealed class SceneTreeLiteSnapshotReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_ForwardsRequestedModeAndReturnsValidatedPayload ()
    {
        var executor = new StubUnityIpcRequestExecutor
        {
            Result = UnityRequestExecutionResult.Success(CreateSuccessResponse(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract("Root", "GlobalObjectId_V1-1-1-1", Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
                    ]))),
        };
        var reader = new SceneTreeLiteSnapshotReader(executor);

        var result = await reader.Read(
            CreateProject(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            failFast: true,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UnityExecutionMode.Auto, executor.LastMode);
        var request = Assert.IsType<UnityRequestPayload.Raw>(executor.LastPayload);
        Assert.Equal(IpcMethodNames.IndexSceneTreeLiteRead, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcIndexSceneTreeLiteReadRequest payload, out _));
        Assert.Equal("Assets/Scenes/Main.unity", payload.ScenePath);
        Assert.True(payload.FailFast);
        Assert.Single(result.Response!.Roots!);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_AcceptsWhitespaceOnlyNodeName_WhenPayloadIsOtherwiseValid ()
    {
        var executor = new StubUnityIpcRequestExecutor
        {
            Result = UnityRequestExecutionResult.Success(CreateSuccessResponse(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract(" ", "GlobalObjectId_V1-1-1-1", Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
                    ]))),
        };
        var reader = new SceneTreeLiteSnapshotReader(executor);

        var result = await reader.Read(
            CreateProject(),
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
        var executor = new StubUnityIpcRequestExecutor
        {
            Result = UnityRequestExecutionResult.Success(CreateSuccessResponse(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.UtcNow,
                    ScenePath: "Assets/Scenes/Other.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract("Root", string.Empty, Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
                    ]))),
        };
        var reader = new SceneTreeLiteSnapshotReader(executor);

        var result = await reader.Read(
            CreateProject(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("scenePath does not match", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenRootsAreInvalid_ReturnsInternalError ()
    {
        var executor = new StubUnityIpcRequestExecutor
        {
            Result = UnityRequestExecutionResult.Success(CreateSuccessResponse(
                new IpcIndexSceneTreeLiteReadResponse(
                    GeneratedAtUtc: DateTimeOffset.UtcNow,
                    ScenePath: "Assets/Scenes/Main.unity",
                    Roots:
                    [
                        new IndexSceneTreeLiteNodeJsonContract("Root", string.Empty, null),
                    ]))),
        };
        var reader = new SceneTreeLiteSnapshotReader(executor);

        var result = await reader.Read(
            CreateProject(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromSeconds(1),
            "Assets/Scenes/Main.unity",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("payload is invalid", result.Message, StringComparison.Ordinal);
    }

    private static ResolvedUnityProjectContext CreateProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static UnityRequestResponse CreateSuccessResponse (object payload)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-scene-tree-lite",
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: Array.Empty<IpcError>()));
    }

    private sealed class StubUnityIpcRequestExecutor : IUnityRequestExecutor
    {
        public UnityExecutionMode LastMode { get; private set; }

        public UnityRequestPayload? LastPayload { get; private set; }

        public UnityRequestExecutionResult Result { get; set; }
            = UnityRequestExecutionResult.Failure("not configured", IpcErrorCodes.InternalError);

        public ValueTask<UnityRequestExecutionResult> Execute (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMode = mode;
            LastPayload = payload;
            return ValueTask.FromResult(Result);
        }
    }
}
