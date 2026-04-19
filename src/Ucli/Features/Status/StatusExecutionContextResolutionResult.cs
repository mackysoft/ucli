using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Status;

/// <summary> Represents the result of resolving <see cref="StatusExecutionContext" /> values. </summary>
/// <param name="Context"> The resolved execution context, or <see langword="null" /> on failure. </param>
/// <param name="Error"> The structured resolution error, or <see langword="null" /> on success. </param>
internal sealed record StatusExecutionContextResolutionResult (
    StatusExecutionContext? Context,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether execution-context resolution succeeded. </summary>
    public bool IsSuccess => Context is not null && Error is null;

    /// <summary> Creates a successful execution-context resolution result. </summary>
    /// <param name="context"> The resolved execution context value. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="context" /> is <see langword="null" />. </exception>
    public static StatusExecutionContextResolutionResult Success (StatusExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new StatusExecutionContextResolutionResult(context, null);
    }

    /// <summary> Creates a failed execution-context resolution result. </summary>
    /// <param name="error"> The structured resolution error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static StatusExecutionContextResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new StatusExecutionContextResolutionResult(null, error);
    }
}