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
    public async ValueTask<AssetLookupSnapshotFetchResult> Read (
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

        var executionResult = await ipcRequestExecutor.Execute(
                command,
                mode,
                timeout,
                config,
                project,
                new UnityRequestPayload.Raw(
                    IpcMethodNames.IndexAssetsRead,
                    IpcPayloadCodec.SerializeToElement(new IpcIndexAssetsReadRequest(failFast))),
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

        if (response.HasFailureStatus || response.Errors.Count != 0)
        {
            var firstError = response.Errors.FirstOrDefault();
            if (firstError != null)
            {
                return AssetLookupSnapshotFetchResult.Failure(firstError.Message, firstError.Code);
            }

            return AssetLookupSnapshotFetchResult.Failure(
                $"{responseSourceName} failed with an error status.",
                IpcErrorCodes.InternalError);
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcIndexAssetsReadResponse payload, out var payloadError))
        {
            return AssetLookupSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {payloadError.Message}",
                IpcErrorCodes.InternalError);
        }

        if (!IndexCatalogContractValidator.TryValidateAssetSearchEntries(payload.AssetSearchEntries, "assetSearchEntries", out var assetSearchError))
        {
            return AssetLookupSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {assetSearchError}",
                IpcErrorCodes.InternalError);
        }

        if (!IndexCatalogContractValidator.TryValidateGuidPathEntries(payload.GuidPathEntries, "guidPathEntries", out var guidPathError))
        {
            return AssetLookupSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. {guidPathError}",
                IpcErrorCodes.InternalError);
        }

        if (!AreSortedByAssetPath(payload.AssetSearchEntries!)
            || !AreSortedByAssetPath(payload.GuidPathEntries!))
        {
            return AssetLookupSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. Lookup entries must be sorted by assetPath.",
                IpcErrorCodes.InternalError);
        }

        if (!GuidPathEntriesMatchAssetSearchEntries(payload.AssetSearchEntries!, payload.GuidPathEntries!))
        {
            return AssetLookupSnapshotFetchResult.Failure(
                $"{responseSourceName} payload is invalid. guidPathEntries must be represented in assetSearchEntries.",
                IpcErrorCodes.InternalError);
        }

        return AssetLookupSnapshotFetchResult.Success(payload);
    }

    private static bool AreSortedByAssetPath (IReadOnlyList<IndexAssetSearchEntryJsonContract> entries)
    {
        for (var i = 1; i < entries.Count; i++)
        {
            if (StringComparer.Ordinal.Compare(entries[i - 1].AssetPath, entries[i].AssetPath) > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreSortedByAssetPath (IReadOnlyList<IndexGuidPathEntryJsonContract> entries)
    {
        for (var i = 1; i < entries.Count; i++)
        {
            if (StringComparer.Ordinal.Compare(entries[i - 1].AssetPath, entries[i].AssetPath) > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool GuidPathEntriesMatchAssetSearchEntries (
        IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries,
        IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries)
    {
        var assetSearchIndex = 0;
        for (var i = 0; i < guidPathEntries.Count; i++)
        {
            var guidPathEntry = guidPathEntries[i];
            while (assetSearchIndex < assetSearchEntries.Count
                && StringComparer.Ordinal.Compare(assetSearchEntries[assetSearchIndex].AssetPath, guidPathEntry.AssetPath) < 0)
            {
                assetSearchIndex++;
            }

            if (assetSearchIndex >= assetSearchEntries.Count)
            {
                return false;
            }

            if (!string.Equals(assetSearchEntries[assetSearchIndex].AssetPath, guidPathEntry.AssetPath, StringComparison.Ordinal)
                || !string.Equals(assetSearchEntries[assetSearchIndex].AssetGuid, guidPathEntry.AssetGuid, StringComparison.Ordinal))
            {
                return false;
            }

            assetSearchIndex++;
        }

        return true;
    }
}
