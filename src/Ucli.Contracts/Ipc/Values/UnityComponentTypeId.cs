using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a Unity type identifier assignable to a Component type. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[Length(1, int.MaxValue)]
[UcliTypeAssignableTo(UcliOperationTypeKind.Component)]
public sealed class UnityComponentTypeId : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityComponentTypeId" /> class. </summary>
    /// <param name="value"> The Unity component type identifier. </param>
    [JsonConstructor]
    public UnityComponentTypeId (string value)
        : base(value)
    {
    }
}
