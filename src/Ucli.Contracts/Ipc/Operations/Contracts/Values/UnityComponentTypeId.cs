using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity type identifier assignable to a Component type. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity type identifier assignable to a Component type.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.TypeAssignableTo, TypeKind = UcliOperationTypeKind.Component)]
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
