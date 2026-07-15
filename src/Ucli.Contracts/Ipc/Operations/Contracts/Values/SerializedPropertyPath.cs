using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity SerializedProperty path. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity SerializedProperty path.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
public sealed class SerializedPropertyPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="SerializedPropertyPath" /> class. </summary>
    /// <param name="value"> The SerializedProperty path. </param>
    [JsonConstructor]
    public SerializedPropertyPath (string value)
        : base(value)
    {
    }
}
