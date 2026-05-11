using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from typed-query service results. </summary>
internal static class QueryCommandResultFactory
{
    /// <summary> Creates one command result for a typed-query command. </summary>
    public static CommandResult Create (QueryServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        var payload = new Dictionary<string, object?>
        {
            ["requestId"] = serviceResult.RequestId,
            ["opResults"] = serviceResult.OpResults,
            ["readIndex"] = ReadIndexInfoPayloadProjector.Create(serviceResult.ReadIndex),
        };
        if (!serviceResult.IsSuccess)
        {
            StartupFailurePayloadProjector.AppendFromFailures(payload, serviceResult.Errors);
        }

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: serviceResult.CommandName,
                message: serviceResult.Message,
                payload: payload);
        }

        return CommandFailureProjector.Create(
            serviceResult.CommandName,
            serviceResult.Message,
            payload,
            serviceResult.Errors);
    }

    /// <summary> Creates one command result for a typed-query command from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (
        string commandName,
        ExecutionError error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(error);
        return Create(QueryServiceResultFactory.FromExecutionError(
            commandName,
            Guid.NewGuid().ToString("D"),
            error));
    }
}
