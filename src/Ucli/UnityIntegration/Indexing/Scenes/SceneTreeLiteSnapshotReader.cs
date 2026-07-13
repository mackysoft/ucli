using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
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
        string scenePath,
        bool failFast = false,
        bool loadedSceneOnly = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
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
                executionResult.ErrorCode!.Value);
        }

        return CreateResultFromResponse(executionResult.Response!, "index.scene-tree-lite.read", scenePath);
    }

    private static SceneTreeLiteSnapshotFetchResult CreateResultFromResponse (
        UnityRequestResponse response,
        string responseSourceName,
        string requestedScenePath)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseSourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedScenePath);

        if (response.HasFailureStatus || response.Errors.Count != 0)
        {
            var firstError = response.Errors.FirstOrDefault();
            if (firstError != null)
            {
                return SceneTreeLiteSnapshotFetchResult.Failure(firstError.Message, firstError.Code);
            }

            if (!string.IsNullOrWhiteSpace(response.FailureStatus))
            {
                return SceneTreeLiteSnapshotFetchResult.Failure(
                    $"{responseSourceName} failed with status '{response.FailureStatus}'.",
                    UcliCoreErrorCodes.InternalError);
            }

            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} failed with an error status.",
                UcliCoreErrorCodes.InternalError);
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcIndexSceneTreeLiteReadResponse payload, out var payloadError))
        {
            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {payloadError.Message}",
                UcliCoreErrorCodes.InternalError);
        }

        if (!string.Equals(payload.ScenePath, requestedScenePath, StringComparison.Ordinal))
        {
            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. scenePath does not match the requested scene path.",
                UcliCoreErrorCodes.InternalError);
        }

        if (!IndexCatalogContractValidator.TryValidateSceneTreeLiteNodes(payload.Roots, "roots", out var rootsError))
        {
            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {rootsError}",
                UcliCoreErrorCodes.InternalError);
        }

        if (payload.SourceState == null || payload.SourceState.Kind == SceneTreeSourceStateKind.Unspecified)
        {
            return SceneTreeLiteSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. sourceState is required.",
                UcliCoreErrorCodes.InternalError);
        }

        return SceneTreeLiteSnapshotFetchResult.Success(payload);
    }
}
