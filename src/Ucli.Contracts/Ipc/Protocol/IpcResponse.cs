using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one IPC response envelope returned from Unity to CLI. </summary>
public sealed record IpcResponse
{
    /// <summary> Initializes one IPC response envelope. </summary>
    /// <param name="protocolVersion"> The protocol version used by the response. </param>
    /// <param name="requestId"> The associated request identifier, or <see langword="null" /> when the request envelope could not be decoded. </param>
    /// <param name="status"> The response status. </param>
    /// <param name="payload"> The method-specific response payload. </param>
    /// <param name="errors"> The machine-readable error collection. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty. </exception>
    [JsonConstructor]
    public IpcResponse (
        int protocolVersion,
        Guid? requestId,
        string status,
        JsonElement payload,
        IReadOnlyList<IpcError> errors)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        Status = status;
        Payload = payload;
        Errors = errors;
    }

    /// <summary> Gets the protocol version used by the response. </summary>
    public int ProtocolVersion { get; }

    /// <summary> Gets the associated request identifier, or <see langword="null" /> when the request envelope could not be decoded. </summary>
    [JsonInclude]
    [JsonRequired]
    public Guid? RequestId { get; private init; }

    /// <summary> Gets the response status. </summary>
    public string Status { get; }

    /// <summary> Gets the method-specific response payload. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets the machine-readable error collection. </summary>
    public IReadOnlyList<IpcError> Errors { get; }
}
