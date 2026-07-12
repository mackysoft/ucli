using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one IPC streaming frame returned before or at terminal response completion. </summary>
public sealed record IpcStreamFrame
{
    /// <summary> Initializes one IPC streaming frame. </summary>
    /// <param name="protocolVersion"> The protocol version used by the frame. </param>
    /// <param name="requestId"> The non-empty request identifier copied from the incoming request. </param>
    /// <param name="kind"> The stream frame kind. </param>
    /// <param name="event"> The progress event name, or <see langword="null" /> for a terminal frame. </param>
    /// <param name="payload"> The frame payload. </param>
    /// <param name="response"> The terminal response, or <see langword="null" /> for a progress frame. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty or <paramref name="response" /> identifies another request. </exception>
    [JsonConstructor]
    public IpcStreamFrame (
        int protocolVersion,
        Guid requestId,
        string kind,
        string? @event,
        JsonElement payload,
        IpcResponse? response)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        if (response != null && response.RequestId != requestId)
        {
            throw new ArgumentException(
                "Terminal response request id must match the stream frame request id.",
                nameof(response));
        }

        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        Kind = kind;
        Event = @event;
        Payload = payload;
        Response = response;
    }

    /// <summary> Gets the protocol version used by the frame. </summary>
    public int ProtocolVersion { get; }

    /// <summary> Gets the request identifier copied from the incoming request. </summary>
    public Guid RequestId { get; }

    /// <summary> Gets the stream frame kind. </summary>
    public string Kind { get; }

    /// <summary> Gets the progress event name, or <see langword="null" /> for a terminal frame. </summary>
    public string? Event { get; }

    /// <summary> Gets the frame payload. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets the terminal response, or <see langword="null" /> for a progress frame. </summary>
    public IpcResponse? Response { get; }
}
