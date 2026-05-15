namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Defines supported code catalog kinds. </summary>
internal static class CodeCatalogKindValues
{
    /// <summary> Gets the kind for command failure codes emitted at <c>errors[].code</c>. </summary>
    public const string Error = "error";

    /// <summary> Gets the kind for diagnostic evidence codes. </summary>
    public const string Diagnostic = "diagnostic";

    /// <summary> Gets the reserved kind for future machine-readable reason codes. </summary>
    public const string Reason = "reason";

    /// <summary> Gets the kind for assurance claim codes. </summary>
    public const string Claim = "claim";

    /// <summary> Gets the kind for residual risk codes. </summary>
    public const string Risk = "risk";

    /// <summary> Gets the fallback kind used when describing an unknown code. </summary>
    public const string Unknown = "unknown";

    /// <summary> Gets supported descriptor kinds in stable display order. </summary>
    public static IReadOnlyList<string> KnownKinds { get; } =
    [
        Error,
        Diagnostic,
        Reason,
        Claim,
        Risk,
    ];

    /// <summary> Determines whether the kind is supported by the catalog. </summary>
    /// <param name="kind"> The kind value to validate. </param>
    /// <returns> <see langword="true" /> when <paramref name="kind" /> is one of the supported descriptor kinds. </returns>
    public static bool IsSupported (string? kind)
    {
        for (var i = 0; i < KnownKinds.Count; i++)
        {
            if (string.Equals(KnownKinds[i], kind, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
