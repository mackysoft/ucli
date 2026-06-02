using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one IPC request envelope sent from CLI to Unity. </summary>
/// <param name="ProtocolVersion"> The protocol version expected by sender. </param>
/// <param name="RequestId"> The request identifier for tracing and idempotency. </param>
/// <param name="SessionToken"> The session token presented for daemon authorization. </param>
/// <param name="Method"> The IPC method name. </param>
/// <param name="Payload"> The method-specific request payload. </param>
/// <param name="ResponseMode"> The requested response framing mode literal. </param>
[method: JsonConstructor]
public sealed record IpcRequest (
    int ProtocolVersion,
    string RequestId,
    string SessionToken,
    string Method,
    JsonElement Payload,
    string ResponseMode)
{
    /// <summary> Initializes a new instance of the <see cref="IpcRequest" /> record with a typed response mode. </summary>
    public IpcRequest (
        int ProtocolVersion,
        string RequestId,
        string SessionToken,
        string Method,
        JsonElement Payload,
        IpcResponseMode responseMode)
        : this(
            ProtocolVersion,
            RequestId,
            SessionToken,
            Method,
            Payload,
            ContractLiteralCodec.ToValue(responseMode))
    {
    }
}
