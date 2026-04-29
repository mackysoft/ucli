using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Status.Common.Contracts;

/// <summary> Represents the result of status command workflow execution. </summary>
/// <param name="Output"> The normalized status output on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record StatusExecutionResult (
    StatusExecutionOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether status workflow execution succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful status execution result. </summary>
    /// <param name="output"> The normalized status output. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="output" /> is <see langword="null" />. </exception>
    public static StatusExecutionResult Success (StatusExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new StatusExecutionResult(output, null);
    }

    /// <summary> Creates a failed status execution result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static StatusExecutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new StatusExecutionResult(null, error);
    }
}
