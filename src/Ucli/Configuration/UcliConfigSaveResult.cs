using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Configuration;

/// <summary> Represents the result of persisting <see cref="UcliConfig" /> values. </summary>
/// <param name="Error"> The structured save error, or <see langword="null" /> on success. </param>
internal sealed record UcliConfigSaveResult (
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether config save succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful config-save result. </summary>
    /// <returns> The successful result. </returns>
    public static UcliConfigSaveResult Success ()
    {
        return new UcliConfigSaveResult(Error: null);
    }

    /// <summary> Creates a failed config-save result. </summary>
    /// <param name="error"> The structured save error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UcliConfigSaveResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UcliConfigSaveResult(Error: error);
    }
}