using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Prefab path operation arguments.")]
public sealed record PrefabPathArgs
{
    [JsonConstructor]
    public PrefabPathArgs (PrefabAssetPath path)
    {
        Path = ContractArgumentGuard.RequireNotNull(path, nameof(path));
    }

    [UcliRequired]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Prefab)]
    public PrefabAssetPath Path { get; }
}
