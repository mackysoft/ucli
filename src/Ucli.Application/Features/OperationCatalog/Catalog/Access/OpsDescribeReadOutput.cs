namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents the internal operation detail used by <c>ops describe</c>. </summary>
/// <param name="Operation"> The selected full operation entry. </param>
/// <param name="AccessInfo"> The internal access metadata. </param>
internal sealed record OpsDescribeReadOutput (
    IndexOpEntryJsonContract Operation,
    OpsCatalogAccessInfo AccessInfo);
