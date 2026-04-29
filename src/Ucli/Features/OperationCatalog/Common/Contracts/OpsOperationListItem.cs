namespace MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents one operation entry returned by <c>ops list</c>. </summary>
/// <param name="Name"> The operation name. </param>
/// <param name="Kind"> The operation kind literal. </param>
/// <param name="Policy"> The operation policy literal. </param>
internal sealed record OpsOperationListItem (
    string Name,
    string Kind,
    string Policy);
