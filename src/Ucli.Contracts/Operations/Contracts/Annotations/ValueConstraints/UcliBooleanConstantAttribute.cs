namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Declares the only permitted value for one Boolean contract. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliBooleanConstantAttribute : Attribute
{
    /// <summary> Initializes a new constant-value declaration. </summary>
    /// <param name="value"> The only permitted value. </param>
    public UcliBooleanConstantAttribute (bool value)
    {
        Value = value;
    }

    /// <summary> Gets the only permitted value. </summary>
    public bool Value { get; }
}
