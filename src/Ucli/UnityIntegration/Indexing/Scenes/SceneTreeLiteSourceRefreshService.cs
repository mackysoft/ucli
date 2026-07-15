using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Projection;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Reads scene-tree-lite source snapshots and refreshes persisted lookup artifacts on a best-effort basis. </summary>
internal sealed class SceneTreeLiteSourceRefreshService : ISceneTreeLiteSourceRefreshService
{
    private const int MaxSnapshotStabilityAttempts = 2;

    private const string SourceHashFailureMessage
        = "Failed to persist refreshed scene-tree-lite readIndex because source hash could not be computed.";

    private const string SourceInstabilityFailureMessage
        = "Failed to persist refreshed scene-tree-lite readIndex because scene source changed while the snapshot was being read.";

    private const string RetrySnapshotReadFailurePrefix
        = "Failed to persist refreshed scene-tree-lite readIndex because retry snapshot read failed.";

    private const string DirtyLiveSourcePersistenceSkippedMessage
        = "Skipped persisting refreshed scene-tree-lite readIndex because source scene is dirty live editor state.";

    private readonly ISceneTreeLiteSnapshotReader snapshotReader;
    private readonly IReadIndexArtifactWriter artifactWriter;
    private readonly IReadIndexSceneSourceHashProvider sceneSourceHashProvider;

    /// <summary> Initializes a new instance of the <see cref="SceneTreeLiteSourceRefreshService" /> class. </summary>
    public SceneTreeLiteSourceRefreshService (
        ISceneTreeLiteSnapshotReader snapshotReader,
        IReadIndexArtifactWriter artifactWriter,
        IReadIndexSceneSourceHashProvider sceneSourceHashProvider)
    {
        this.snapshotReader = snapshotReader ?? throw new ArgumentNullException(nameof(snapshotReader));
        this.artifactWriter = artifactWriter ?? throw new ArgumentNullException(nameof(artifactWriter));
        this.sceneSourceHashProvider = sceneSourceHashProvider ?? throw new ArgumentNullException(nameof(sceneSourceHashProvider));
    }

    /// <inheritdoc />
    public async ValueTask<SceneTreeLiteRefreshResult> RefreshAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UnityScenePath scenePath,
        string fallbackReason,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(scenePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackReason);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        if (!SceneAssetPath.TryParse(scenePath.Value, out var indexScenePath))
        {
            var fetchResult = await snapshotReader.ReadAsync(
                    project,
                    config,
                    command,
                    mode,
                    timeout,
                    scenePath,
                    failFast,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!fetchResult.IsSuccess)
            {
                return SceneTreeLiteRefreshResult.Failure(fetchResult.Message, fetchResult.ErrorCode!);
            }

            var liveOnlyFallbackReason = ReadIndexAccessUtilities.CombineFallbackReasons(
                fallbackReason,
                null);
            return SceneTreeLiteRefreshResult.Success(fetchResult.Snapshot!, liveOnlyFallbackReason);
        }

        SceneTreeLiteSourceSnapshot? snapshot = null;
        string? persistFailure = null;
        for (var attempt = 0; attempt < MaxSnapshotStabilityAttempts; attempt++)
        {
            var attemptResult = await TryReadAndPersistLookupArtifactAsync(
                    project,
                    config,
                    command,
                    mode,
                    timeout,
                    scenePath,
                    indexScenePath,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!attemptResult.FetchResult.IsSuccess)
            {
                if (snapshot != null)
                {
                    persistFailure = ReadIndexAccessUtilities.CombineFallbackReasons(
                        persistFailure,
                        $"{RetrySnapshotReadFailurePrefix} {attemptResult.FetchResult.Message}");
                    break;
                }

                return SceneTreeLiteRefreshResult.Failure(attemptResult.FetchResult.Message, attemptResult.FetchResult.ErrorCode!);
            }

            snapshot = attemptResult.FetchResult.Snapshot!;
            persistFailure = attemptResult.PersistFailure;
            if (!attemptResult.ShouldRetry)
            {
                break;
            }
        }

        var combinedFallbackReason = ReadIndexAccessUtilities.CombineFallbackReasons(
            fallbackReason,
            persistFailure);
        return SceneTreeLiteRefreshResult.Success(snapshot!, combinedFallbackReason);
    }

    private async ValueTask<(SceneTreeLiteSnapshotFetchResult FetchResult, string? PersistFailure, bool ShouldRetry)> TryReadAndPersistLookupArtifactAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UnityScenePath scenePath,
        SceneAssetPath indexScenePath,
        bool failFast,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sourceHashBeforeRead = await sceneSourceHashProvider.TryComputeAsync(project, indexScenePath, cancellationToken).ConfigureAwait(false);
        var fetchResult = await snapshotReader.ReadAsync(
                project,
                config,
                command,
                mode,
                timeout,
                scenePath,
                failFast,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!fetchResult.IsSuccess)
        {
            return (fetchResult, null, false);
        }

        var snapshot = fetchResult.Snapshot!;

        if (sourceHashBeforeRead == null)
        {
            return (fetchResult, SourceHashFailureMessage, false);
        }

        if (SceneTreeSourceStatePolicy.IsDirtyLiveSource(snapshot.SourceState))
        {
            return (fetchResult, DirtyLiveSourcePersistenceSkippedMessage, false);
        }

        var sourceHashAfterRead = await sceneSourceHashProvider.TryComputeAsync(project, indexScenePath, cancellationToken).ConfigureAwait(false);
        if (sourceHashAfterRead == null)
        {
            return (fetchResult, SourceHashFailureMessage, false);
        }

        if (sourceHashBeforeRead != sourceHashAfterRead)
        {
            return (fetchResult, SourceInstabilityFailureMessage, true);
        }

        try
        {
            await artifactWriter.WriteSceneTreeLiteAsync(
                    project.RepositoryRoot,
                    project.ProjectFingerprint,
                    snapshot.GeneratedAtUtc,
                    indexScenePath,
                    ReadIndexJsonContractMapper.ToJsonContracts(snapshot.Roots),
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
