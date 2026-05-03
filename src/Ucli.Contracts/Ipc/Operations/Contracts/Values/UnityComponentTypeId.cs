using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity type identifier assignable to a Component type. </summary>
[UcliDescription("Unity type identifier assignable to a Component type.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.TypeAssignableTo, TypeKind = UcliOperationTypeKind.Component)]
public sealed record UnityComponentTypeId : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityComponentTypeId" /> class. </summary>
    /// <param name="value"> The Unity component type identifier. </param>
    [JsonConstructor]
    public UnityComponentTypeId (string value)
        : base(value)
    {
    }

    /// <summary> Converts a string to a component type identifier contract value. </summary>
    /// <param name="value"> The Unity component type identifier. </param>
    /// <returns> The semantic component type identifier value. </returns>
    public static implicit operator UnityComponentTypeId (string value)
    {
        return new UnityComponentTypeId(value);
    }
}
