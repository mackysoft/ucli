namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Requires a reference resolvable to a specified Unity target kind. </summary>
public sealed class UcliReferenceResolvableAttribute : UcliOperationInputConstraintAnnotationAttribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliReferenceResolvableAttribute" /> class. </summary>
    /// <param name="targetKind"> The target kind to which the reference must resolve. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="targetKind" /> is undefined. </exception>
    public UcliReferenceResolvableAttribute (UcliOperationReferenceTargetKind targetKind)
    {
        if (!TextVocabulary.IsDefined(targetKind))
        {
            throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, "Reference target kind must be defined by the operation contract.");
        }

        TargetKind = targetKind;
    }

    /// <summary> Gets the target kind to which the reference must resolve. </summary>
    public UcliOperationReferenceTargetKind TargetKind { get; }
}
