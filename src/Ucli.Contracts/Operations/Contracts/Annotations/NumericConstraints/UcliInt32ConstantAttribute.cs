namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Declares the only permitted value for one 32-bit integer contract. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliInt32ConstantAttribute : Attribute
{
    /// <summary> Initializes a new constant-value declaration. </summary>
    /// <param name="value"> The only permitted value. </param>
    public UcliInt32ConstantAttribute (int value)
    {
        Value = value;
    }

    /// <summary> Gets the only permitted value. </summary>
    public int Value { get; }
}
