using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a Unity SerializedProperty path. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[Length(1, int.MaxValue)]
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
