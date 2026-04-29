using MackySoft.Ucli.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan.Projection;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan.Preflight;

/// <summary> Represents the command preflight outcome for one <c>plan</c> invocation. </summary>
/// <param name="Output"> The base payload used to preserve the command contract on later failures. </param>
/// <param name="FailureResult"> The normalized failure result when preflight itself failed. </param>
internal sealed record PlanCommandPreflightResult (
    PlanExecutionOutput? Output,
    PlanServiceResult? FailureResult)
{
    /// <summary> Gets a value indicating whether preflight succeeded and produced a base payload. </summary>
    public bool IsSuccess => Output is not null && FailureResult is null;

    /// <summary> Creates a successful preflight result. </summary>
    /// <param name="output"> The base payload. </param>
    /// <returns> The successful preflight result. </returns>
    public static PlanCommandPreflightResult Success (PlanExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new PlanCommandPreflightResult(output, null);
    }

    /// <summary> Creates a failed preflight result. </summary>
    /// <param name="failureResult"> The normalized failure result. </param>
    /// <returns> The failed preflight result. </returns>
    public static PlanCommandPreflightResult Failure (PlanServiceResult failureResult)
    {
        ArgumentNullException.ThrowIfNull(failureResult);
        return new PlanCommandPreflightResult(null, failureResult);
    }

    /// <summary> Creates one normalized failure result that preserves the preflight payload when available. </summary>
    /// <param name="error"> The normalized execution error. </param>
    /// <returns> The normalized failure result. </returns>
    public PlanServiceResult ToFailureResult (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FailureResult ?? PlanFailureResultFactory.FromExecutionError(error, Output);
    }
}
