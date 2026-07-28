using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a path prefix that identifies the Unity <c>Assets</c> root or one of its descendants. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[Length(1, int.MaxValue)]
[UcliProjectRelativePath]
public sealed class UnityAssetPathPrefix : UcliStringValue
{
    /// <summary> Initializes a normalized path prefix for filtering assets below the Unity <c>Assets</c> root. </summary>
    /// <param name="value"> The <c>Assets</c> root or a project-relative path below it. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> does not identify the <c>Assets</c> root or one of its descendants. </exception>
    [JsonConstructor]
    public UnityAssetPathPrefix (string value)
        : base(UnityAssetPathContract.NormalizeAssetsRootOrDescendantPathOrThrow(value))
    {
    }
}
