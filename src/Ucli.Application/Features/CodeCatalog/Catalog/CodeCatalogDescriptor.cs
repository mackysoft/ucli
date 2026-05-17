namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Describes one globally unique machine-readable code value. </summary>
/// <param name="Code"> The globally unique code value. </param>
/// <param name="Kind"> The catalog kind. This classifies the code but does not participate in identity. </param>
/// <param name="Category"> The kind-local category used for grouping. </param>
/// <param name="Summary"> A short summary used when selecting a code from list output. </param>
/// <param name="Meaning"> The static code meaning when known. </param>
/// <param name="AppearsIn"> JSON field paths where this code can appear. </param>
/// <param name="AppliesTo"> CLI command identifiers associated with this code. </param>
/// <param name="CoverageImpact"> Optional static coverage or completeness semantics. </param>
/// <param name="VerdictSemantics"> Optional static assurance verdict semantics. </param>
/// <param name="ExecutionSemantics"> Optional static execution and retry semantics. </param>
/// <param name="Inspect"> Response fields or diagnostic commands callers should inspect. </param>
/// <param name="RelatedCodes"> Adjacent globally unique code values. </param>
internal sealed record CodeCatalogDescriptor (
    UcliCodeValue Code,
    string Kind,
    string Category,
    string Summary,
    string? Meaning,
    IReadOnlyList<string> AppearsIn,
    IReadOnlyList<UcliCommand> AppliesTo,
    object? CoverageImpact,
    object? VerdictSemantics,
    UcliErrorExecutionSemantics? ExecutionSemantics,
    IReadOnlyList<string> Inspect,
    IReadOnlyList<UcliCodeValue> RelatedCodes);
