using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the <c>query assets find</c> CLI command entry point. </summary>
internal sealed class QueryAssetsFindCommand
{
    private const string OperationId = "assets.find";

    private readonly IQueryService queryService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="QueryAssetsFindCommand" /> class. </summary>
    /// <param name="queryService"> The query workflow service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public QueryAssetsFindCommand (
        IQueryService queryService,
        ICommandResultWriter commandResultWriter)
    {
        this.queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>query assets find</c> and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="readIndexMode">--readIndexMode, readIndex mode (disabled|allowStale|requireFresh).</param>
    /// <param name="failFast">--failFast, Fails immediately when live fallback hits a non-ready Unity editor lifecycle.</param>
    /// <param name="type">Optional runtime type identifier filter.</param>
    /// <param name="pathPrefix">--pathPrefix, Optional asset path prefix filter.</param>
    /// <param name="nameContains">--nameContains, Optional asset name substring filter.</param>
    /// <param name="limit">Maximum number of matches to return in one window.</param>
    /// <param name="after">Window cursor returned from the previous response.</param>
    /// <param name="all">Returns all matches without bounded windowing.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.FindSubcommand)]
    public async Task<int> Find (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? readIndexMode = null,
        bool failFast = false,
        string? type = null,
        string? pathPrefix = null,
        string? nameContains = null,
        int? limit = null,
        string? after = null,
        bool all = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var commonOptionsResult = QueryCommonOptionsNormalizer.Normalize(projectPath, mode, timeout, readIndexMode, failFast);
        if (!commonOptionsResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(commandResultWriter, UcliCommandNames.QueryAssetsFind, commonOptionsResult.Error!);
        }

        var operationRequestResult = QueryAssetsFindOperationRequestFactory.Create(
            UcliCommandNames.QueryAssetsFind,
            OperationId,
            UcliPrimitiveOperationNames.AssetsFind,
            type,
            pathPrefix,
            nameContains,
            all,
            limit,
            after);
        if (!operationRequestResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(commandResultWriter, UcliCommandNames.QueryAssetsFind, operationRequestResult.Error!);
        }

        return await QueryCommandExecutionHelper.Execute(
                queryService,
                commonOptionsResult.Options!,
                operationRequestResult.Operation!,
                commandResultWriter,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
