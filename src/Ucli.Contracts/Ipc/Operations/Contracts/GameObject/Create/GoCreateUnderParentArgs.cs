using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Creates a GameObject below an existing parent. </summary>
public sealed record GoCreateUnderParentArgs : GoCreateArgs
{
    /// <summary> Initializes a parented GameObject creation contract. </summary>
    /// <param name="name"> The name assigned to the created GameObject. </param>
    /// <param name="parent"> The parent GameObject reference. </param>
    [JsonConstructor]
    public GoCreateUnderParentArgs (
        string name,
        GameObjectReferenceArgs parent)
        : base(name)
    {
        Parent = ContractArgumentGuard.RequireNotNull(parent, nameof(parent));
    }

    /// <summary> Gets the parent GameObject reference. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Parent GameObject reference.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Parent { get; private init; }
}
