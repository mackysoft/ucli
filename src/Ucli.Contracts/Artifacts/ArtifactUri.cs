using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts;

/// <summary> Carries a fragment-free absolute URI that locates one immutable artifact byte sequence. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[Length(3, int.MaxValue)]
[Pattern(ReferenceTextContract.ArtifactUriPattern)]
public sealed class ArtifactUri : UcliStringValue
{
    /// <summary> Initializes a fragment-free absolute artifact URI. </summary>
    /// <param name="value"> The fragment-free absolute URI text interpreted by the artifact publisher and consumer. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> has no absolute scheme, has an empty scheme-specific part,
    /// contains a character outside the artifact URI contract, including a fragment delimiter,
    /// or contains malformed percent encoding.
    /// </exception>
    [JsonConstructor]
    public ArtifactUri (string value)
        : base(ReferenceTextContract.ValidateArtifactUri(value))
    {
    }
}
