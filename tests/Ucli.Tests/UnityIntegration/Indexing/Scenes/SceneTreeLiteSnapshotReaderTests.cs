using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
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
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UnityExecutionMode.Auto, executor.LastMode);
        Assert.Equal(IpcMethodNames.IndexSceneTreeLiteRead, executor.LastMethod);
        Assert.True(IpcPayloadCodec.TryDeserialize(executor.LastPayload, out IpcIndexSceneTreeLiteReadRequest payload, out _));
        Assert.Equal("Assets/Scenes/Main.unity", payload.ScenePath);
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
            CancellationToken.None);

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
            CancellationToken.None);

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
            CancellationToken.None);

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

    private static IpcResponse CreateSuccessResponse (object payload)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-scene-tree-lite",
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: Array.Empty<IpcError>());
    }

    private sealed class StubUnityIpcRequestExecutor : IUnityRequestExecutor
    {
        public UnityExecutionMode LastMode { get; private set; }

        public string LastMethod { get; private set; } = string.Empty;

        public JsonElement LastPayload { get; private set; }

        public UnityRequestExecutionResult Result { get; set; }
            = UnityRequestExecutionResult.Failure("not configured", IpcErrorCodes.InternalError);

        public ValueTask<UnityRequestExecutionResult> Execute (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            string method,
            JsonElement payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMode = mode;
            LastMethod = method;
            LastPayload = payload;
            return ValueTask.FromResult(Result);
        }
    }
}