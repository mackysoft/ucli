using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one IPC request envelope sent from CLI to Unity. </summary>
/// <param name="ProtocolVersion"> The protocol version expected by sender. </param>
/// <param name="RequestId"> The request identifier for tracing and idempotency. </param>
/// <param name="SessionToken"> The session token presented for daemon authorization. </param>
/// <param name="Method"> The IPC method name. </param>
/// <param name="Payload"> The method-specific request payload. </param>
public sealed record IpcRequest (
    int ProtocolVersion,
    string RequestId,
    string SessionToken,
    string Method,
    JsonElement Payload);