using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one IPC request envelope sent from CLI to Unity. </summary>
public sealed record IpcRequest
{
    /// <summary> Initializes one IPC request envelope. </summary>
    /// <param name="protocolVersion"> The protocol version expected by the sender. </param>
    /// <param name="requestId"> The non-empty request identifier used for tracing and idempotency. </param>
    /// <param name="sessionToken"> The raw session token presented for daemon authorization, or <see langword="null" /> when the wire field is absent or null. </param>
    /// <param name="method"> The IPC method name, or <see langword="null" /> when the wire field is absent. </param>
    /// <param name="payload"> The method-specific request payload. </param>
    /// <param name="responseMode"> The requested response framing mode literal. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty. </exception>
    [JsonConstructor]
    public IpcRequest (
        int protocolVersion,
        Guid requestId,
        string? sessionToken,
        string? method,
        JsonElement payload,
        string responseMode)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        SessionToken = sessionToken;
        Method = method;
        Payload = payload;
        ResponseMode = responseMode;
    }

    /// <summary> Gets the protocol version expected by the sender. </summary>
    public int ProtocolVersion { get; }

    /// <summary> Gets the request identifier for tracing and idempotency. </summary>
    public Guid RequestId { get; }

    /// <summary> Gets the raw session token presented for daemon authorization, or <see langword="null" /> when the wire field is absent or null. </summary>
    public string? SessionToken { get; }

    /// <summary> Gets the IPC method name, or <see langword="null" /> when the wire field is absent. </summary>
    public string? Method { get; }

    /// <summary> Gets the method-specific request payload. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets the requested response framing mode literal. </summary>
    public string ResponseMode { get; }
}
