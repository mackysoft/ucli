using System.Text.Json;

namespace MackySoft.Ucli.Operations;

/// <summary> Represents normalized request values for static validation. </summary>
/// <param name="ProtocolVersion"> The request protocol version. </param>
/// <param name="RequestId"> The request identifier. </param>
/// <param name="Ops"> The requested operation list. </param>
internal sealed record ValidateRequest (
    int ProtocolVersion,
    string? RequestId,
    IReadOnlyList<ValidateRequestOperation>? Ops);

/// <summary> Represents one operation element in a normalized validation request. </summary>
/// <param name="OpId"> The operation identifier. </param>
/// <param name="Op"> The operation name. </param>
/// <param name="Args"> The operation arguments. </param>
internal sealed record ValidateRequestOperation (
    string? OpId,
    string? Op,
    JsonElement Args);