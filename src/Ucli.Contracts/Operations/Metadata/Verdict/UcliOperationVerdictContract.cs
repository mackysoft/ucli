using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Describes the condition judged by one read-only operation. </summary>
public sealed record UcliOperationVerdictContract
{
    /// <summary> Initializes one operation verdict contract. </summary>
    /// <param name="Description"> The condition that must hold for the operation to return <c>pass</c>. </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="Description" /> is empty or whitespace.
    /// </exception>
    [JsonConstructor]
    public UcliOperationVerdictContract (string Description)
    {
        this.Description = ContractArgumentGuard.RequireValue(Description, nameof(Description));
    }

    /// <summary> Gets the condition that must hold for the operation to return <c>pass</c>. </summary>
    public string Description { get; }
}
