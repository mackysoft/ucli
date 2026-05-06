using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates shared failure envelopes for request command results. </summary>
internal static class RequestCommandFailureResultFactory
{
    /// <summary> Creates one failed command result from a verified application result state. </summary>
    public static CommandResult Create (
        string command,
        string message,
        object payload,
        IReadOnlyList<OperationExecutionError> errors,
        ApplicationOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(errors);

        var commandErrors = new CommandError[errors.Count];
        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            commandErrors[i] = new CommandError(error.Code, error.Message, error.OpId);
        }

        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: command,
            Status: IpcProtocol.StatusError,
            ExitCode: ApplicationOutcomeCliExitCodeMapper.ToExitCode(outcome),
            Message: message,
            Payload: payload,
            Errors: commandErrors);
    }
}
