using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene path operation arguments.")]
public sealed record ScenePathArgs
{
    [JsonConstructor]
    public ScenePathArgs (SceneAssetPath path)
    {
        Path = ContractArgumentGuard.RequireNotNull(path, nameof(path));
    }

    [UcliRequired]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetExists, AssetKind = UcliOperationAssetKind.Scene)]
    public SceneAssetPath Path { get; }
}
