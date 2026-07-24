using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

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
    /// <exception cref="ArgumentException"> Thrown when the request identifier, payload, or frame shape is invalid. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="kind" /> is not a defined wire kind. </exception>
    [JsonConstructor]
    public IpcStreamFrame (
        int protocolVersion,
        Guid requestId,
        IpcStreamFrameKind kind,
        string? @event,
        JsonElement payload,
        IpcResponse? response)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "IPC stream frame kind must be specified.");
        }

        if (payload.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("IPC stream frame payload must be specified.", nameof(payload));
        }

        if (kind == IpcStreamFrameKind.Progress)
        {
            if (string.IsNullOrWhiteSpace(@event))
            {
                throw new ArgumentException("Progress stream frames must contain an event name.", nameof(@event));
            }

            if (response != null)
            {
                throw new ArgumentException("Progress stream frames must not contain a terminal response.", nameof(response));
            }
        }
        else
        {
            if (@event != null)
            {
                throw new ArgumentException("Terminal stream frames must not contain an event name.", nameof(@event));
            }

            var terminalResponse = response
                ?? throw new ArgumentNullException(nameof(response), "Terminal stream frames must contain a response.");
            if (terminalResponse.RequestId != requestId)
            {
                throw new ArgumentException(
                    "Terminal response request id must match the stream frame request id.",
                    nameof(response));
            }

            if (terminalResponse.ProtocolVersion != protocolVersion)
            {
                throw new ArgumentException(
                    "Terminal response protocol version must match the stream frame protocol version.",
                    nameof(response));
            }
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
    public IpcStreamFrameKind Kind { get; }

    /// <summary> Gets the progress event name, or <see langword="null" /> for a terminal frame. </summary>
    public string? Event { get; }

    /// <summary> Gets the frame payload. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets the terminal response, or <see langword="null" /> for a progress frame. </summary>
    public IpcResponse? Response { get; }
}
