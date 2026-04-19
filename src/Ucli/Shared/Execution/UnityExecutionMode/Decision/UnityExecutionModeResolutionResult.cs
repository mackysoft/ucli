using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Represents one Unity execution mode resolution result. </summary>
/// <param name="Mode"> The resolved mode on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal readonly record struct UnityExecutionModeResolutionResult (
    UnityExecutionMode? Mode,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether mode resolution succeeded. </summary>
    public bool IsSuccess => Mode is not null && Error is null;

    /// <summary> Creates a successful mode-resolution result. </summary>
    /// <param name="mode"> The resolved mode. </param>
    /// <returns> The successful result. </returns>
    public static UnityExecutionModeResolutionResult Success (UnityExecutionMode mode)
    {
        return new UnityExecutionModeResolutionResult(mode, null);
    }

    /// <summary> Creates a failed mode-resolution result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityExecutionModeResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityExecutionModeResolutionResult(null, error);
    }
}