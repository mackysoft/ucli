using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity asset GUID string used for exact asset resolution. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity asset GUID string used for exact asset resolution.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.AssetGuid)]
public sealed record UnityAssetGuid : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityAssetGuid" /> class. </summary>
    /// <param name="value"> The Unity asset GUID string. </param>
    [JsonConstructor]
    public UnityAssetGuid (string value)
        : base(value)
    {
    }

    /// <summary> Converts a string to a Unity asset GUID contract value. </summary>
    /// <param name="value"> The Unity asset GUID string. </param>
    /// <returns> The semantic Unity asset GUID value. </returns>
    public static implicit operator UnityAssetGuid (string value)
    {
        return new UnityAssetGuid(value);
    }
}
