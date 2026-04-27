using MackySoft.Ucli.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides shared typed-query command execution helpers. </summary>
internal static class QueryCommandExecutionHelper
{
    /// <summary> Writes one execution error and returns its exit code. </summary>
    public static int WriteExecutionError (
        string commandName,
        ExecutionError error)
    {
        var errorResult = QueryCommandResultFactory.CreateExecutionError(commandName, error);
        CommandResultWriter.WriteToStandardOutput(errorResult);
        return errorResult.ExitCode;
    }

    /// <summary> Executes a typed-query operation through the query service and writes the command result. </summary>
    public static async Task<int> Execute (
        IQueryService queryService,
        QueryCommonOptions options,
        QueryOperationRequest operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(operation);

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
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
