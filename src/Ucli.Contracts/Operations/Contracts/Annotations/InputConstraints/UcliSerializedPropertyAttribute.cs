namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Requires a Unity SerializedProperty supporting a specified access capability. </summary>
public sealed class UcliSerializedPropertyAttribute : UcliOperationInputConstraintAnnotationAttribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliSerializedPropertyAttribute" /> class. </summary>
    /// <param name="access"> The required SerializedProperty access capability. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="access" /> is undefined. </exception>
    public UcliSerializedPropertyAttribute (UcliOperationSerializedPropertyAccess access)
    {
        if (!TextVocabulary.IsDefined(access))
        {
            throw new ArgumentOutOfRangeException(nameof(access), access, "SerializedProperty access must be defined by the operation contract.");
        }

        Access = access;
    }

    /// <summary> Gets the required SerializedProperty access capability. </summary>
    public UcliOperationSerializedPropertyAccess Access { get; }
}
