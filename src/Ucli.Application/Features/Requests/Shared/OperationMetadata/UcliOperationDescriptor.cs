using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents one operation metadata entry resolved from the catalog provider. </summary>
/// <param name="Name"> The unique operation name. </param>
/// <param name="Kind"> The operation kind. </param>
/// <param name="Policy"> The required operation policy. </param>
/// <param name="ArgsSchemaJson"> The operation argument schema as JSON text. </param>
/// <param name="ResultSchemaJson"> The operation result schema as JSON text, or <see langword="null" /> when no result is emitted. </param>
/// <param name="Exposure"> Whether the operation is reachable from public request surfaces. </param>
internal sealed record UcliOperationDescriptor (
    string Name,
    UcliOperationKind Kind,
    OperationPolicy Policy,
    string ArgsSchemaJson,
    string? ResultSchemaJson = null,
    UcliOperationExposure Exposure = UcliOperationExposure.Public);
