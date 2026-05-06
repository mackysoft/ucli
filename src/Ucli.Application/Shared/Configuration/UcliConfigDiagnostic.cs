namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Represents one diagnostic produced while validating or compiling <c>.ucli/config.json</c>. </summary>
/// <param name="Code"> The stable diagnostic code. </param>
/// <param name="PropertyPath"> The JSON property path related to the diagnostic, or <see langword="null" /> for document-level diagnostics. </param>
/// <param name="SourcePath"> The source config path related to the diagnostic, or <see langword="null" /> when unavailable. </param>
/// <param name="Message"> The user-facing diagnostic message. </param>
internal sealed record UcliConfigDiagnostic (
    string Code,
    string? PropertyPath,
    string? SourcePath,
    string Message)
{
    /// <summary> Creates a config diagnostic after validating required text fields. </summary>
    /// <param name="code"> The stable diagnostic code. </param>
    /// <param name="propertyPath"> The JSON property path related to the diagnostic. </param>
    /// <param name="sourcePath"> The source config path related to the diagnostic. </param>
    /// <param name="message"> The user-facing diagnostic message. </param>
    /// <returns> The created diagnostic. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="code" /> or <paramref name="message" /> is empty. </exception>
    public static UcliConfigDiagnostic Create (
        string code,
        string? propertyPath,
        string? sourcePath,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new UcliConfigDiagnostic(code, propertyPath, sourcePath, message);
    }
}
