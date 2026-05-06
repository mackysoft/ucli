using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides shared typed-query command execution helpers. </summary>
internal static class QueryCommandExecutionHelper
{
    /// <summary> Writes one execution error and returns its exit code. </summary>
    public static int WriteExecutionError (
        ICommandResultWriter commandResultWriter,
        string commandName,
        ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(commandResultWriter);

        var errorResult = QueryCommandResultFactory.CreateExecutionError(commandName, error);
        commandResultWriter.WriteToStandardOutput(errorResult);
        return errorResult.ExitCode;
    }

    /// <summary> Executes a typed-query operation through the query service and writes the command result. </summary>
    public static async Task<int> Execute (
        IQueryService queryService,
        QueryCommonOptions options,
        QueryOperationRequest operation,
        ICommandResultWriter commandResultWriter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(commandResultWriter);

        var serviceResult = await queryService.Execute(
                new QueryCommandInput(
                    ProjectPath: options.ProjectPath,
                    Mode: options.Mode,
                    TimeoutMilliseconds: options.TimeoutMilliseconds,
                    ReadIndexMode: options.ReadIndexMode,
                    FailFast: options.FailFast,
                    Operation: operation),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = QueryCommandResultFactory.Create(serviceResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
