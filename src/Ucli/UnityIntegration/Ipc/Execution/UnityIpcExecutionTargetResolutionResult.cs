using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Represents a resolved Unity IPC execution target or one classified failure. </summary>
internal sealed record UnityIpcExecutionTargetResolutionResult (
    UnityExecutionTarget Target,
    UnityRequestFailure? Failure)
{
    /// <summary> Gets a value indicating whether target resolution succeeded. </summary>
    public bool IsSuccess => Failure is null;

    /// <summary> Creates a successful target resolution result. </summary>
    /// <param name="target"> The resolved execution target. </param>
    /// <returns> The successful target resolution result. </returns>
    public static UnityIpcExecutionTargetResolutionResult Success (UnityExecutionTarget target)
    {
        return new UnityIpcExecutionTargetResolutionResult(target, null);
    }

    /// <summary> Creates a failed target resolution result. </summary>
    /// <param name="failure"> The classified failure. </param>
    /// <returns> The failed target resolution result. </returns>
    public static UnityIpcExecutionTargetResolutionResult FailureResult (UnityRequestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new UnityIpcExecutionTargetResolutionResult(default, failure);
    }
}
