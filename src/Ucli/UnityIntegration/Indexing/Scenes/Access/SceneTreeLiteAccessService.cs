using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes.Access;

/// <summary> Implements scene-tree-lite access flow across read-index and source fallback paths. </summary>
internal sealed class SceneTreeLiteAccessService : ISceneTreeLiteAccessService
{
    private readonly IIndexCatalogReader indexCatalogReader;
    private readonly ISceneTreeLiteFreshnessEvaluator freshnessEvaluator;
    private readonly IMutationReadPostconditionStore mutationReadPostconditionStore;
    private readonly ISceneTreeLiteSourceRefreshService sourceRefreshService;

    /// <summary> Initializes a new instance of the <see cref="SceneTreeLiteAccessService" /> class. </summary>
    public SceneTreeLiteAccessService (
        IIndexCatalogReader indexCatalogReader,
        ISceneTreeLiteFreshnessEvaluator freshnessEvaluator,
        IMutationReadPostconditionStore mutationReadPostconditionStore,
        ISceneTreeLiteSourceRefreshService sourceRefreshService)
    {
        this.indexCatalogReader = indexCatalogReader ?? throw new ArgumentNullException(nameof(indexCatalogReader));
        this.freshnessEvaluator = freshnessEvaluator ?? throw new ArgumentNullException(nameof(freshnessEvaluator));
        this.mutationReadPostconditionStore = mutationReadPostconditionStore ?? throw new ArgumentNullException(nameof(mutationReadPostconditionStore));
        this.sourceRefreshService = sourceRefreshService ?? throw new ArgumentNullException(nameof(sourceRefreshService));
    }

    /// <inheritdoc />
    public async ValueTask<SceneTreeLiteReadResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string scenePath,
        int? depth,
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
        if (isLookupEligibleScene
            && !SceneTreeLiteAccessUtilities.TryEnsureCurrentAssetsSceneExists(project.UnityProjectRoot, normalizedScenePath, out errorMessage))
        {
            return SceneTreeLiteReadResult.Failure(errorMessage, IpcErrorCodes.InvalidArgument);
        }

        if (readIndexMode == ReadIndexMode.Disabled)
        {
            return await ReadFromSource(
                    project,
                    config,
                    command,
                    mode,
                    timeout,
                    readIndexMode,
                    normalizedScenePath,
                    depth,
                    "readIndex disabled by mode.",
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
                    readIndexMode,
                    normalizedScenePath,
                    depth,
                    "scene-tree-lite readIndex is unavailable for non-Assets scene paths.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var lookupResult = await indexCatalogReader.ReadSceneTreeLiteLookup(
                project.RepositoryRoot,
                project.ProjectFingerprint,
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
                    readIndexMode,
                    normalizedScenePath,
                    depth,
                    lookupResult.Error.Message,
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
                    readIndexMode,
                    normalizedScenePath,
                    depth,
                    readPostconditionEvaluation.FallbackReason!,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var freshnessResult = await freshnessEvaluator.Evaluate(
                project.UnityProjectRoot,
                normalizedScenePath,
                lookupResult.Value!.SourceInputsHash,
                readIndexMode,
                cancellationToken)
            .ConfigureAwait(false);
        if (freshnessResult.IsSuccess)
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

        if (!string.Equals(freshnessResult.Error!.Code, IpcErrorCodes.ReadIndexFreshRequired, StringComparison.Ordinal))
        {
            return SceneTreeLiteReadResult.Failure(freshnessResult.Error.Message, freshnessResult.Error.Code);
        }

        return await ReadFromSource(
                project,
                config,
                command,
                mode,
                timeout,
                readIndexMode,
                normalizedScenePath,
                depth,
                $"Existing scene-tree-lite index freshness is '{SceneTreeLiteAccessUtilities.DescribeFreshness(freshnessResult.Freshness)}'.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<SceneTreeLiteReadResult> ReadFromSource (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string normalizedScenePath,
        int? depth,
        string fallbackReason,
        CancellationToken cancellationToken)
    {
        var refreshResult = await sourceRefreshService.Refresh(
                project,
                config,
                command,
                mode,
                timeout,
                readIndexMode,
                normalizedScenePath,
                fallbackReason,
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