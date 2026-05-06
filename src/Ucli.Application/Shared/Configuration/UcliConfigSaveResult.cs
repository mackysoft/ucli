using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Represents the result of persisting <see cref="UcliConfig" /> values. </summary>
/// <param name="Error"> The structured save error, or <see langword="null" /> on success. </param>
/// <param name="Diagnostics"> The config-content diagnostics, or an empty array when no diagnostics were produced. </param>
internal sealed record UcliConfigSaveResult (
    ExecutionError? Error,
    IReadOnlyList<UcliConfigDiagnostic> Diagnostics)
{
    /// <summary> Gets a value indicating whether config save succeeded. </summary>
    public bool IsSuccess => Error is null && Diagnostics.Count == 0;

    /// <summary> Creates a successful config-save result. </summary>
    /// <returns> The successful result. </returns>
    public static UcliConfigSaveResult Success ()
    {
        return new UcliConfigSaveResult(Error: null, Diagnostics: Array.Empty<UcliConfigDiagnostic>());
    }

    /// <summary> Creates a failed config-save result. </summary>
    /// <param name="error"> The structured save error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UcliConfigSaveResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UcliConfigSaveResult(Error: error, Diagnostics: Array.Empty<UcliConfigDiagnostic>());
    }

    /// <summary> Creates a failed config-save result from config-content diagnostics. </summary>
    /// <param name="diagnostics"> The config-content diagnostics. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="diagnostics" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="diagnostics" /> is empty. </exception>
    public static UcliConfigSaveResult Failure (IReadOnlyList<UcliConfigDiagnostic> diagnostics)
    {
        return new UcliConfigSaveResult(Error: null, Diagnostics: UcliConfigDiagnosticList.CopyForFailure(diagnostics));
    }
}
