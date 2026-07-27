using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> References a Unity asset by its path below <c>Assets/</c>. </summary>
[Description("Unity asset-path reference.")]
public sealed record AssetPathReferenceArgs : AssetReferenceArgs, ResolveSelectorArgs
{
    /// <summary> Initializes a new Unity asset-path reference. </summary>
    /// <param name="assetPath"> The Unity asset path. </param>
    [JsonConstructor]
    public AssetPathReferenceArgs (UnityAssetPath assetPath)
    {
        AssetPath = ContractArgumentGuard.RequireNotNull(assetPath, nameof(assetPath));
    }

    /// <summary> Gets the Unity asset path. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Asset path selector under the Unity project.")]
    [UcliAssetExists(UcliOperationAssetKind.Asset)]
    public UnityAssetPath AssetPath { get; private init; }
}
