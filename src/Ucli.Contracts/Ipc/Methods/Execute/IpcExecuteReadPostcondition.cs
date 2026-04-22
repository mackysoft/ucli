using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one optional mutation-to-read safety contract carried by an <c>execute</c> response. </summary>
/// <param name="Requirements"> The read requirements that later reads must satisfy. </param>
public sealed record IpcExecuteReadPostcondition (
    IReadOnlyList<IpcExecuteReadPostconditionRequirement> Requirements)
{
    /// <summary> Gets a value indicating whether the contract carries no requirements. </summary>
    [JsonIgnore]
    public bool IsEmpty => Requirements.Count == 0;
}