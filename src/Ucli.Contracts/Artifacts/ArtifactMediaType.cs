using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts;

/// <summary> Carries the media type of one immutable artifact byte sequence. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[Length(3, int.MaxValue)]
[Pattern(ReferenceTextContract.ArtifactMediaTypePattern)]
public sealed class ArtifactMediaType : UcliStringValue
{
    /// <summary> Initializes a canonical lowercase media type with optional token-valued parameters. </summary>
    /// <param name="value">
    /// The lowercase <c>type/subtype</c> text followed by zero or more <c>; name=value</c> parameters
    /// with lowercase names and case-preserving token values.
    /// </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is not in the canonical token-valued media type form.
    /// </exception>
    [JsonConstructor]
    public ArtifactMediaType (string value)
        : base(ReferenceTextContract.ValidateArtifactMediaType(value))
    {
    }
}
