using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the <c>query go describe</c> CLI command entry point. </summary>
internal sealed class QueryGoDescribeCommand
{
    private const int DefaultDepth = 0;

    private static readonly IpcExecuteStepId OperationId = new("go.describe");

    private readonly IQueryService queryService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="QueryGoDescribeCommand" /> class. </summary>
    /// <param name="queryService"> The query workflow service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public QueryGoDescribeCommand (
        IQueryService queryService,
        ICommandResultWriter commandResultWriter)
    {
        this.queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>query go describe</c> and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="readIndexMode">--readIndexMode, readIndex mode (disabled|allowStale|requireFresh).</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet ready.</param>
    /// <param name="globalObjectId">--globalObjectId, GlobalObjectId target.</param>
    /// <param name="scene">Scene path used with hierarchyPath.</param>
    /// <param name="hierarchyPath">--hierarchyPath, GameObject hierarchy path used with scene or prefab.</param>
    /// <param name="prefab">Prefab path used with hierarchyPath.</param>
    /// <param name="depth">Description depth. 0 returns only the target GameObject summary.</param>
    /// <param name="fullDepth">--fullDepth, Expands the full GameObject description.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.DescribeSubcommand)]
    public async Task<int> DescribeAsync (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? readIndexMode = null,
        bool failFast = false,
        string? globalObjectId = null,
        string? scene = null,
        string? hierarchyPath = null,
        string? prefab = null,
        int? depth = null,
        bool fullDepth = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var requestId = Guid.NewGuid();

        var commonOptionsResult = QueryCommonOptionsNormalizer.Normalize(projectPath, mode, timeout, readIndexMode, failFast);
        if (!commonOptionsResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(requestId, commandResultWriter, UcliCommandNames.QueryGoDescribe, commonOptionsResult.Error!);
        }

        if (!TryCreateTarget(globalObjectId, scene, hierarchyPath, prefab, out var target, out var error))
        {
            return QueryCommandExecutionHelper.WriteExecutionError(requestId, commandResultWriter, UcliCommandNames.QueryGoDescribe, error!);
        }

        var depthResult = QueryDepthOptionNormalizer.Normalize(depth, fullDepth, DefaultDepth);
        if (!depthResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(requestId, commandResultWriter, UcliCommandNames.QueryGoDescribe, depthResult.Error!);
        }

        return await QueryCommandExecutionHelper.ExecuteAsync(
                requestId,
                queryService,
                commonOptionsResult.Options!,
                new QueryUnityOperationRequest(
                    CommandName: UcliCommandNames.QueryGoDescribe,
                    OperationId: OperationId,
                    OperationName: UcliPrimitiveOperationNames.GoDescribe,
                    Args: QueryOperationArgsFactory.CreateGoDescribe(target!, depthResult.Depth)),
                commandResultWriter,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryCreateTarget (
        string? globalObjectId,
        string? scene,
        string? hierarchyPath,
        string? prefab,
        out GameObjectReferenceArgs? target,
        out ExecutionError? error)
    {
        target = null;
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(globalObjectId, "globalObjectId", out var normalizedGlobalObjectId, out error))
        {
            return false;
        }
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(scene, "scene", out var normalizedScene, out error))
        {
            return false;
        }
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(hierarchyPath, "hierarchyPath", out var normalizedHierarchyPath, out error))
        {
            return false;
        }
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(prefab, "prefab", out var normalizedPrefab, out error))
        {
            return false;
        }

        var selectorCount = 0;
        selectorCount += normalizedGlobalObjectId is null ? 0 : 1;
        selectorCount += normalizedScene is null ? 0 : 1;
        selectorCount += normalizedPrefab is null ? 0 : 1;
        if (selectorCount != 1)
        {
            error = ExecutionError.InvalidArgument(
                "query go describe requires exactly one target: --globalObjectId, --scene --hierarchyPath, or --prefab --hierarchyPath.");
            return false;
        }

        if (normalizedGlobalObjectId is not null)
        {
            if (normalizedHierarchyPath is not null)
            {
                error = ExecutionError.InvalidArgument(
                    "'--hierarchyPath' is supported only with '--scene' or '--prefab'.");
                return false;
            }

            if (!UnityGlobalObjectId.TryParse(normalizedGlobalObjectId, out var typedGlobalObjectId))
            {
                error = ExecutionError.InvalidArgument(
                    "Selector '--globalObjectId' must be a supported non-null Unity GlobalObjectId.");
                return false;
            }

            target = new GameObjectReferenceArgs(
                alias: null,
                globalObjectId: typedGlobalObjectId,
                prefab: null,
                scene: null,
                hierarchyPath: null);
            return true;
        }

        if (normalizedHierarchyPath is null)
        {
            error = ExecutionError.InvalidArgument(
                "Hierarchy targets require either '--scene --hierarchyPath' or '--prefab --hierarchyPath'.");
            return false;
        }

        if (!UnityHierarchyPath.TryParse(normalizedHierarchyPath, out var typedHierarchyPath))
        {
            error = ExecutionError.InvalidArgument(
                "Selector '--hierarchyPath' must contain non-empty slash-separated object names.");
            return false;
        }

        SceneAssetPath? typedScene = null;
        if (normalizedScene is not null
            && !SceneAssetPath.TryParse(normalizedScene, out typedScene))
        {
            error = ExecutionError.InvalidArgument(
                "Selector '--scene' must be a normalized .unity path below 'Assets/'.");
            return false;
        }

        PrefabAssetPath? typedPrefab = null;
        if (normalizedPrefab is not null
            && !PrefabAssetPath.TryParse(normalizedPrefab, out typedPrefab))
        {
            error = ExecutionError.InvalidArgument(
                "Selector '--prefab' must be a normalized .prefab path below 'Assets/'.");
            return false;
        }

        target = new GameObjectReferenceArgs(
            alias: null,
            globalObjectId: null,
            prefab: typedPrefab,
            scene: typedScene,
            hierarchyPath: typedHierarchyPath);
        return true;
    }
}
