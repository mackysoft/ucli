using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Init.Common.Contracts;

/// <summary> Represents the result of init execution. </summary>
/// <param name="Output"> The init output values on success. </param>
/// <param name="Error"> The structured init error on failure. </param>
internal sealed record InitExecutionResult (
    InitExecutionOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether init execution succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful init-execution result. </summary>
    /// <param name="output"> The init output values. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="output" /> is <see langword="null" />. </exception>
    public static InitExecutionResult Success (InitExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new InitExecutionResult(output, null);
    }

    /// <summary> Creates a failed init-execution result. </summary>
    /// <param name="error"> The structured init error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static InitExecutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new InitExecutionResult(null, error);
    }
}