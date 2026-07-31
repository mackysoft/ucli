using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>compile.recovered</c> stream payload. </summary>
public sealed record CompileRecoveredEntry
{
    /// <summary> Initializes one compile recovery entry for a non-empty execution identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="ExecutionId" /> is empty. </exception>
    [JsonConstructor]
    public CompileRecoveredEntry (Guid ExecutionId)
    {
        if (ExecutionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Lifecycle Execution id must not be empty.",
                nameof(ExecutionId));
        }

        this.ExecutionId = ExecutionId;
    }

    /// <summary> Gets the compile Lifecycle Execution that resumed after endpoint re-registration. </summary>
    public Guid ExecutionId { get; }
}
