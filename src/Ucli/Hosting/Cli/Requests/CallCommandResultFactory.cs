using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

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

        return CommandFailureProjector.Create(
            UcliCommandNames.Call,
            serviceResult.Message,
            payload,
            serviceResult.Errors);
    }

    private static object CreatePayload (CallExecutionOutput? output)
    {
        if (output == null)
        {
            return new { };
        }

        var payload = new Dictionary<string, object?>
        {
            ["requestId"] = output.RequestId,
            ["opResults"] = output.OpResults,
        };

        if (output.ReadPostcondition != null)
        {
            payload["readPostcondition"] = output.ReadPostcondition;
        }

        if (output.Plan == null)
        {
            return payload;
        }

        if (string.IsNullOrWhiteSpace(output.Plan.PlanToken))
        {
            payload["plan"] = new
            {
                requestId = output.Plan.RequestId,
                opResults = output.Plan.OpResults,
            };
            return payload;
        }

        payload["plan"] = new
        {
            requestId = output.Plan.RequestId,
            opResults = output.Plan.OpResults,
            planToken = output.Plan.PlanToken,
        };
        return payload;
    }
}
