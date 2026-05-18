using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

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

        var payload = CreatePayload(serviceResult.Output);
        if (!serviceResult.IsSuccess)
        {
            StartupFailurePayloadProjector.AppendFromFailures(payload, serviceResult.Errors);
        }

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

    private static Dictionary<string, object?> CreatePayload (CallExecutionOutput? output)
    {
        if (output == null)
        {
            return new Dictionary<string, object?>();
        }

        var payload = new Dictionary<string, object?>
        {
            ["requestId"] = output.RequestId,
            ["project"] = ProjectIdentityPayloadProjector.Create(output.Project),
            ["opResults"] = output.OpResults,
        };

        if (output.ReadPostcondition != null)
        {
            payload["readPostcondition"] = output.ReadPostcondition;
        }

        if (output.ContractViolations.Count != 0)
        {
            payload["contractViolations"] = output.ContractViolations;
        }

        if (output.Plan == null)
        {
            return payload;
        }

        var planPayload = new Dictionary<string, object?>
        {
            ["requestId"] = output.Plan.RequestId,
            ["project"] = ProjectIdentityPayloadProjector.Create(output.Plan.Project),
            ["opResults"] = output.Plan.OpResults,
        };
        if (output.Plan.ContractViolations.Count != 0)
        {
            planPayload["contractViolations"] = output.Plan.ContractViolations;
        }

        if (string.IsNullOrWhiteSpace(output.Plan.PlanToken))
        {
            payload["plan"] = planPayload;
            return payload;
        }

        planPayload["planToken"] = output.Plan.PlanToken;
        payload["plan"] = planPayload;
        return payload;
    }
}
