using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents the internal catalog snapshot used by <c>ops</c> subcommands. </summary>
/// <param name="Operations"> The available operation entries. </param>
/// <param name="AccessInfo"> The internal access metadata. </param>
internal sealed record OpsCatalogReadOutput (
    IReadOnlyList<IndexOpEntryJsonContract> Operations,
    OpsCatalogAccessInfo AccessInfo);
