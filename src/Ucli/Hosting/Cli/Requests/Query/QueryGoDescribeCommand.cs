using ConsoleAppFramework;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the <c>query go describe</c> CLI command entry point. </summary>
internal sealed class QueryGoDescribeCommand
{
    private const int DefaultDepth = 0;

    private const string OperationId = "go.describe";

    private readonly IQueryService queryService;

    /// <summary> Initializes a new instance of the <see cref="QueryGoDescribeCommand" /> class. </summary>
    public QueryGoDescribeCommand (IQueryService queryService)
    {
        this.queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
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
    public async Task<int> Describe (
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

        var commonOptionsResult = QueryCommonOptionsNormalizer.Normalize(projectPath, mode, timeout, readIndexMode, failFast);
        if (!commonOptionsResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(UcliCommandNames.QueryGoDescribe, commonOptionsResult.Error!);
        }

        if (!TryCreateTarget(globalObjectId, scene, hierarchyPath, prefab, out var target, out var error))
        {
            return QueryCommandExecutionHelper.WriteExecutionError(UcliCommandNames.QueryGoDescribe, error!);
        }

        var depthResult = QueryDepthOptionNormalizer.Normalize(depth, fullDepth, DefaultDepth);
        if (!depthResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(UcliCommandNames.QueryGoDescribe, depthResult.Error!);
        }

        return await QueryCommandExecutionHelper.Execute(
                queryService,
                commonOptionsResult.Options!,
                new QueryUnityOperationRequest(
                    CommandName: UcliCommandNames.QueryGoDescribe,
                    OperationId: OperationId,
                    OperationName: UcliPrimitiveOperationNames.GoDescribe,
                    Args: QueryOperationArgsFactory.CreateGoDescribe(target!, depthResult.Depth)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryCreateTarget (
        string? globalObjectId,
        string? scene,
        string? hierarchyPath,
        string? prefab,
        out IReadOnlyDictionary<string, string>? target,
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

            target = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["globalObjectId"] = normalizedGlobalObjectId,
            };
            return true;
        }

        if (normalizedHierarchyPath is null)
        {
            error = ExecutionError.InvalidArgument(
                "Hierarchy targets require either '--scene --hierarchyPath' or '--prefab --hierarchyPath'.");
            return false;
        }

        target = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [normalizedScene is not null ? "scene" : "prefab"] = normalizedScene ?? normalizedPrefab!,
            ["hierarchyPath"] = normalizedHierarchyPath,
        };
        return true;
    }
}
