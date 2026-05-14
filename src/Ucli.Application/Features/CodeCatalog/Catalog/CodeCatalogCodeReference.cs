namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Represents a code lookup reference parsed from <c>CODE</c> or <c>KIND:CODE</c>. </summary>
/// <param name="Code"> The raw code value. </param>
/// <param name="ExpectedKind"> The expected kind from a qualified lookup, or <see langword="null" /> for an unqualified lookup. </param>
internal sealed record CodeCatalogCodeReference (
    string Code,
    string? ExpectedKind);
