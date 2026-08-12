using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents one host-decoded Unity request response without exposing the IPC response envelope. </summary>
internal sealed record UnityRequestResponse
{
    /// <summary> Initializes a response whose status is inferred from the error collection. </summary>
    public UnityRequestResponse (JsonElement Payload, IReadOnlyList<OperationExecutionError> Errors)
        : this(
            Errors is { Count: > 0 } ? IpcResponseStatus.Error : IpcResponseStatus.Ok,
            Payload,
            Errors)
    {
    }

    /// <summary> Initializes one decoded Unity response. </summary>
    /// <param name="Status"> The response status. </param>
    /// <param name="Payload"> The response payload body. </param>
    /// <param name="Errors"> The machine-readable response errors. </param>
    public UnityRequestResponse (
        IpcResponseStatus Status,
        JsonElement Payload,
        IReadOnlyList<OperationExecutionError> Errors)
    {
        if (!TextVocabulary.IsDefined(Status))
        {
            throw new ArgumentOutOfRangeException(nameof(Status));
        }
        if (Payload.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException("Unity response payload must be specified.", nameof(Payload));
        }

        ArgumentNullException.ThrowIfNull(Errors);
        if (Errors.Count == 0)
        {
            this.Errors = [];
        }
        else
        {
            var errorSnapshot = new OperationExecutionError[Errors.Count];
            for (var index = 0; index < Errors.Count; index++)
            {
                errorSnapshot[index] = Errors[index]
                    ?? throw new ArgumentException($"Unity response error at index {index} must not be null.", nameof(Errors));
            }

            this.Errors = Array.AsReadOnly(errorSnapshot);
        }

        this.Status = Status;
        this.Payload = Payload;
    }

    /// <summary> Gets the transport response status. </summary>
    public IpcResponseStatus Status { get; }

    /// <summary> Gets the response payload body. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets the machine-readable response errors. </summary>
    public IReadOnlyList<OperationExecutionError> Errors { get; }
}
