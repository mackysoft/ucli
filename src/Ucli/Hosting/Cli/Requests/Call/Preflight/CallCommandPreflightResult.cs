using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests.Call.Preflight;

/// <summary> Represents the command preflight outcome for one <c>call</c> invocation. </summary>
/// <param name="Output"> The base payload used to preserve the command contract on later failures. </param>
/// <param name="FailureResult"> The normalized failure result when preflight itself failed. </param>
internal sealed record CallCommandPreflightResult (
    CallExecutionOutput? Output,
    CallServiceResult? FailureResult)
{
    /// <summary> Gets a value indicating whether preflight succeeded and produced a base payload. </summary>
    public bool IsSuccess => Output is not null && FailureResult is null;

    /// <summary> Creates a successful preflight result. </summary>
    /// <param name="output"> The base payload. </param>
    /// <returns> The successful preflight result. </returns>
    public static CallCommandPreflightResult Success (CallExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new CallCommandPreflightResult(output, null);
    }

    /// <summary> Creates a failed preflight result. </summary>
    /// <param name="failureResult"> The normalized failure result. </param>
    /// <returns> The failed preflight result. </returns>
    public static CallCommandPreflightResult Failure (CallServiceResult failureResult)
    {
        ArgumentNullException.ThrowIfNull(failureResult);
        return new CallCommandPreflightResult(null, failureResult);
    }

    /// <summary> Creates one normalized failure result that preserves the preflight payload when available. </summary>
    /// <param name="error"> The normalized execution error. </param>
    /// <returns> The normalized failure result. </returns>
    public CallServiceResult ToFailureResult (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FailureResult ?? CallFailureResultFactory.FromExecutionError(error, Output);
    }
}
