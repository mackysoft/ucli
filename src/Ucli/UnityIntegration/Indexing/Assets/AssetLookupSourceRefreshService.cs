using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

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
    private readonly IReadIndexArtifactWriter artifactWriter;
    private readonly IReadIndexInputFingerprintProvider inputFingerprintProvider;

    /// <summary> Initializes a new instance of the <see cref="AssetLookupSourceRefreshService" /> class. </summary>
    public AssetLookupSourceRefreshService (
        IAssetLookupSnapshotReader assetLookupSnapshotReader,
        IReadIndexArtifactWriter artifactWriter,
        IReadIndexInputFingerprintProvider inputFingerprintProvider)
    {
        this.assetLookupSnapshotReader = assetLookupSnapshotReader ?? throw new ArgumentNullException(nameof(assetLookupSnapshotReader));
        this.artifactWriter = artifactWriter ?? throw new ArgumentNullException(nameof(artifactWriter));
        this.inputFingerprintProvider = inputFingerprintProvider ?? throw new ArgumentNullException(nameof(inputFingerprintProvider));
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
                    persistFailure = ReadIndexAccessUtilities.CombineFallbackReasons(
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

        var combinedFallbackReason = ReadIndexAccessUtilities.CombineFallbackReasons(
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

        var snapshotBeforeRead = await inputFingerprintProvider.TryCompute(
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

        var snapshotAfterRead = await inputFingerprintProvider.TryCompute(
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
            await artifactWriter.WriteAssetLookups(
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
