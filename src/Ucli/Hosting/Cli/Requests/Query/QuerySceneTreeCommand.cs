using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the <c>query scene tree</c> CLI command entry point. </summary>
internal sealed class QuerySceneTreeCommand
{
    private const int DefaultDepth = 1;

    private static readonly IpcExecuteStepId OperationId = new("scene.tree");

    private readonly IQueryService queryService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="QuerySceneTreeCommand" /> class. </summary>
    /// <param name="queryService"> The query workflow service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public QuerySceneTreeCommand (
        IQueryService queryService,
        ICommandResultWriter commandResultWriter)
    {
        this.queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>query scene tree</c> and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="readIndexMode">--readIndexMode, readIndex mode (disabled|allowStale|requireFresh).</param>
    /// <param name="failFast">--failFast, Fails immediately when live fallback hits a non-ready Unity editor lifecycle.</param>
    /// <param name="path">Scene asset path.</param>
    /// <param name="depth">Tree depth. 0 returns only roots.</param>
    /// <param name="fullDepth">--fullDepth, Expands the full tree.</param>
    /// <param name="limit">Maximum number of preorder hierarchy nodes to return in one window.</param>
    /// <param name="after">Window cursor returned from the previous response.</param>
    /// <param name="all">Returns the full tree without bounded windowing.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.TreeSubcommand)]
    public async Task<int> TreeAsync (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? readIndexMode = null,
        bool failFast = false,
        string? path = null,
        int? depth = null,
        bool fullDepth = false,
        int? limit = null,
        string? after = null,
        bool all = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var requestId = Guid.NewGuid();

        var commonOptionsResult = QueryCommonOptionsNormalizer.Normalize(projectPath, mode, timeout, readIndexMode, failFast);
        if (!commonOptionsResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(requestId, commandResultWriter, UcliCommandNames.QuerySceneTree, commonOptionsResult.Error!);
        }

        if (!QueryOptionValueNormalizer.TryNormalizeRequired(path, "path", out var normalizedPath, out var error))
        {
            return QueryCommandExecutionHelper.WriteExecutionError(requestId, commandResultWriter, UcliCommandNames.QuerySceneTree, error!);
        }

        if (!UnityScenePath.TryParse(normalizedPath, out var scenePath))
        {
            return QueryCommandExecutionHelper.WriteExecutionError(
                requestId,
                commandResultWriter,
                UcliCommandNames.QuerySceneTree,
                ExecutionError.InvalidArgument("Option '--path' must identify a .unity scene below 'Assets/' or 'Packages/'."));
        }

        var depthResult = QueryDepthOptionNormalizer.Normalize(depth, fullDepth, DefaultDepth);
        if (!depthResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(requestId, commandResultWriter, UcliCommandNames.QuerySceneTree, depthResult.Error!);
        }

        var windowResult = QueryWindowOptionsFactory.Create(all, limit, after);
        if (!windowResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(requestId, commandResultWriter, UcliCommandNames.QuerySceneTree, windowResult.Error!);
        }

        return await QueryCommandExecutionHelper.ExecuteAsync(
                requestId,
                queryService,
                commonOptionsResult.Options!,
                new QuerySceneTreeOperationRequest(
                    CommandName: UcliCommandNames.QuerySceneTree,
                    OperationId: OperationId,
                    OperationName: UcliPrimitiveOperationNames.SceneTree,
                    ScenePath: scenePath,
                    Depth: depthResult.Depth,
                    WindowOptions: windowResult.Options!),
                commandResultWriter,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
