using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts;

/// <summary> Carries the feature-owned state of one long-lived logical execution. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[Length(1, int.MaxValue)]
public sealed class ExecutionState : UcliStringValue
{
    /// <summary> Initializes one feature-defined execution state. </summary>
    /// <param name="value"> The state text defined by the feature that owns the execution. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is not a dot-separated sequence of lower-camel identifier segments.
    /// </exception>
    [JsonConstructor]
    public ExecutionState (string value)
        : base(ReferenceTextContract.ValidateDotSeparatedLowerCamel(
            value,
            "Execution state"))
    {
    }
}
