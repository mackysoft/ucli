namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one operation entry needed by <c>ops list</c>. </summary>
/// <param name="Name"> The operation name. </param>
/// <param name="Kind"> The operation kind value. </param>
/// <param name="Policy"> The operation policy value. </param>
internal sealed record OpsCatalogListEntry (
    string Name,
    string Kind,
    string Policy);
