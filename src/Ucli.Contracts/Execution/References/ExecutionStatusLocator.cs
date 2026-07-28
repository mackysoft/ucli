using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts;

/// <summary> Carries a feature-owned opaque locator for subsequent execution operations. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[Length(1, int.MaxValue)]
[Pattern(
    """^[^\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028-\u2029\u202F\u205F\u3000]+$(?![\s\S])""")]
public sealed class ExecutionStatusLocator : UcliStringValue
{
    /// <summary> Initializes an opaque execution status locator. </summary>
    /// <param name="value"> The locator text interpreted only by the feature that owns the execution. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is empty, contains whitespace, or contains malformed UTF-16 text.
    /// </exception>
    [JsonConstructor]
    public ExecutionStatusLocator (string value)
        : base(ReferenceTextContract.ValidateNonWhitespace(
            value,
            "Execution status locator"))
    {
    }
}
