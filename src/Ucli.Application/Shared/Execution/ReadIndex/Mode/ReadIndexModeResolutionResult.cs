using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one read-index mode resolution result. </summary>
/// <param name="Mode"> The resolved mode on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal readonly record struct ReadIndexModeResolutionResult (
    ReadIndexMode? Mode,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether mode resolution succeeded. </summary>
    public bool IsSuccess => Mode is not null && Error is null;

    /// <summary> Creates a successful mode-resolution result. </summary>
    /// <param name="mode"> The resolved mode. </param>
    /// <returns> The successful result. </returns>
    public static ReadIndexModeResolutionResult Success (ReadIndexMode mode)
    {
        return new ReadIndexModeResolutionResult(mode, null);
    }

    /// <summary> Creates a failed mode-resolution result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static ReadIndexModeResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ReadIndexModeResolutionResult(null, error);
    }
}
