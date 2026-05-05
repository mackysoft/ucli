using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Reads one persisted-preview scene-tree-lite snapshot through the shared Unity IPC execution path. </summary>
internal sealed class SceneTreeLiteSnapshotReader : ISceneTreeLiteSnapshotReader
{
    private readonly IUnityRequestExecutor ipcRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="SceneTreeLiteSnapshotReader" /> class. </summary>
    public SceneTreeLiteSnapshotReader (IUnityRequestExecutor ipcRequestExecutor)
    {
        this.ipcRequestExecutor = ipcRequestExecutor ?? throw new ArgumentNullException(nameof(ipcRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<SceneTreeLiteSnapshotFetchResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        string scenePath,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var executionResult = await ipcRequestExecutor.Execute(
                command,
                mode,
                timeout,
                config,
                project,
                IpcMethodNames.IndexSceneTreeLiteRead,
                IpcPayloadCodec.SerializeToElement(new IpcIndexSceneTreeLiteReadRequest(scenePath, failFast)),
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
