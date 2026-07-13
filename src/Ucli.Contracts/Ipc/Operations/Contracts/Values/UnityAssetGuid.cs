using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

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
}
