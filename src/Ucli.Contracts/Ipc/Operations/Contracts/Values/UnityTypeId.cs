using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity type identifier that must resolve in the project. </summary>
[UcliDescription("Unity type identifier that must resolve in the project.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.TypeExists)]
public sealed record UnityTypeId : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityTypeId" /> class. </summary>
    /// <param name="value"> The Unity type identifier. </param>
    [JsonConstructor]
    public UnityTypeId (string value)
        : base(value)
    {
    }

    /// <summary> Converts a string to a Unity type identifier contract value. </summary>
    /// <param name="value"> The Unity type identifier. </param>
    /// <returns> The semantic Unity type identifier value. </returns>
    public static implicit operator UnityTypeId (string value)
    {
        return new UnityTypeId(value);
    }
}
