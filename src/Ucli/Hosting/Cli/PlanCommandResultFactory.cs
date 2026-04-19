using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Plan;

namespace MackySoft.Ucli.Hosting.Cli;

/// <summary> Creates command-level JSON results from <c>plan</c> service results. </summary>
internal static class PlanCommandResultFactory
{
    /// <summary> Creates one command result for <c>plan</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (PlanServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        object payload = CreatePayload(serviceResult.Output);

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Plan,
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
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: UcliCommandNames.Plan,
            Status: IpcProtocol.StatusError,
            ExitCode: serviceResult.ExitCode,
            Message: serviceResult.Message,
            Payload: payload,
            Errors: errors);
    }

    private static object CreatePayload (PlanExecutionOutput? output)
    {
        if (output == null)
        {
            return new { };
        }

        if (string.IsNullOrWhiteSpace(output.PlanToken))
        {
            return new
            {
                requestId = output.RequestId,
                opResults = output.OpResults,
                readIndex = output.ReadIndex,
            };
        }

        return new
        {
            requestId = output.RequestId,
            opResults = output.OpResults,
            readIndex = output.ReadIndex,
            planToken = output.PlanToken,
        };
    }
}