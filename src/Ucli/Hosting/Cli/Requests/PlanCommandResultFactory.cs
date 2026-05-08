using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

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

        object payload = CreatePayload(serviceResult.Output);

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
                readIndex = ReadIndexInfoPayloadProjector.Create(output.ReadIndex),
            };
        }

        return new
        {
            requestId = output.RequestId,
            opResults = output.OpResults,
            readIndex = ReadIndexInfoPayloadProjector.Create(output.ReadIndex),
            planToken = output.PlanToken,
        };
    }
}
