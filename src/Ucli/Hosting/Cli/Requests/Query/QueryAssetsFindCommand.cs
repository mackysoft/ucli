using ConsoleAppFramework;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Features.Requests.Query.UseCases.Query.Projection;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the <c>query assets find</c> CLI command entry point. </summary>
internal sealed class QueryAssetsFindCommand
{
    private const string OperationId = "assets.find";

    private readonly IQueryService queryService;

    /// <summary> Initializes a new instance of the <see cref="QueryAssetsFindCommand" /> class. </summary>
    public QueryAssetsFindCommand (IQueryService queryService)
    {
        this.queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
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
            return QueryCommandExecutionHelper.WriteExecutionError(UcliCommandNames.QueryAssetsFind, commonOptionsResult.Error!);
        }

        if (!TryCreateFilter(type, pathPrefix, nameContains, out var filter, out var error))
        {
            return QueryCommandExecutionHelper.WriteExecutionError(UcliCommandNames.QueryAssetsFind, error!);
        }

        var windowResult = QueryWindowOptionsFactory.Create(all, limit, after);
        if (!windowResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(UcliCommandNames.QueryAssetsFind, windowResult.Error!);
        }

        return await QueryCommandExecutionHelper.Execute(
                queryService,
                commonOptionsResult.Options!,
                new QueryAssetsFindOperationRequest(
                    CommandName: UcliCommandNames.QueryAssetsFind,
                    OperationId: OperationId,
                    OperationName: UcliPrimitiveOperationNames.AssetsFind,
                    Filter: filter!,
                    WindowOptions: windowResult.Options!),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryCreateFilter (
        string? type,
        string? pathPrefix,
        string? nameContains,
        out QueryAssetsFindFilter? filter,
        out ExecutionError? error)
    {
        filter = null;
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(type, "type", out var normalizedType, out error))
        {
            return false;
        }
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(pathPrefix, "pathPrefix", out var normalizedPathPrefix, out error))
        {
            return false;
        }
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(nameContains, "nameContains", out var normalizedNameContains, out error))
        {
            return false;
        }

        if (normalizedType is null
            && normalizedPathPrefix is null
            && normalizedNameContains is null)
        {
            error = ExecutionError.InvalidArgument(
                "query assets find requires at least one filter: --type, --pathPrefix, or --nameContains.");
            return false;
        }

        filter = new QueryAssetsFindFilter(
            TypeId: normalizedType,
            PathPrefix: normalizedPathPrefix,
            NameContains: normalizedNameContains);
        return true;
    }
}
