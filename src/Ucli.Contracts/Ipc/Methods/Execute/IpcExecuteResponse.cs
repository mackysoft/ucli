using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an <c>execute</c> IPC response payload. </summary>
/// <param name="OpResults"> The per-step execution results. </param>
public sealed record IpcExecuteResponse (
    IReadOnlyList<IpcExecuteOperationResult> OpResults)
{
    /// <summary> Gets the resolved Unity project identity for the request. </summary>
    public IpcProjectIdentity Project { get; init; } = IpcProjectIdentity.Unknown;

    /// <summary> Gets the optional plan token issued by the <c>plan</c> command. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlanToken { get; init; }

    /// <summary> Gets the optional mutation-to-read postcondition contract emitted after call execution. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcExecuteReadPostcondition? ReadPostcondition { get; init; }

    /// <summary> Gets source facts needed to verify post-read claims from this portable result. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcExecutePostReadSource? PostReadSource { get; init; }

    /// <summary> Gets runtime result violations against published operation assurance facts. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<IpcExecuteContractViolation>? ContractViolations { get; init; }
}
