namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents normalized request values for static validation. </summary>
/// <param name="ProtocolVersion"> The request protocol version. </param>
/// <param name="Steps"> The requested step list. </param>
/// <param name="AllowPlayMode"> Whether static validation should use Play Mode-specific edit lowering. </param>
internal sealed record ValidateRequest (
    int ProtocolVersion,
    IReadOnlyList<ValidateRequestStep> Steps,
    bool AllowPlayMode = false);
