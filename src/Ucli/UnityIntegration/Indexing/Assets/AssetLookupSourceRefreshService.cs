using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets.Access;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets;

/// <summary> Reads live asset lookup snapshots and refreshes persisted lookup artifacts on a best-effort basis. </summary>
internal sealed class AssetLookupSourceRefreshService : IAssetLookupSourceRefreshService
{
    private const int MaxSnapshotStabilityAttempts = 2;

    private const string InputFingerprintFailureMessage
        = "Failed to persist refreshed asset lookup readIndex because input fingerprint could not be computed.";

    private const string InputInstabilityFailureMessage
        = "Failed to persist refreshed asset lookup readIndex because project inputs changed while the snapshot was being read.";

    private const string RetrySnapshotReadFailurePrefix
        = "Failed to persist refreshed asset lookup readIndex because retry snapshot read failed.";

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
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string fallbackReason,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackReason);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        IpcIndexAssetsReadResponse? response = null;
        string? persistFailure = null;
        for (var attempt = 0; attempt < MaxSnapshotStabilityAttempts; attempt++)
        {
            var attemptResult = await TryReadAndPersistLookupArtifacts(
                    project,
                    config,
                    command,
                    mode,
                    timeout,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!attemptResult.FetchResult.IsSuccess)
            {
                if (response != null)
                {
                    persistFailure = AssetLookupAccessUtilities.CombineFallbackReasons(
                        persistFailure,
                        $"{RetrySnapshotReadFailurePrefix} {attemptResult.FetchResult.Message}");
                    break;
                }

                return AssetLookupRefreshResult.Failure(attemptResult.FetchResult.Message, attemptResult.FetchResult.ErrorCode!);
            }

            response = attemptResult.FetchResult.Response!;
            persistFailure = attemptResult.PersistFailure;
            if (!attemptResult.ShouldRetry)
            {
                break;
            }
        }

        var combinedFallbackReason = AssetLookupAccessUtilities.CombineFallbackReasons(
            readIndexMode == ReadIndexMode.Disabled ? "readIndex disabled by mode." : fallbackReason,
            persistFailure);
        return AssetLookupRefreshResult.Success(response!, combinedFallbackReason);
    }

    private async ValueTask<(AssetLookupSnapshotFetchResult FetchResult, string? PersistFailure, bool ShouldRetry)> TryReadAndPersistLookupArtifacts (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshotBeforeRead = await indexInputFingerprintCalculator.TryCompute(
                project.UnityProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);

        var fetchResult = await assetLookupSnapshotReader.Read(
                project,
                config,
                command,
                mode,
                timeout,
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
        if (!fetchResult.IsSuccess)
        {
            return (fetchResult, null, false);
        }

        if (snapshotBeforeRead == null)
        {
            return (fetchResult, InputFingerprintFailureMessage, false);
        }

        var snapshotAfterRead = await indexInputFingerprintCalculator.TryCompute(
                project.UnityProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (snapshotAfterRead == null)
        {
            return (fetchResult, InputFingerprintFailureMessage, false);
        }

        // NOTE: Stamp sourceInputsHash only when the same input snapshot is observed
        // before and after the live Unity read. Otherwise a stale lookup can be marked fresh.
        if (!Equals(snapshotBeforeRead, snapshotAfterRead))
        {
            return (fetchResult, InputInstabilityFailureMessage, true);
        }

        try
        {
            await assetLookupStore.Write(
                    project.RepositoryRoot,
                    project.ProjectFingerprint,
                    fetchResult.Response!.GeneratedAtUtc,
                    fetchResult.Response.AssetSearchEntries!.ToArray(),
                    fetchResult.Response.GuidPathEntries!.ToArray(),
                    snapshotAfterRead,
                    cancellationToken)
                .ConfigureAwait(false);
            return (fetchResult, null, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return (fetchResult, $"Failed to persist refreshed asset lookup readIndex. {exception.Message}", false);
        }
    }
}
