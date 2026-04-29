using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from <c>refresh</c> execution results. </summary>
internal static class RefreshCommandResultFactory
{
    /// <summary> Creates one command result for <c>refresh</c>. </summary>
    /// <param name="executionResult"> The execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (OperationExecuteResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        var payload = new Dictionary<string, object?>
        {
            ["requestId"] = executionResult.RequestId,
            ["opResults"] = executionResult.OpResults,
        };
        if (executionResult.ReadPostcondition != null)
        {
            payload["readPostcondition"] = executionResult.ReadPostcondition;
        }

        if (executionResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Refresh,
                message: "uCLI refresh completed.",
                payload: payload);
        }

        var errors = new CommandError[executionResult.Errors.Count];
        for (var i = 0; i < executionResult.Errors.Count; i++)
        {
            var error = executionResult.Errors[i];
            errors[i] = new CommandError(error.Code, error.Message, error.OpId);
        }

        return new CommandResult(
            ProtocolVersion: executionResult.ProtocolVersion,
            Command: UcliCommandNames.Refresh,
            Status: IpcProtocol.StatusError,
            ExitCode: executionResult.ExitCode,
            Message: ResolveFailureMessage(executionResult.Errors),
            Payload: payload,
            Errors: errors);
    }

    /// <summary> Creates one command result for <c>refresh</c> from a normalized execution error. </summary>
    /// <param name="error"> The normalized execution error. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(OperationExecuteResultFactory.FromExecutionError(error));
    }

    private static string ResolveFailureMessage (IReadOnlyList<IpcError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            if (!string.IsNullOrWhiteSpace(error.Message))
            {
                return error.Message;
            }
        }

        return "uCLI refresh failed.";
    }
}
