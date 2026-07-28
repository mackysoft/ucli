namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Requires a Unity type assignable to a specified type kind. </summary>
public sealed class UcliTypeAssignableToAttribute : UcliOperationInputConstraintAnnotationAttribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliTypeAssignableToAttribute" /> class. </summary>
    /// <param name="typeKind"> The Unity type kind to which the type must be assignable. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="typeKind" /> is undefined. </exception>
    public UcliTypeAssignableToAttribute (UcliOperationTypeKind typeKind)
    {
        if (!TextVocabulary.IsDefined(typeKind))
        {
            throw new ArgumentOutOfRangeException(nameof(typeKind), typeKind, "Type kind must be defined by the operation contract.");
        }

        TypeKind = typeKind;
    }

    /// <summary> Gets the Unity type kind to which the type must be assignable. </summary>
    public UcliOperationTypeKind TypeKind { get; }
}
