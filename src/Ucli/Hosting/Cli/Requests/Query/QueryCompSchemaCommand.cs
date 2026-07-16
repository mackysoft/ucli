using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the <c>query comp schema</c> CLI command entry point. </summary>
internal sealed class QueryCompSchemaCommand
{
    private static readonly IpcExecuteStepId OperationId = new("comp.schema");

    private readonly IQueryService queryService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="QueryCompSchemaCommand" /> class. </summary>
    /// <param name="queryService"> The query workflow service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public QueryCompSchemaCommand (
        IQueryService queryService,
        ICommandResultWriter commandResultWriter)
    {
        this.queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Describes a Unity component schema and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="readIndexMode">--readIndexMode, readIndex mode (disabled|allowStale|requireFresh).</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet ready.</param>
    /// <param name="type">Component runtime type identifier.</param>
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
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var requestId = Guid.NewGuid();

        var commonOptionsResult = QueryCommonOptionsNormalizer.Normalize(projectPath, mode, timeout, readIndexMode, failFast);
        if (!commonOptionsResult.IsSuccess)
        {
            return QueryCommandExecutionHelper.WriteExecutionError(requestId, commandResultWriter, UcliCommandNames.QueryCompSchema, commonOptionsResult.Error!);
        }

        if (!QueryOptionValueNormalizer.TryNormalizeRequired(type, "type", out var normalizedType, out var error))
        {
            return QueryCommandExecutionHelper.WriteExecutionError(requestId, commandResultWriter, UcliCommandNames.QueryCompSchema, error!);
        }

        return await QueryCommandExecutionHelper.ExecuteAsync(
                requestId,
                queryService,
                commonOptionsResult.Options!,
                new QueryUnityOperationRequest(
                    CommandName: UcliCommandNames.QueryCompSchema,
                    OperationId: OperationId,
                    OperationName: UcliPrimitiveOperationNames.CompSchema,
                    Args: QueryOperationArgsFactory.CreateCompSchema(normalizedType)),
                commandResultWriter,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
