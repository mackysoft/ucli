using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents one operation metadata entry resolved from the catalog provider. </summary>
/// <param name="Name"> The unique operation name. </param>
/// <param name="Kind"> The operation kind. </param>
/// <param name="Policy"> The required operation policy. </param>
/// <param name="ArgsSchemaJson"> The operation argument schema as JSON text. </param>
internal sealed record UcliOperationDescriptor (
    string Name,
    UcliOperationKind Kind,
    OperationPolicy Policy,
    string ArgsSchemaJson);