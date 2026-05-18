using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from <c>plan</c> service results. </summary>
internal static class PlanCommandResultFactory
{
    /// <summary> Creates one command result for <c>plan</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (PlanServiceResult serviceResult)
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
                command: UcliCommandNames.Plan,
                message: serviceResult.Message,
                payload: payload);
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.Plan,
            serviceResult.Message,
            payload,
            serviceResult.Errors);
    }

    private static Dictionary<string, object?> CreatePayload (PlanExecutionOutput? output)
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
            ["readIndex"] = ReadIndexInfoPayloadProjector.Create(output.ReadIndex),
        };

        if (output.ContractViolations.Count != 0)
        {
            payload["contractViolations"] = output.ContractViolations;
        }

        if (string.IsNullOrWhiteSpace(output.PlanToken))
        {
            return payload;
        }

        payload["planToken"] = output.PlanToken;
        return payload;
    }
}
