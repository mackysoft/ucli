using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts;

/// <summary> Carries a feature-defined kind for one long-lived logical execution. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[Length(1, int.MaxValue)]
[Pattern(ReferenceTextContract.DotSeparatedLowerCamelPattern)]
public sealed class ExecutionKind : UcliStringValue
{
    /// <summary> Initializes a feature-defined execution kind. </summary>
    /// <param name="value"> The stable execution-kind text. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is not a dot-separated sequence of lower-camel identifier segments.
    /// </exception>
    [JsonConstructor]
    public ExecutionKind (string value)
        : base(ReferenceTextContract.ValidateDotSeparatedLowerCamel(
            value,
            "Execution kind"))
    {
    }
}
