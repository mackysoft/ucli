using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> References a Unity asset by GUID. </summary>
[Description("Unity asset GUID reference.")]
public sealed record AssetGuidReferenceArgs : AssetReferenceArgs, ResolveSelectorArgs
{
    /// <summary> Initializes a new Unity asset GUID reference. </summary>
    /// <param name="assetGuid"> The non-empty Unity asset GUID. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="assetGuid" /> is empty. </exception>
    [JsonConstructor]
    public AssetGuidReferenceArgs (Guid assetGuid)
    {
        if (assetGuid == Guid.Empty)
        {
            throw new ArgumentException("Asset GUID must not be empty.", nameof(assetGuid));
        }

        AssetGuid = assetGuid;
    }

    /// <summary> Gets the Unity asset GUID. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Asset GUID selector.")]
    [UcliAssetGuid]
    public Guid AssetGuid { get; private init; }
}
