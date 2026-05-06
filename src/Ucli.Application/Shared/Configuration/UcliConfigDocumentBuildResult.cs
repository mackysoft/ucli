namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Represents the result of building serializable config document values. </summary>
/// <param name="Document"> The serializable config document when build succeeds. </param>
/// <param name="Diagnostics"> The config diagnostics when build fails. </param>
internal sealed record UcliConfigDocumentBuildResult (
    UcliConfigDocument? Document,
    IReadOnlyList<UcliConfigDiagnostic> Diagnostics)
{
    /// <summary> Gets a value indicating whether document build succeeded. </summary>
    public bool IsSuccess => Document is not null && Diagnostics.Count == 0;

    /// <summary> Creates a successful document build result. </summary>
    /// <param name="document"> The serializable config document. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="document" /> is <see langword="null" />. </exception>
    public static UcliConfigDocumentBuildResult Success (UcliConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new UcliConfigDocumentBuildResult(document, Array.Empty<UcliConfigDiagnostic>());
    }

    /// <summary> Creates a failed document build result. </summary>
    /// <param name="diagnostics"> The config diagnostics. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="diagnostics" /> is <see langword="null" />. </exception>
    public static UcliConfigDocumentBuildResult Failure (IReadOnlyList<UcliConfigDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new UcliConfigDocumentBuildResult(null, diagnostics.ToArray());
    }
}
