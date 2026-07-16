using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using UnityExecutionModeValue = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Implements scene-tree-lite access flow across read-index and source fallback paths. </summary>
internal sealed class SceneTreeLiteAccessService : ISceneTreeLiteAccessService
{
    private readonly IReadIndexArtifactReader artifactReader;
    private readonly IReadIndexFreshnessEvaluator freshnessEvaluator;
    private readonly IMutationReadPostconditionStore mutationReadPostconditionStore;
    private readonly ISceneTreeLiteSourceRefreshService sourceRefreshService;
    private readonly ISceneTreeLiteSourceProbe sourceProbe;
    private readonly ISceneTreeLiteDirtySourceProbeService dirtySourceProbeService;

    /// <summary> Initializes a new instance of the <see cref="SceneTreeLiteAccessService" /> class. </summary>
    public SceneTreeLiteAccessService (
        IReadIndexArtifactReader artifactReader,
        IReadIndexFreshnessEvaluator freshnessEvaluator,
        IMutationReadPostconditionStore mutationReadPostconditionStore,
        ISceneTreeLiteSourceRefreshService sourceRefreshService,
        ISceneTreeLiteSourceProbe sourceProbe,
        ISceneTreeLiteDirtySourceProbeService dirtySourceProbeService)
    {
        this.artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
        this.freshnessEvaluator = freshnessEvaluator ?? throw new ArgumentNullException(nameof(freshnessEvaluator));
        this.mutationReadPostconditionStore = mutationReadPostconditionStore ?? throw new ArgumentNullException(nameof(mutationReadPostconditionStore));
        this.sourceRefreshService = sourceRefreshService ?? throw new ArgumentNullException(nameof(sourceRefreshService));
        this.sourceProbe = sourceProbe ?? throw new ArgumentNullException(nameof(sourceProbe));
        this.dirtySourceProbeService = dirtySourceProbeService ?? throw new ArgumentNullException(nameof(dirtySourceProbeService));
    }

    /// <inheritdoc />
    public async ValueTask<SceneTreeLiteReadResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        UnityScenePath scenePath,
        int? depth,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(scenePath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        if (depth is < 0)
        {
            return SceneTreeLiteReadResult.Failure("Property 'depth' must be greater than or equal to 0.", UcliCoreErrorCodes.InvalidArgument);
        }

        if (SceneAssetPath.TryParse(scenePath.Value, out var lookupScenePath))
        {
            var sourceProbeResult = await sourceProbe.EnsureCurrentAssetsSceneExistsAsync(project, lookupScenePath, cancellationToken).ConfigureAwait(false);
            if (!sourceProbeResult.IsSuccess)
            {
                return SceneTreeLiteReadResult.Failure(sourceProbeResult.ErrorMessage!, UcliCoreErrorCodes.InvalidArgument);
            }
        }

        if (readIndexMode == ReadIndexMode.Disabled)
        {
            return await ReadFromSourceAsync(
                    project,
                    config,
                    command,
                    mode,
                    timeout,
                    scenePath,
                    depth,
                    "readIndex disabled by mode.",
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var dirtySourceProbeResult = await dirtySourceProbeService.ProbeAsync(
                project,
                config,
                command,
                mode,
                timeout,
                scenePath,
                cancellationToken)
            .ConfigureAwait(false);
        if (dirtySourceProbeResult.Snapshot is { } dirtySourceSnapshot)
        {
            return CreateSourceReadResult(
                dirtySourceSnapshot,
                depth,
                dirtySourceProbeResult.FallbackReason);
        }

        if (lookupScenePath is null)
        {
            return await ReadFromSourceAsync(
                    project,
                    config,
                    command,
                    mode,
                    timeout,
                    scenePath,
                    depth,
                    "scene-tree-lite readIndex is unavailable for non-Assets scene paths.",
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var lookupResult = await artifactReader.ReadSceneTreeLiteLookupAsync(
                project,
                lookupScenePath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!lookupResult.IsSuccess)
        {
            if (lookupResult.Error!.Code == UcliCoreErrorCodes.InvalidArgument)
            {
                return SceneTreeLiteReadResult.Failure(lookupResult.Error.Message, lookupResult.Error.Code);
            }

            return await ReadFromSourceAsync(
                    project,
                    config,
                    command,
                    mode,
                    timeout,
                    scenePath,
                    depth,
                    lookupResult.Error.Message,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var lookupSnapshot = lookupResult.Value!;
        var readPostconditionEvaluation = await MutationReadPostconditionAccessEvaluator.EvaluateSceneTreeLiteAsync(
                mutationReadPostconditionStore,
                project,
                scenePath,
                lookupSnapshot.GeneratedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readPostconditionEvaluation.CanUseIndex)
        {
            return await ReadFromSourceAsync(
                    project,
                    config,
                    command,
                    mode,
                    timeout,
                    scenePath,
                    depth,
                    readPostconditionEvaluation.FallbackReason!,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var freshnessResult = await freshnessEvaluator.ObserveSceneTreeLiteAsync(
                project,
                lookupScenePath,
                lookupSnapshot.SourceInputsHash,
                cancellationToken)
            .ConfigureAwait(false);
        if (!freshnessResult.IsSuccess)
        {
            return SceneTreeLiteReadResult.Failure(
                freshnessResult.Error!.Message,
                freshnessResult.Error.Code);
        }

        if (readIndexMode == ReadIndexMode.AllowStale || freshnessResult.Freshness == IndexFreshness.Fresh)
        {
            return SceneTreeLiteReadResult.Success(
                new SceneTreeLiteReadOutput(
                    scenePath,
                    SceneTreeLiteAccessUtilities.TrimToDepth(lookupSnapshot.Roots, depth),
                    new SceneTreeSourceState(SceneTreeSourceStateKind.ReadIndex, isDirty: false),
                    new SceneTreeLiteAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: SceneTreeLiteSource.Index,
                        Freshness: freshnessResult.Freshness,
                        GeneratedAtUtc: lookupSnapshot.GeneratedAtUtc,
                        FallbackReason: null)),
                "Scene-tree-lite read completed.");
        }

        return await ReadFromSourceAsync(
                project,
                config,
                command,
                mode,
                timeout,
                scenePath,
                depth,
                $"Existing scene-tree-lite index freshness is '{ContractLiteralCodec.ToValue(freshnessResult.Freshness)}'.",
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<SceneTreeLiteReadResult> ReadFromSourceAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        UnityScenePath scenePath,
        int? depth,
        string fallbackReason,
        bool failFast,
        CancellationToken cancellationToken)
    {
        var refreshResult = await sourceRefreshService.RefreshAsync(
                project,
                config,
                command,
                mode,
                timeout,
                scenePath,
                fallbackReason,
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
        if (!refreshResult.IsSuccess)
        {
            return SceneTreeLiteReadResult.Failure(refreshResult.Message, refreshResult.ErrorCode!);
        }

        var snapshot = refreshResult.Snapshot!;

        return CreateSourceReadResult(snapshot, depth, refreshResult.FallbackReason);
    }

    private static SceneTreeLiteReadResult CreateSourceReadResult (
        SceneTreeLiteSourceSnapshot snapshot,
        int? depth,
        string? fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return SceneTreeLiteReadResult.Success(
            new SceneTreeLiteReadOutput(
                snapshot.ScenePath,
                SceneTreeLiteAccessUtilities.TrimToDepth(snapshot.Roots, depth),
                snapshot.SourceState,
                new SceneTreeLiteAccessInfo(
                    Used: false,
                    Hit: true,
                    Source: SceneTreeLiteSource.Source,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: snapshot.GeneratedAtUtc,
                    FallbackReason: fallbackReason)),
            "Scene-tree-lite read completed.");
    }

}
