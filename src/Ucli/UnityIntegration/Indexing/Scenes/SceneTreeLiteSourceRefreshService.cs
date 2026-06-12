using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
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

        if (!UnityAssetPathContract.IsNormalizedSceneAssetPath(scenePath))
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
                return SceneTreeLiteRefreshResult.Failure(fetchResult.Message, fetchResult.ErrorCode!.Value);
            }

            var liveOnlyFallbackReason = ReadIndexAccessUtilities.CombineFallbackReasons(
                fallbackReason,
                null);
            return SceneTreeLiteRefreshResult.Success(fetchResult.Response!, liveOnlyFallbackReason);
        }

        IpcIndexSceneTreeLiteReadResponse? response = null;
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

                return SceneTreeLiteRefreshResult.Failure(attemptResult.FetchResult.Message, attemptResult.FetchResult.ErrorCode!.Value);
            }

            response = attemptResult.FetchResult.Response!;
            persistFailure = attemptResult.PersistFailure;
            if (!attemptResult.ShouldRetry)
            {
                break;
            }
        }

        var combinedFallbackReason = ReadIndexAccessUtilities.CombineFallbackReasons(
            fallbackReason,
            persistFailure);
        return SceneTreeLiteRefreshResult.Success(response!, combinedFallbackReason);
    }

    private async ValueTask<(SceneTreeLiteSnapshotFetchResult FetchResult, string? PersistFailure, bool ShouldRetry)> TryReadAndPersistLookupArtifactAsync (
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

        var sourceHashBeforeRead = await sceneSourceHashProvider.TryComputeAsync(project, scenePath, cancellationToken).ConfigureAwait(false);
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

        if (sourceHashBeforeRead == null)
        {
            return (fetchResult, SourceHashFailureMessage, false);
        }

        if (SceneTreeSourceStatePolicy.IsDirtyLiveSource(fetchResult.Response!.SourceState))
        {
            return (fetchResult, DirtyLiveSourcePersistenceSkippedMessage, false);
        }

        var sourceHashAfterRead = await sceneSourceHashProvider.TryComputeAsync(project, scenePath, cancellationToken).ConfigureAwait(false);
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
            await artifactWriter.WriteSceneTreeLiteAsync(
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
