using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from <c>resolve</c> service results. </summary>
internal static class ResolveCommandResultFactory
{
    /// <summary> Creates one command result for <c>resolve</c>. </summary>
    public static CommandResult Create (ResolveServiceResult serviceResult)
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
                command: UcliCommandNames.Resolve,
                message: "uCLI resolve completed.",
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
            Command: UcliCommandNames.Resolve,
            Status: IpcProtocol.StatusError,
            ExitCode: ApplicationOutcomeCliExitCodeMapper.ToExitCode(serviceResult.Outcome),
            Message: ResolveFailureMessage(serviceResult.Errors),
            Payload: payload,
            Errors: errors);
    }

    /// <summary> Creates one command result for <c>resolve</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(ResolveServiceResultFactory.FromExecutionError(Guid.NewGuid().ToString("D"), error));
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

        return "uCLI resolve failed.";
    }
}
