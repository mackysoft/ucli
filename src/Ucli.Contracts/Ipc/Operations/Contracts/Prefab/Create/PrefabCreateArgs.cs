using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Prefab creation operation arguments.")]
public sealed record PrefabCreateArgs
{
    [JsonConstructor]
    public PrefabCreateArgs (
        SceneGameObjectReferenceArgs target,
        PrefabAssetPath path)
    {
        Target = ContractArgumentGuard.RequireNotNull(target, nameof(target));
        Path = ContractArgumentGuard.RequireNotNull(path, nameof(path));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Source scene GameObject reference.")]
    [UcliReferenceResolvable(UcliOperationReferenceTargetKind.GameObject)]
    public SceneGameObjectReferenceArgs Target { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Prefab asset path to create.")]
    [UcliAssetCreatable(UcliOperationAssetKind.Prefab)]
    public PrefabAssetPath Path { get; private init; }
}
