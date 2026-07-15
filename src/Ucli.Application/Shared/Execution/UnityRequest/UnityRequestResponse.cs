using System.Text.Json;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents one host-decoded Unity request response without exposing the IPC response envelope. </summary>
internal sealed record UnityRequestResponse
{
    /// <summary> Initializes one decoded Unity response. </summary>
    /// <param name="Payload"> The response payload body. </param>
    /// <param name="Errors"> The machine-readable response errors. </param>
    public UnityRequestResponse (
        JsonElement Payload,
        IReadOnlyList<OperationExecutionError> Errors)
    {
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

        this.Payload = Payload;
    }

    /// <summary> Gets the response payload body. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets the machine-readable response errors. </summary>
    public IReadOnlyList<OperationExecutionError> Errors { get; }
}
