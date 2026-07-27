using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents one operation entry returned by <c>ops list</c>. </summary>
/// <param name="Name"> The operation name. </param>
/// <param name="Kind"> The operation kind literal. </param>
/// <param name="Policy"> The operation policy literal. </param>
/// <param name="Description"> The operation purpose description. </param>
internal sealed record OpsOperationListItem (
    string Name,
    UcliOperationKind Kind,
    OperationPolicy Policy,
    string Description);
