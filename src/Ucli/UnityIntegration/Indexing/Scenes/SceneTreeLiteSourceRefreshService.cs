using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Index;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes.Access;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Reads persisted-preview scene-tree-lite snapshots and refreshes persisted lookup artifacts on a best-effort basis. </summary>
internal sealed class SceneTreeLiteSourceRefreshService : ISceneTreeLiteSourceRefreshService
{
    private const int MaxSnapshotStabilityAttempts = 2;

    private const string SourceHashFailureMessage
        = "Failed to persist refreshed scene-tree-lite readIndex because source hash could not be computed.";

    private const string SourceInstabilityFailureMessage
        = "Failed to persist refreshed scene-tree-lite readIndex because scene source changed while the snapshot was being read.";

    private const string RetrySnapshotReadFailurePrefix
        = "Failed to persist refreshed scene-tree-lite readIndex because retry snapshot read failed.";

    private readonly ISceneTreeLiteSnapshotReader snapshotReader;
    private readonly ISceneTreeLiteStore store;
    private readonly ISceneTreeLiteSourceHashCalculator sourceHashCalculator;

    /// <summary> Initializes a new instance of the <see cref="SceneTreeLiteSourceRefreshService" /> class. </summary>
    public SceneTreeLiteSourceRefreshService (
        ISceneTreeLiteSnapshotReader snapshotReader,
        ISceneTreeLiteStore store,
        ISceneTreeLiteSourceHashCalculator sourceHashCalculator)
    {
        this.snapshotReader = snapshotReader ?? throw new ArgumentNullException(nameof(snapshotReader));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.sourceHashCalculator = sourceHashCalculator ?? throw new ArgumentNullException(nameof(sourceHashCalculator));
    }

    /// <inheritdoc />
    public async ValueTask<SceneTreeLiteRefreshResult> Refresh (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string scenePath,
        string fallbackReason,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackReason);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        if (!SceneTreeLiteAccessUtilities.IsLookupEligibleScenePath(scenePath))
        {
            var fetchResult = await snapshotReader.Read(project, config, command, mode, timeout, scenePath, failFast, cancellationToken).ConfigureAwait(false);
            if (!fetchResult.IsSuccess)
            {
                return SceneTreeLiteRefreshResult.Failure(fetchResult.Message, fetchResult.ErrorCode!);
            }

            var liveOnlyFallbackReason = SceneTreeLiteAccessUtilities.CombineFallbackReasons(
                readIndexMode == ReadIndexMode.Disabled ? "readIndex disabled by mode." : fallbackReason,
                null);
            return SceneTreeLiteRefreshResult.Success(fetchResult.Response!, liveOnlyFallbackReason);
        }

        IpcIndexSceneTreeLiteReadResponse? response = null;
        string? persistFailure = null;
        for (var attempt = 0; attempt < MaxSnapshotStabilityAttempts; attempt++)
        {
            var attemptResult = await TryReadAndPersistLookupArtifact(
                    project,
                    config,
                    command,
                    mode,
                    timeout,
                    scenePath,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!attemptResult.FetchResult.IsSuccess)
            {
                if (response != null)
                {
                    persistFailure = SceneTreeLiteAccessUtilities.CombineFallbackReasons(
                        persistFailure,
                        $"{RetrySnapshotReadFailurePrefix} {attemptResult.FetchResult.Message}");
                    break;
                }

                return SceneTreeLiteRefreshResult.Failure(attemptResult.FetchResult.Message, attemptResult.FetchResult.ErrorCode!);
            }

            response = attemptResult.FetchResult.Response!;
            persistFailure = attemptResult.PersistFailure;
            if (!attemptResult.ShouldRetry)
            {
                break;
            }
        }

        var combinedFallbackReason = SceneTreeLiteAccessUtilities.CombineFallbackReasons(
            readIndexMode == ReadIndexMode.Disabled ? "readIndex disabled by mode." : fallbackReason,
            persistFailure);
        return SceneTreeLiteRefreshResult.Success(response!, combinedFallbackReason);
    }

    private async ValueTask<(SceneTreeLiteSnapshotFetchResult FetchResult, string? PersistFailure, bool ShouldRetry)> TryReadAndPersistLookupArtifact (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        string scenePath,
        bool failFast,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sourceHashBeforeRead = await sourceHashCalculator.TryCompute(project.UnityProjectRoot, scenePath, cancellationToken).ConfigureAwait(false);
        var fetchResult = await snapshotReader.Read(project, config, command, mode, timeout, scenePath, failFast, cancellationToken).ConfigureAwait(false);
        if (!fetchResult.IsSuccess)
        {
            return (fetchResult, null, false);
        }

        if (sourceHashBeforeRead == null)
        {
            return (fetchResult, SourceHashFailureMessage, false);
        }

        var sourceHashAfterRead = await sourceHashCalculator.TryCompute(project.UnityProjectRoot, scenePath, cancellationToken).ConfigureAwait(false);
        if (sourceHashAfterRead == null)
        {
            return (fetchResult, SourceHashFailureMessage, false);
        }

        if (!string.Equals(sourceHashBeforeRead, sourceHashAfterRead, StringComparison.Ordinal))
        {
            return (fetchResult, SourceInstabilityFailureMessage, true);
        }

        try
        {
            await store.Write(
                    project.RepositoryRoot,
                    project.ProjectFingerprint,
                    fetchResult.Response!.GeneratedAtUtc,
                    fetchResult.Response.ScenePath,
                    fetchResult.Response.Roots!,
                    sourceHashAfterRead,
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
            return (fetchResult, $"Failed to persist refreshed scene-tree-lite readIndex. {exception.Message}", false);
        }
    }
}
