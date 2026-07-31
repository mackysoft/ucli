using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Execution;

/// <summary> Represents one optional mutation-to-read safety contract carried by an <c>execute</c> response. </summary>
/// <param name="Requirements"> The read requirements that later reads must satisfy. </param>
public sealed record ExecutionReadPostcondition
{
    /// <summary> Initializes a read postcondition from a stable snapshot of its requirements. </summary>
    [JsonConstructor]
    public ExecutionReadPostcondition (IReadOnlyList<ExecutionReadPostconditionRequirement> Requirements)
    {
        this.Requirements = ContractArgumentGuard.RequireItems(Requirements, nameof(Requirements));
    }

    public IReadOnlyList<ExecutionReadPostconditionRequirement> Requirements { get; }

    /// <summary> Gets a value indicating whether the contract carries no requirements. </summary>
    [JsonIgnore]
    public bool IsEmpty => Requirements.Count == 0;
}
