using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents the internal catalog snapshot used by <c>ops</c> subcommands. </summary>
/// <param name="Snapshot"> The validated operation catalog snapshot. </param>
/// <param name="AccessInfo"> The internal access metadata. </param>
internal sealed record OpsCatalogReadOutput (
    OpsCatalogSnapshot Snapshot,
    OpsCatalogAccessInfo AccessInfo);
