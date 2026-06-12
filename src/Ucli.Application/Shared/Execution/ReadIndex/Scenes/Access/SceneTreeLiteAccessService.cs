using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
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
        string scenePath,
        int? depth,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        if (!SceneTreeLiteAccessUtilities.TryNormalizeScenePath(scenePath, out var normalizedScenePath, out var errorMessage))
        {
            return SceneTreeLiteReadResult.Failure(errorMessage, UcliCoreErrorCodes.InvalidArgument);
        }

        if (depth is < 0)
        {
            return SceneTreeLiteReadResult.Failure("Property 'depth' must be greater than or equal to 0.", UcliCoreErrorCodes.InvalidArgument);
        }

        var isLookupEligibleScene = UnityAssetPathContract.IsNormalizedSceneAssetPath(normalizedScenePath);
        if (isLookupEligibleScene)
        {
            var sourceProbeResult = await sourceProbe.EnsureCurrentAssetsSceneExistsAsync(project, normalizedScenePath, cancellationToken).ConfigureAwait(false);
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
                    normalizedScenePath,
                    depth,
                    "readIndex disabled by mode.",
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (isLookupEligibleScene)
        {
            var dirtySourceProbeResult = await dirtySourceProbeService.ProbeAsync(
                    project,
                    config,
                    command,
                    mode,
                    timeout,
                    normalizedScenePath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (dirtySourceProbeResult.HasDirtySource)
            {
                return CreateSourceReadResult(
                    dirtySourceProbeResult.Response!,
                    depth,
                    dirtySourceProbeResult.FallbackReason);
            }
        }

        if (!isLookupEligibleScene)
        {
            return await ReadFromSourceAsync(
                    project,
                    config,
                    command,
                    mode,
                    timeout,
                    normalizedScenePath,
                    depth,
                    "scene-tree-lite readIndex is unavailable for non-Assets scene paths.",
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var lookupResult = await artifactReader.ReadSceneTreeLiteLookupAsync(
                project,
                normalizedScenePath,
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
                    normalizedScenePath,
                    depth,
                    lookupResult.Error.Message,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var readPostconditionEvaluation = await MutationReadPostconditionAccessEvaluator.EvaluateSceneTreeLiteAsync(
                mutationReadPostconditionStore,
                project,
                normalizedScenePath,
                lookupResult.Value!.GeneratedAtUtc,
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
                    normalizedScenePath,
                    depth,
                    readPostconditionEvaluation.FallbackReason!,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var freshnessResult = await freshnessEvaluator.ObserveSceneTreeLiteAsync(
                project,
                normalizedScenePath,
                lookupResult.Value!.SourceInputsHash,
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
                    lookupResult.Value.ScenePath!,
                    SceneTreeLiteAccessUtilities.TrimToDepth(lookupResult.Value.Roots!, depth),
                    new SceneTreeSourceState(SceneTreeSourceStateKind.ReadIndex, isDirty: false),
                    new SceneTreeLiteAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: SceneTreeLiteSource.Index,
                        Freshness: freshnessResult.Freshness,
                        GeneratedAtUtc: lookupResult.Value.GeneratedAtUtc,
                        FallbackReason: null)),
                "Scene-tree-lite read completed.");
        }

        return await ReadFromSourceAsync(
                project,
                config,
                command,
                mode,
                timeout,
                normalizedScenePath,
                depth,
                $"Existing scene-tree-lite index freshness is '{ReadIndexAccessUtilities.DescribeFreshness(freshnessResult.Freshness)}'.",
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
        string normalizedScenePath,
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
                normalizedScenePath,
                fallbackReason,
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
        if (!refreshResult.IsSuccess)
        {
            return SceneTreeLiteReadResult.Failure(refreshResult.Message, refreshResult.ErrorCode!.Value);
        }

        return CreateSourceReadResult(
            refreshResult.Response!,
            depth,
            refreshResult.FallbackReason);
    }

    private static SceneTreeLiteReadResult CreateSourceReadResult (
        IpcIndexSceneTreeLiteReadResponse response,
        int? depth,
        string? fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(response);
        return SceneTreeLiteReadResult.Success(
            new SceneTreeLiteReadOutput(
                response.ScenePath,
                SceneTreeLiteAccessUtilities.TrimToDepth(response.Roots!, depth),
                response.SourceState,
                new SceneTreeLiteAccessInfo(
                    Used: false,
                    Hit: true,
                    Source: SceneTreeLiteSource.Source,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: response.GeneratedAtUtc,
                    FallbackReason: fallbackReason)),
            "Scene-tree-lite read completed.");
    }

}
