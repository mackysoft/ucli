using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> References a Unity object by GlobalObjectId. </summary>
[Description("Unity GlobalObjectId reference.")]
public sealed record GlobalObjectIdReferenceArgs :
    AssetReferenceArgs,
    ComponentReferenceArgs,
    GameObjectReferenceArgs,
    SceneGameObjectReferenceArgs,
    ResolveSelectorArgs
{
    /// <summary> Initializes a new GlobalObjectId reference. </summary>
    /// <param name="globalObjectId"> The resolved Unity GlobalObjectId. </param>
    [JsonConstructor]
    public GlobalObjectIdReferenceArgs (UnityGlobalObjectId globalObjectId)
    {
        GlobalObjectId = ContractArgumentGuard.RequireNotNull(globalObjectId, nameof(globalObjectId));
    }

    /// <summary> Gets the resolved Unity GlobalObjectId. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Resolved Unity GlobalObjectId.")]
    public UnityGlobalObjectId GlobalObjectId { get; private init; }
}
