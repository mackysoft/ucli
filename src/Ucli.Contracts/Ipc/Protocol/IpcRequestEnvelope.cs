using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one IPC request envelope sent from CLI to Unity. </summary>
public sealed class IpcRequestEnvelope : IIpcRequestCorrelation
{
    /// <summary> Initializes one IPC request envelope. </summary>
    /// <param name="protocolVersion"> The protocol version expected by the sender. </param>
    /// <param name="requestId"> The non-empty request identifier used for tracing and idempotency. </param>
    /// <param name="sessionToken"> The raw session token presented for daemon authorization, or <see langword="null" /> when the wire field is absent or null. </param>
    /// <param name="method"> The IPC method name, or <see langword="null" /> when the wire field is absent. </param>
    /// <param name="payload"> The method-specific request payload. </param>
    /// <param name="responseMode"> The requested response framing mode literal, or <see langword="null" /> when the wire field is absent. </param>
    /// <param name="requestDeadlineUtc"> The UTC deadline shared by every delivery attempt for the logical request. </param>
    /// <param name="requestDeadlineRemainingMilliseconds"> The positive monotonic-clock time remaining until <paramref name="requestDeadlineUtc" /> when this delivery attempt starts, rounded up to milliseconds. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty, or when <paramref name="requestDeadlineUtc" /> is the default value or has a non-UTC offset. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="requestDeadlineRemainingMilliseconds" /> is less than or equal to zero. </exception>
    [JsonConstructor]
    public IpcRequestEnvelope (
        int protocolVersion,
        Guid requestId,
        string? sessionToken,
        string? method,
        JsonElement payload,
        string? responseMode,
        DateTimeOffset requestDeadlineUtc,
        int requestDeadlineRemainingMilliseconds)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        if (requestDeadlineRemainingMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestDeadlineRemainingMilliseconds),
                requestDeadlineRemainingMilliseconds,
                "Request deadline remaining milliseconds must be greater than zero.");
        }

        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        SessionToken = sessionToken;
        Method = method;
        Payload = payload;
        RequestDeadlineUtc = ContractArgumentGuard.RequireUtcTimestamp(requestDeadlineUtc, nameof(requestDeadlineUtc));
        RequestDeadlineRemainingMilliseconds = requestDeadlineRemainingMilliseconds;
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

    /// <summary> Gets the UTC deadline shared by every delivery attempt for the logical request. </summary>
    public DateTimeOffset RequestDeadlineUtc { get; }

    /// <summary> Gets the positive monotonic-clock time remaining until <see cref="RequestDeadlineUtc" /> when this delivery attempt starts, rounded up to milliseconds. </summary>
    public int RequestDeadlineRemainingMilliseconds { get; }

    /// <summary> Gets the requested response framing mode literal, or <see langword="null" /> when the wire field is absent. </summary>
    public string? ResponseMode { get; }
}
