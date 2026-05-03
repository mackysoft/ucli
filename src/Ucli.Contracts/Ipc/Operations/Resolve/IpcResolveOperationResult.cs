using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the result payload returned by one <c>ucli.resolve</c> operation result. </summary>
[UcliDescription("Resolve operation result.")]
public sealed record IpcResolveOperationResult
{
    [JsonConstructor]
    public IpcResolveOperationResult (string GlobalObjectId)
    {
        if (string.IsNullOrWhiteSpace(GlobalObjectId))
        {
            throw new ArgumentException("GlobalObjectId must not be null, empty, or whitespace.", nameof(GlobalObjectId));
        }

        this.GlobalObjectId = GlobalObjectId;
    }

    /// <summary> Gets the resolved GlobalObjectId string. </summary>
    [UcliRequired]
    [UcliDescription("Resolved Unity GlobalObjectId.")]
    public string GlobalObjectId { get; init; }
}
