using MackySoft.Ucli.Assets.Access;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Assets;

/// <summary> Reads live asset lookup snapshots and refreshes persisted lookup artifacts on a best-effort basis. </summary>
internal sealed class AssetLookupSourceRefreshService : IAssetLookupSourceRefreshService
{
    private readonly IAssetLookupSnapshotReader assetLookupSnapshotReader;
    private readonly IAssetLookupStore assetLookupStore;
    private readonly IIndexInputFingerprintCalculator indexInputFingerprintCalculator;

    /// <summary> Initializes a new instance of the <see cref="AssetLookupSourceRefreshService" /> class. </summary>
    public AssetLookupSourceRefreshService (
        IAssetLookupSnapshotReader assetLookupSnapshotReader,
        IAssetLookupStore assetLookupStore,
        IIndexInputFingerprintCalculator indexInputFingerprintCalculator)
    {
        this.assetLookupSnapshotReader = assetLookupSnapshotReader ?? throw new ArgumentNullException(nameof(assetLookupSnapshotReader));
        this.assetLookupStore = assetLookupStore ?? throw new ArgumentNullException(nameof(assetLookupStore));
        this.indexInputFingerprintCalculator = indexInputFingerprintCalculator ?? throw new ArgumentNullException(nameof(indexInputFingerprintCalculator));
    }

    /// <inheritdoc />
    public async ValueTask<AssetLookupRefreshResult> Refresh (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        string? mode,
        string? timeout,
        ReadIndexMode readIndexMode,
        string fallbackReason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackReason);
        cancellationToken.ThrowIfCancellationRequested();

        var fetchResult = await assetLookupSnapshotReader.Read(
                project,
                config,
                command,
                mode,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!fetchResult.IsSuccess)
        {
            return AssetLookupRefreshResult.Failure(fetchResult.Message, fetchResult.ErrorCode!);
        }

        var response = fetchResult.Response!;
        var persistFailure = await TryPersistLookupArtifacts(
                project,
                response,
                cancellationToken)
            .ConfigureAwait(false);

        var combinedFallbackReason = AssetLookupAccessUtilities.CombineFallbackReasons(
            readIndexMode == ReadIndexMode.Disabled ? "readIndex disabled by mode." : fallbackReason,
            persistFailure);
        return AssetLookupRefreshResult.Success(response, combinedFallbackReason);
    }

    private async ValueTask<string?> TryPersistLookupArtifacts (
        ResolvedUnityProjectContext project,
        IpcIndexAssetsReadResponse response,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inputSnapshot = await indexInputFingerprintCalculator.TryCompute(
                project.UnityProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (inputSnapshot == null)
        {
            return "Failed to persist refreshed asset lookup readIndex because input fingerprint could not be computed.";
        }

        try
        {
            await assetLookupStore.Write(
                    project.RepositoryRoot,
                    project.ProjectFingerprint,
                    response.GeneratedAtUtc,
                    response.AssetSearchEntries!.ToArray(),
                    response.GuidPathEntries!.ToArray(),
                    inputSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return $"Failed to persist refreshed asset lookup readIndex. {exception.Message}";
        }
    }
}