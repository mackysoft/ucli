using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Selects an asset schema by Unity type identifier. </summary>
public sealed record AssetSchemaByTypeArgs : AssetSchemaArgs
{
    /// <summary> Initializes a new type-based asset-schema selection. </summary>
    /// <param name="type"> The Unity type identifier to inspect. </param>
    [JsonConstructor]
    public AssetSchemaByTypeArgs (UnityTypeId type)
    {
        Type = ContractArgumentGuard.RequireNotNull(type, nameof(type));
    }

    /// <summary> Gets the Unity type identifier to inspect. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Unity asset type identifier to inspect.")]
    public UnityTypeId Type { get; private init; }
}
