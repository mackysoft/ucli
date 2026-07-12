using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the <c>query asset schema</c> CLI command entry point. </summary>
internal sealed class QueryAssetSchemaCommand
{
    private const string OperationId = "asset.schema";

    private readonly IQueryService queryService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="QueryAssetSchemaCommand" /> class. </summary>
    /// <param name="queryService"> The query workflow service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public QueryAssetSchemaCommand (
        IQueryService queryService,
        ICommandResultWriter commandResultWriter)
    {
        this.queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>query asset schema</c> and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="readIndexMode">--readIndexMode, readIndex mode (disabled|allowStale|requireFresh).</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet ready.</param>
    /// <param name="type">Asset runtime type identifier.</param>
    /// <param name="globalObjectId">--globalObjectId, GlobalObjectId target.</param>
    /// <param name="assetGuid">--assetGuid, Asset GUID target.</param>
    /// <param name="assetPath">--assetPath, Unity asset path target.</param>
    /// <param name="projectAssetPath">--projectAssetPath, Project-relative asset path target.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.SchemaSubcommand)]
    public async Task<int> SchemaAsync (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? readIndexMode = null,
        bool failFast = false,
        string? type = null,
        string? globalObjectId = null,
        string? assetGuid = null,
        string? assetPath = null,
        string? projectAssetPath = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var requestId = Guid.NewGuid();

        var commonOptionsResult = QueryCommonOptionsNormalizer.Normalize(projectPath, mode, timeout, readIndexMode, failFast);
        if (!commonOptionsResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(requestId, commandResultWriter, UcliCommandNames.QueryAssetSchema, commonOptionsResult.Error!);
        }

        if (!TryCreateArgs(type, globalObjectId, assetGuid, assetPath, projectAssetPath, out var args, out var error))
        {
            return QueryCommandExecutionHelper.WriteExecutionError(requestId, commandResultWriter, UcliCommandNames.QueryAssetSchema, error!);
        }

        return await QueryCommandExecutionHelper.ExecuteAsync(
                requestId,
                queryService,
                commonOptionsResult.Options!,
                new QueryUnityOperationRequest(
                    CommandName: UcliCommandNames.QueryAssetSchema,
                    OperationId: OperationId,
                    OperationName: UcliPrimitiveOperationNames.AssetSchema,
                    Args: args),
                commandResultWriter,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryCreateArgs (
        string? type,
        string? globalObjectId,
        string? assetGuid,
        string? assetPath,
        string? projectAssetPath,
        out System.Text.Json.JsonElement args,
        out ExecutionError? error)
    {
        args = default;
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(type, "type", out var normalizedType, out error))
        {
            return false;
        }
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(globalObjectId, "globalObjectId", out var normalizedGlobalObjectId, out error))
        {
            return false;
        }
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(assetGuid, "assetGuid", out var normalizedAssetGuid, out error))
        {
            return false;
        }
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(assetPath, "assetPath", out var normalizedAssetPath, out error))
        {
            return false;
        }
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(projectAssetPath, "projectAssetPath", out var normalizedProjectAssetPath, out error))
        {
            return false;
        }

        var selectorCount = 0;
        selectorCount += normalizedType is null ? 0 : 1;
        selectorCount += normalizedGlobalObjectId is null ? 0 : 1;
        selectorCount += normalizedAssetGuid is null ? 0 : 1;
        selectorCount += normalizedAssetPath is null ? 0 : 1;
        selectorCount += normalizedProjectAssetPath is null ? 0 : 1;
        if (selectorCount != 1)
        {
            error = ExecutionError.InvalidArgument(
                "query asset schema requires exactly one selector: --type, --globalObjectId, --assetGuid, --assetPath, or --projectAssetPath.");
            return false;
        }

        if (normalizedType is not null)
        {
            args = QueryOperationArgsFactory.CreateAssetSchemaType(normalizedType);
            return true;
        }

        var target = new Dictionary<string, string>(StringComparer.Ordinal);
        AddIfNotNull(target, "globalObjectId", normalizedGlobalObjectId);
        AddIfNotNull(target, "assetGuid", normalizedAssetGuid);
        AddIfNotNull(target, "assetPath", normalizedAssetPath);
        AddIfNotNull(target, "projectAssetPath", normalizedProjectAssetPath);
        args = QueryOperationArgsFactory.CreateAssetSchemaTarget(target);
        return true;
    }

    private static void AddIfNotNull (
        IDictionary<string, string> target,
        string name,
        string? value)
    {
        if (value is null)
        {
            return;
        }

        target.Add(name, value);
    }
}
