using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Scene path operation arguments.")]
public sealed record ScenePathArgs
{
    [JsonConstructor]
    public ScenePathArgs (SceneAssetPath path)
    {
        Path = ContractArgumentGuard.RequireNotNull(path, nameof(path));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Project-relative path to a Unity scene asset.")]
    [UcliAssetExists(UcliOperationAssetKind.Scene)]
    public SceneAssetPath Path { get; private init; }
}
