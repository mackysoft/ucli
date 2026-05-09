using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents the internal list snapshot used by <c>ops list</c>. </summary>
/// <param name="Snapshot"> The operation list projection snapshot. </param>
/// <param name="AccessInfo"> The internal access metadata. </param>
internal sealed record OpsListReadOutput (
    OpsCatalogListSnapshot Snapshot,
    OpsCatalogAccessInfo AccessInfo);
