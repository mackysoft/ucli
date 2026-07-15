using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Reads one scene-tree-lite snapshot through the shared Unity IPC execution path. </summary>
internal sealed class SceneTreeLiteSnapshotReader : ISceneTreeLiteSnapshotReader
{
    private readonly IUnityRequestExecutor ipcRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="SceneTreeLiteSnapshotReader" /> class. </summary>
    public SceneTreeLiteSnapshotReader (IUnityRequestExecutor ipcRequestExecutor)
    {
        this.ipcRequestExecutor = ipcRequestExecutor ?? throw new ArgumentNullException(nameof(ipcRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<SceneTreeLiteSnapshotFetchResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UnityScenePath scenePath,
        bool failFast = false,
        bool loadedSceneOnly = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(scenePath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var executionResult = await ipcRequestExecutor.ExecuteAsync(
                command,
                mode,
                timeout,
                config,
                project,
                new UnityRequestPayload.IndexSceneTreeLiteRead(scenePath, failFast, loadedSceneOnly),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            return SceneTreeLiteSnapshotFetchResult.Failure(
                executionResult.Message,
                executionResult.ErrorCode!);
        }

        return CreateResultFromResponse(executionResult.Response!, "index.scene-tree-lite.read", scenePath);
    }

    private static SceneTreeLiteSnapshotFetchResult CreateResultFromResponse (
        UnityRequestResponse response,
        string responseSourceName,
        UnityScenePath requestedScenePath)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseSourceName);
        ArgumentNullException.ThrowIfNull(requestedScenePath);

        if (response.Errors.Count != 0)
        {
            var firstError = response.Errors[0];
            return SceneTreeLiteSnapshotFetchResult.Failure(firstError.Message, firstError.Code);
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcIndexSceneTreeLiteReadResponse payload, out var payloadError))
        {
            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {payloadError.Message}",
                UcliCoreErrorCodes.InternalError);
        }

        if (payload.ScenePath != requestedScenePath)
        {
            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. scenePath does not match the requested scene path.",
                UcliCoreErrorCodes.InternalError);
        }

        if (!IndexCatalogContractValidator.TryProjectSceneTreeLiteNodes(
                payload.Roots,
                "roots",
                out var roots,
                out var rootsError))
        {
            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {rootsError}",
                UcliCoreErrorCodes.InternalError);
        }

        return SceneTreeLiteSnapshotFetchResult.Success(
            new SceneTreeLiteSourceSnapshot(
                payload.GeneratedAtUtc,
                payload.ScenePath,
                roots,
                payload.SourceState));
    }
}
