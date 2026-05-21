using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Status;

/// <summary> Represents the result of Play Mode status workflow execution. </summary>
/// <param name="Output"> The normalized status output on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured execution error on failure; otherwise <see langword="null" />. </param>
internal sealed record PlayStatusExecutionResult (
    PlayStatusExecutionOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the workflow succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful result. </summary>
    /// <param name="output"> The normalized output payload values. </param>
    /// <returns> The successful result. </returns>
    public static PlayStatusExecutionResult Success (PlayStatusExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new PlayStatusExecutionResult(output, null);
    }

    /// <summary> Creates a failed result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed result. </returns>
    public static PlayStatusExecutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new PlayStatusExecutionResult(null, error);
    }
}
