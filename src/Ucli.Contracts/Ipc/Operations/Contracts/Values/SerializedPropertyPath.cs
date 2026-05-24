using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity SerializedProperty path. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity SerializedProperty path.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
public sealed record SerializedPropertyPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="SerializedPropertyPath" /> class. </summary>
    /// <param name="value"> The SerializedProperty path. </param>
    [JsonConstructor]
    public SerializedPropertyPath (string value)
        : base(value)
    {
    }

    /// <summary> Converts a string to a SerializedProperty path contract value. </summary>
    /// <param name="value"> The SerializedProperty path. </param>
    /// <returns> The semantic SerializedProperty path value. </returns>
    public static implicit operator SerializedPropertyPath (string value)
    {
        return new SerializedPropertyPath(value);
    }
}
