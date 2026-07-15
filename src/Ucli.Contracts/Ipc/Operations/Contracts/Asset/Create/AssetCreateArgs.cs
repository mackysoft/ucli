using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Asset creation operation arguments.")]
public sealed record AssetCreateArgs
{
    [JsonConstructor]
    public AssetCreateArgs (
        UnityTypeId type,
        UnityAssetPath path)
    {
        Type = ContractArgumentGuard.RequireNotNull(type, nameof(type));
        Path = ContractArgumentGuard.RequireNotNull(path, nameof(path));
    }

    [UcliRequired]
    [UcliDescription("Unity asset type identifier to create.")]
    public UnityTypeId Type { get; }

    [UcliRequired]
    [UcliDescription("Unity project relative asset path to create.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.AssetCreatable, AssetKind = UcliOperationAssetKind.Asset)]
    public UnityAssetPath Path { get; }
}
