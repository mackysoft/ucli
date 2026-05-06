using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts;
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

    /// <summary> Initializes a new instance of the <see cref="SceneTreeLiteAccessService" /> class. </summary>
    public SceneTreeLiteAccessService (
        IReadIndexArtifactReader artifactReader,
        IReadIndexFreshnessEvaluator freshnessEvaluator,
        IMutationReadPostconditionStore mutationReadPostconditionStore,
        ISceneTreeLiteSourceRefreshService sourceRefreshService,
        ISceneTreeLiteSourceProbe sourceProbe)
    {
        this.artifactReader = artifactReader ?? throw new ArgumentNullException(nameof(artifactReader));
        this.freshnessEvaluator = freshnessEvaluator ?? throw new ArgumentNullException(nameof(freshnessEvaluator));
        this.mutationReadPostconditionStore = mutationReadPostconditionStore ?? throw new ArgumentNullException(nameof(mutationReadPostconditionStore));
        this.sourceRefreshService = sourceRefreshService ?? throw new ArgumentNullException(nameof(sourceRefreshService));
        this.sourceProbe = sourceProbe ?? throw new ArgumentNullException(nameof(sourceProbe));
    }

    /// <inheritdoc />
    public async ValueTask<SceneTreeLiteReadResult> Read (
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
            return SceneTreeLiteReadResult.Failure(errorMessage, IpcErrorCodes.InvalidArgument);
        }

        if (depth is < 0)
        {
            return SceneTreeLiteReadResult.Failure("Property 'depth' must be greater than or equal to 0.", IpcErrorCodes.InvalidArgument);
        }

        var isLookupEligibleScene = SceneTreeLiteAccessUtilities.IsLookupEligibleScenePath(normalizedScenePath);
        if (isLookupEligibleScene)
        {
            var sourceProbeResult = await sourceProbe.EnsureCurrentAssetsSceneExists(project, normalizedScenePath, cancellationToken).ConfigureAwait(false);
            if (!sourceProbeResult.IsSuccess)
            {
                return SceneTreeLiteReadResult.Failure(sourceProbeResult.ErrorMessage!, IpcErrorCodes.InvalidArgument);
            }
        }

        if (readIndexMode == ReadIndexMode.Disabled)
        {
            return await ReadFromSource(
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

        if (!isLookupEligibleScene)
        {
            return await ReadFromSource(
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

        var lookupResult = await artifactReader.ReadSceneTreeLiteLookup(
                project,
                normalizedScenePath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!lookupResult.IsSuccess)
        {
            if (string.Equals(lookupResult.Error!.Code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal))
            {
                return SceneTreeLiteReadResult.Failure(lookupResult.Error.Message, lookupResult.Error.Code);
            }

            return await ReadFromSource(
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

        var readPostconditionEvaluation = await MutationReadPostconditionAccessEvaluator.EvaluateSceneTreeLite(
                mutationReadPostconditionStore,
                project,
                normalizedScenePath,
                lookupResult.Value!.GeneratedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readPostconditionEvaluation.CanUseIndex)
        {
            return await ReadFromSource(
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

        var freshnessResult = await freshnessEvaluator.ObserveSceneTreeLite(
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
                    new SceneTreeLiteAccessInfo(
                        Used: true,
                        Hit: true,
                        Source: SceneTreeLiteSource.Index,
                        Freshness: freshnessResult.Freshness,
                        GeneratedAtUtc: lookupResult.Value.GeneratedAtUtc,
                        FallbackReason: null)),
                "Scene-tree-lite read completed.");
        }

        return await ReadFromSource(
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

    private async ValueTask<SceneTreeLiteReadResult> ReadFromSource (
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
        var refreshResult = await sourceRefreshService.Refresh(
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
            return SceneTreeLiteReadResult.Failure(refreshResult.Message, refreshResult.ErrorCode!);
        }

        var response = refreshResult.Response!;
        return SceneTreeLiteReadResult.Success(
            new SceneTreeLiteReadOutput(
                response.ScenePath,
                SceneTreeLiteAccessUtilities.TrimToDepth(response.Roots!, depth),
                new SceneTreeLiteAccessInfo(
                    Used: false,
                    Hit: true,
                    Source: SceneTreeLiteSource.Source,
                    Freshness: IndexFreshness.Fresh,
                    GeneratedAtUtc: response.GeneratedAtUtc,
                    FallbackReason: refreshResult.FallbackReason)),
            "Scene-tree-lite read completed.");
    }
}
