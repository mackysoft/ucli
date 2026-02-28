using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one IPC response envelope returned from Unity to CLI. </summary>
/// <param name="ProtocolVersion"> The protocol version used by the response. </param>
/// <param name="RequestId"> The request identifier copied from the incoming request. </param>
/// <param name="Status"> The response status. Uses <c>ok</c> or <c>error</c>. </param>
/// <param name="Payload"> The method-specific response payload. </param>
/// <param name="Errors"> The machine-readable error collection. </param>
public sealed record IpcResponse (
    int ProtocolVersion,
    string RequestId,
    string Status,
    JsonElement Payload,
    IReadOnlyList<IpcError> Errors);