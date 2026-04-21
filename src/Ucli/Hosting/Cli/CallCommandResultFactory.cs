using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Call.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli;

/// <summary> Creates command-level JSON results from <c>call</c> service results. </summary>
internal static class CallCommandResultFactory
{
    /// <summary> Creates one command result for <c>call</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (CallServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        object payload = CreatePayload(serviceResult.Output);
        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Call,
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
            Command: UcliCommandNames.Call,
            Status: IpcProtocol.StatusError,
            ExitCode: serviceResult.ExitCode,
            Message: serviceResult.Message,
            Payload: payload,
            Errors: errors);
    }

    private static object CreatePayload (CallExecutionOutput? output)
    {
        if (output == null)
        {
            return new { };
        }

        if (output.Plan == null)
        {
            return new
            {
                requestId = output.RequestId,
                opResults = output.OpResults,
            };
        }

        if (string.IsNullOrWhiteSpace(output.Plan.PlanToken))
        {
            return new
            {
                requestId = output.RequestId,
                opResults = output.OpResults,
                plan = new
                {
                    requestId = output.Plan.RequestId,
                    opResults = output.Plan.OpResults,
                },
            };
        }

        return new
        {
            requestId = output.RequestId,
            opResults = output.OpResults,
            plan = new
            {
                requestId = output.Plan.RequestId,
                opResults = output.Plan.OpResults,
                planToken = output.Plan.PlanToken,
            },
        };
    }
}