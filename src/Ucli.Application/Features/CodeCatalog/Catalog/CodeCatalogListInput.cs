namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Represents filters for listing code catalog descriptors. </summary>
/// <param name="Kind"> Optional exact kind filter. </param>
/// <param name="Command"> Optional exact or command-family filter. </param>
internal sealed record CodeCatalogListInput (
    string? Kind,
    string? Command);
