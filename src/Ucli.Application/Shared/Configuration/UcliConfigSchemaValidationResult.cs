namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Represents the result of config JSON schema validation. </summary>
/// <param name="Document"> The raw config document when schema validation succeeds. </param>
/// <param name="Diagnostics"> The schema diagnostics when validation fails. </param>
internal sealed record UcliConfigSchemaValidationResult (
    UcliConfigJsonRawDocument? Document,
    IReadOnlyList<UcliConfigDiagnostic> Diagnostics)
{
    /// <summary> Gets a value indicating whether schema validation succeeded. </summary>
    public bool IsSuccess => Document.HasValue && Diagnostics.Count == 0;

    /// <summary> Creates a successful schema validation result. </summary>
    /// <param name="document"> The raw config document. </param>
    /// <returns> The successful result. </returns>
    public static UcliConfigSchemaValidationResult Success (UcliConfigJsonRawDocument document)
    {
        return new UcliConfigSchemaValidationResult(document, Array.Empty<UcliConfigDiagnostic>());
    }

    /// <summary> Creates a failed schema validation result. </summary>
    /// <param name="diagnostics"> The schema diagnostics. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="diagnostics" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="diagnostics" /> is empty. </exception>
    public static UcliConfigSchemaValidationResult Failure (IReadOnlyList<UcliConfigDiagnostic> diagnostics)
    {
        return new UcliConfigSchemaValidationResult(null, UcliConfigDiagnosticList.CopyForFailure(diagnostics));
    }
}
