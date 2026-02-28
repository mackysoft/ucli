namespace MackySoft.Ucli.Operations;

/// <summary> Represents normalized request values for static validation. </summary>
/// <param name="ProtocolVersion"> The request protocol version. </param>
/// <param name="RequestId"> The request identifier. </param>
/// <param name="Ops"> The requested operation list. A malformed payload can include <see langword="null" /> elements. </param>
internal sealed record ValidateRequest (
    int ProtocolVersion,
    string? RequestId,
    IReadOnlyList<ValidateRequestOperation?>? Ops);