namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Declares an inclusive minimum for one 32-bit integer contract. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliInt32MinimumAttribute : Attribute
{
    /// <summary> Initializes a new inclusive minimum declaration. </summary>
    /// <param name="minimum"> The inclusive minimum value. </param>
    public UcliInt32MinimumAttribute (int minimum)
    {
        Minimum = minimum;
    }

    /// <summary> Gets the inclusive minimum value. </summary>
    public int Minimum { get; }
}
