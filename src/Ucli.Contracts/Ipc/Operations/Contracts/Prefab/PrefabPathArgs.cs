using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Prefab path operation arguments.")]
public sealed record PrefabPathArgs
{
    [JsonConstructor]
    public PrefabPathArgs (PrefabAssetPath path)
    {
        Path = ContractArgumentGuard.RequireNotNull(path, nameof(path));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Project-relative path to a Unity prefab asset.")]
    [UcliAssetExists(UcliOperationAssetKind.Prefab)]
    public PrefabAssetPath Path { get; private init; }
}
