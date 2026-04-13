using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Index;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Scenes;

/// <summary> Reads one live scene-tree-lite snapshot through oneshot Unity IPC. </summary>
internal sealed class SceneTreeLiteSnapshotReader : ISceneTreeLiteSnapshotReader
{
    private readonly IUnityIpcRequestExecutor ipcRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="SceneTreeLiteSnapshotReader" /> class. </summary>
    public SceneTreeLiteSnapshotReader (IUnityIpcRequestExecutor ipcRequestExecutor)
    {
        this.ipcRequestExecutor = ipcRequestExecutor ?? throw new ArgumentNullException(nameof(ipcRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<SceneTreeLiteSnapshotFetchResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        TimeSpan timeout,
        string scenePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var executionResult = await ipcRequestExecutor.Execute(
                command,
                UnityExecutionMode.Oneshot,
                timeout,
                config,
                project,
                IpcMethodNames.IndexSceneTreeLiteRead,
                IpcPayloadCodec.SerializeToElement(new IpcIndexSceneTreeLiteReadRequest(scenePath)),
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
        IpcResponse response,
        string responseSourceName,
        string requestedScenePath)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseSourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedScenePath);

        if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
        {
            if (firstError != null)
            {
                return SceneTreeLiteSnapshotFetchResult.Failure(firstError.Message, firstError.Code);
            }

            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} failed with status '{status}'.",
                IpcErrorCodes.InternalError);
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcIndexSceneTreeLiteReadResponse payload, out var payloadError))
        {
            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {payloadError.Message}",
                IpcErrorCodes.InternalError);
        }

        if (!string.Equals(payload.ScenePath, requestedScenePath, StringComparison.Ordinal))
        {
            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. scenePath does not match the requested scene path.",
                IpcErrorCodes.InternalError);
        }

        if (!IndexCatalogContractValidator.TryValidateSceneTreeLiteNodes(payload.Roots, "roots", out var rootsError))
        {
            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {rootsError}",
                IpcErrorCodes.InternalError);
        }

        return SceneTreeLiteSnapshotFetchResult.Success(payload);
    }
}