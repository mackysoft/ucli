using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Asset creation operation arguments.")]
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

    [JsonInclude]
    [JsonRequired]
    [Description("Unity asset type identifier to create.")]
    public UnityTypeId Type { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Unity project relative asset path to create.")]
    [UcliAssetCreatable(UcliOperationAssetKind.Asset)]
    public UnityAssetPath Path { get; private init; }
}
