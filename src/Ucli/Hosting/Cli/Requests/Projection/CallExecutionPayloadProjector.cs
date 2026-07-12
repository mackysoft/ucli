using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Requests.Projection;

/// <summary> Projects call workflow output into the shared request-execution command payload shape. </summary>
internal static class CallExecutionPayloadProjector
{
    /// <summary> Creates the command payload for a call-based workflow. </summary>
    /// <param name="output"> The call workflow output. </param>
    /// <returns> The command payload dictionary. </returns>
    public static Dictionary<string, object?> Create (CallExecutionOutput? output)
    {
        if (output == null)
        {
            return new Dictionary<string, object?>();
        }

        var payload = new Dictionary<string, object?>
        {
            ["requestId"] = output.RequestId.ToString("D"),
            ["project"] = ProjectIdentityPayloadProjector.Create(output.Project),
            ["opResults"] = output.OpResults,
        };

        if (output.ContractViolations.Count != 0)
        {
            payload["contractViolations"] = output.ContractViolations;
        }

        if (output.ReadPostcondition != null)
        {
            payload["readPostcondition"] = output.ReadPostcondition;
        }

        if (output.PostReadSource != null)
        {
            payload["postReadSource"] = output.PostReadSource;
        }

        if (output.Plan == null)
        {
            return payload;
        }

        var planPayload = new Dictionary<string, object?>
        {
            ["requestId"] = output.RequestId.ToString("D"),
            ["project"] = ProjectIdentityPayloadProjector.Create(output.Project),
            ["opResults"] = output.Plan.OpResults,
        };
        if (output.Plan.ContractViolations.Count != 0)
        {
            planPayload["contractViolations"] = output.Plan.ContractViolations;
        }

        if (!string.IsNullOrWhiteSpace(output.Plan.PlanToken))
        {
            planPayload["planToken"] = output.Plan.PlanToken;
        }

        payload["plan"] = planPayload;
        return payload;
    }
}
