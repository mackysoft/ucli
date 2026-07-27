namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Declares an inclusive range for one nullable 32-bit integer input. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliInt32RangeAttribute : Attribute
{
    /// <summary> Initializes a new inclusive range declaration. </summary>
    /// <param name="minimum"> The inclusive minimum value. </param>
    /// <param name="maximum"> The inclusive maximum value. </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maximum" /> is less than <paramref name="minimum" />.
    /// </exception>
    public UcliInt32RangeAttribute (
        int minimum,
        int maximum)
    {
        if (maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum must be greater than or equal to minimum.");
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary> Gets the inclusive minimum value. </summary>
    public int Minimum { get; }

    /// <summary> Gets the inclusive maximum value. </summary>
    public int Maximum { get; }
}
