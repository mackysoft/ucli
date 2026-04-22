using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an <c>execute</c> IPC response payload. </summary>
/// <param name="OpResults"> The per-step execution results. </param>
public sealed record IpcExecuteResponse (
    IReadOnlyList<IpcExecuteOperationResult> OpResults)
{
    /// <summary> Gets the optional plan token issued by the <c>plan</c> command. </summary>

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlanToken { get; init; }

    /// <summary> Gets the optional mutation-to-read postcondition contract emitted after call execution. </summary>

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcExecuteReadPostcondition? ReadPostcondition { get; init; }
}