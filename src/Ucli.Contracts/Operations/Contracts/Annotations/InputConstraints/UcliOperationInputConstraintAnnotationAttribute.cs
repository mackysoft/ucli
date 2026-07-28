namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Identifies an annotation that contributes one uCLI operation-input constraint. </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property, Inherited = true)]
public abstract class UcliOperationInputConstraintAnnotationAttribute : Attribute
{
}
