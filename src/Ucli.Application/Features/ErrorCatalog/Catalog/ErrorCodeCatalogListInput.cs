namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

/// <summary> Represents optional filters for listing known error-code descriptors. </summary>
/// <param name="Category"> Optional exact category filter. </param>
/// <param name="Command"> Optional command filter matched by exact name or dot-segment family relationship. </param>
internal sealed record ErrorCodeCatalogListInput (
    string? Category,
    string? Command);
