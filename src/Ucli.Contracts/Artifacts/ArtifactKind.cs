using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts;

/// <summary> Carries a product-defined kind for one immutable artifact. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[Length(1, int.MaxValue)]
[Pattern(ReferenceTextContract.DotSeparatedLowerCamelPattern)]
public sealed class ArtifactKind : UcliStringValue
{
    /// <summary> Initializes a product-defined artifact kind. </summary>
    /// <param name="value"> The stable artifact-kind text. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is not a dot-separated sequence of lower-camel identifier segments.
    /// </exception>
    [JsonConstructor]
    public ArtifactKind (string value)
        : base(ReferenceTextContract.ValidateDotSeparatedLowerCamel(
            value,
            "Artifact kind"))
    {
    }
}
