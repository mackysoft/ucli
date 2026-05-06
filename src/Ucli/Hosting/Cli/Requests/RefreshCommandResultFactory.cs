using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

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
                message: executionResult.Message,
                payload: payload);
        }

        var errors = new CommandError[executionResult.Errors.Count];
        for (var i = 0; i < executionResult.Errors.Count; i++)
        {
            var error = executionResult.Errors[i];
            errors[i] = new CommandError(error.Code, error.Message, error.OpId);
        }

        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: UcliCommandNames.Refresh,
            Status: IpcProtocol.StatusError,
            ExitCode: ApplicationOutcomeCliExitCodeMapper.ToExitCode(executionResult.Outcome),
            Message: executionResult.Message,
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
}
