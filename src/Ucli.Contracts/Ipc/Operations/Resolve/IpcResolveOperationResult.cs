using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the result payload returned by one <c>ucli.resolve</c> operation result. </summary>
[UcliDescription("Resolve operation result.")]
public sealed record IpcResolveOperationResult
{
    [JsonConstructor]
    public IpcResolveOperationResult (UnityGlobalObjectId globalObjectId)
    {
        if (globalObjectId == null)
        {
            throw new ArgumentNullException(nameof(globalObjectId));
        }

        GlobalObjectId = globalObjectId;
    }

    /// <summary> Gets the resolved GlobalObjectId string. </summary>
    [UcliRequired]
    [UcliDescription("Resolved Unity GlobalObjectId.")]
    public UnityGlobalObjectId GlobalObjectId { get; init; }
}
