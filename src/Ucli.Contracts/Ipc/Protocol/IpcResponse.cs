using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

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
    /// <exception cref="ArgumentException"> Thrown when an identifier, payload, error, or status/error combination is invalid. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="errors" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="status" /> is not a defined wire status. </exception>
    [JsonConstructor]
    public IpcResponse (
        int protocolVersion,
        Guid? requestId,
        IpcResponseStatus status,
        JsonElement payload,
        IReadOnlyList<IpcError> errors)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        if (!ContractLiteralCodec.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "IPC response status must be specified.");
        }

        if (payload.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("IPC response payload must be specified.", nameof(payload));
        }

        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        var errorSnapshot = errors.ToArray();
        for (var index = 0; index < errorSnapshot.Length; index++)
        {
            var error = errorSnapshot[index];
            if (error == null)
            {
                throw new ArgumentException("IPC response errors must not contain null entries.", nameof(errors));
            }

        }

        if (status == IpcResponseStatus.Ok && errorSnapshot.Length != 0)
        {
            throw new ArgumentException("Successful IPC responses must not contain errors.", nameof(errors));
        }

        if (status == IpcResponseStatus.Error && errorSnapshot.Length == 0)
        {
            throw new ArgumentException("Failed IPC responses must contain at least one error.", nameof(errors));
        }

        if (requestId == null && status != IpcResponseStatus.Error)
        {
            throw new ArgumentException("Only failed IPC responses may omit request correlation.", nameof(requestId));
        }

        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        Status = status;
        Payload = payload;
        Errors = Array.AsReadOnly(errorSnapshot);
    }

    /// <summary> Gets the protocol version used by the response. </summary>
    public int ProtocolVersion { get; }

    /// <summary> Gets the associated request identifier, or <see langword="null" /> when the request envelope could not be decoded. </summary>
    [JsonInclude]
    [JsonRequired]
    public Guid? RequestId { get; private init; }

    /// <summary> Gets the response status. </summary>
    public IpcResponseStatus Status { get; }

    /// <summary> Gets the method-specific response payload. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets the machine-readable error collection. </summary>
    public IReadOnlyList<IpcError> Errors { get; }
}
