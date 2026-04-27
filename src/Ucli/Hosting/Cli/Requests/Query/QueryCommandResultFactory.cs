using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from typed-query service results. </summary>
internal static class QueryCommandResultFactory
{
    /// <summary> Creates one command result for a typed-query command. </summary>
    public static CommandResult Create (QueryServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        object payload = new
        {
            requestId = serviceResult.RequestId,
            opResults = serviceResult.OpResults,
            readIndex = serviceResult.ReadIndex,
        };

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: serviceResult.CommandName,
                message: serviceResult.Message,
                payload: payload);
        }

        var errors = new CommandError[serviceResult.Errors.Count];
        for (var i = 0; i < serviceResult.Errors.Count; i++)
        {
            var error = serviceResult.Errors[i];
            errors[i] = new CommandError(error.Code, error.Message, error.OpId);
        }

        return new CommandResult(
            ProtocolVersion: serviceResult.ProtocolVersion,
            Command: serviceResult.CommandName,
            Status: IpcProtocol.StatusError,
            ExitCode: serviceResult.ExitCode,
            Message: serviceResult.Message,
            Payload: payload,
            Errors: errors);
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
