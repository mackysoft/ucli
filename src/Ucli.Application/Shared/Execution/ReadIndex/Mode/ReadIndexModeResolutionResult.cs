using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one read-index mode resolution result. </summary>
internal sealed class ReadIndexModeResolutionResult
{
    private ReadIndexModeResolutionResult (ReadIndexMode mode)
    {
        Mode = mode;
    }

    private ReadIndexModeResolutionResult (ExecutionError error)
    {
        Error = error;
    }

    /// <summary> Gets a value indicating whether mode resolution succeeded. </summary>
    public bool IsSuccess => Mode is not null;

    /// <summary> Gets the resolved mode on success; otherwise <see langword="null" />. </summary>
    public ReadIndexMode? Mode { get; }

    /// <summary> Gets the structured error on failure; otherwise <see langword="null" />. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Creates a successful mode-resolution result. </summary>
    /// <param name="mode"> The resolved mode. </param>
    /// <returns> The successful result. </returns>
    public static ReadIndexModeResolutionResult Success (ReadIndexMode mode)
    {
        if (!TextVocabulary.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Read-index mode must be a defined contract value.");
        }

        return new ReadIndexModeResolutionResult(mode);
    }

    /// <summary> Creates a failed mode-resolution result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static ReadIndexModeResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ReadIndexModeResolutionResult(error);
    }
}
