using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets;

/// <summary> Reads one live asset lookup snapshot through the shared IPC execution path. </summary>
internal sealed class AssetLookupSnapshotReader : IAssetLookupSnapshotReader
{
    private readonly IUnityRequestExecutor ipcRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="AssetLookupSnapshotReader" /> class. </summary>
    public AssetLookupSnapshotReader (IUnityRequestExecutor ipcRequestExecutor)
    {
        this.ipcRequestExecutor = ipcRequestExecutor ?? throw new ArgumentNullException(nameof(ipcRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<AssetLookupSnapshotFetchResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var executionResult = await ipcRequestExecutor.ExecuteAsync(
                command,
                mode,
                timeout,
                config,
                project,
                new UnityRequestPayload.IndexAssetsRead(failFast),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            return AssetLookupSnapshotFetchResult.Failure(
                executionResult.Message,
                executionResult.ErrorCode!);
        }

        return CreateResultFromResponse(executionResult.Response!, "index.assets.read");
    }

    private static AssetLookupSnapshotFetchResult CreateResultFromResponse (
        UnityRequestResponse response,
        string responseSourceName)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseSourceName);

        if (response.Errors.Count != 0)
        {
            var firstError = response.Errors[0];
            return AssetLookupSnapshotFetchResult.Failure(firstError.Message, firstError.Code);
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcIndexAssetsReadResponse payload, out var payloadError))
        {
            return AssetLookupSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {payloadError.Message}",
                UcliCoreErrorCodes.InternalError);
        }

        if (!IndexCatalogContractValidator.TryProjectAssetSearchEntries(
                payload.AssetSearchEntries,
                "assetSearchEntries",
                out var assetSearchEntries,
                out var assetSearchError))
        {
            return AssetLookupSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {assetSearchError}",
                UcliCoreErrorCodes.InternalError);
        }

        if (!IndexCatalogContractValidator.TryProjectGuidPathEntries(
                payload.GuidPathEntries,
                "guidPathEntries",
                out var guidPathEntries,
                out var guidPathError))
        {
            return AssetLookupSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {guidPathError}",
                UcliCoreErrorCodes.InternalError);
        }

        if (!AssetLookupSnapshot.TryCreate(
                payload.GeneratedAtUtc,
                assetSearchEntries,
                guidPathEntries,
                out var snapshot,
                out var snapshotError))
        {
            return AssetLookupSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {snapshotError}",
                UcliCoreErrorCodes.InternalError);
        }

        return AssetLookupSnapshotFetchResult.Success(snapshot);
    }
}
