namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents the catalog detail selected for <c>ops describe</c>. </summary>
/// <param name="Operation"> The selected full operation entry. </param>
/// <param name="AccessInfo"> The access metadata used to enforce catalog visibility. </param>
internal sealed record OpsDescribeReadOutput (
    ValidatedOpsOperation Operation,
    OpsCatalogAccessInfo AccessInfo);
