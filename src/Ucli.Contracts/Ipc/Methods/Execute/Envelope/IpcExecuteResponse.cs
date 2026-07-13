using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an <c>execute</c> IPC response payload with its resolved Unity project identity. </summary>
public sealed record IpcExecuteResponse
{
    /// <summary> Initializes an <c>execute</c> IPC response. </summary>
    /// <param name="opResults"> The per-step execution results. </param>
    /// <param name="project"> The resolved Unity project identity for the request. </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="opResults" /> or <paramref name="project" /> is <see langword="null" />.
    /// </exception>
    [JsonConstructor]
    public IpcExecuteResponse (
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IpcProjectIdentity project)
    {
        OpResults = opResults ?? throw new ArgumentNullException(nameof(opResults));
        Project = project ?? throw new ArgumentNullException(nameof(project));
    }

    /// <summary> Gets the per-step execution results. </summary>
    public IReadOnlyList<IpcExecuteOperationResult> OpResults { get; }

    /// <summary> Gets the resolved Unity project identity for the request. </summary>
    public IpcProjectIdentity Project { get; }

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
