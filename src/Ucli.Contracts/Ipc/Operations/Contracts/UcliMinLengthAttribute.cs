using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the internal minimum length validation for a string property. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliMinLengthAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliMinLengthAttribute" /> class. </summary>
    /// <param name="minLength"> The minimum string length. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="minLength" /> is negative. </exception>
    public UcliMinLengthAttribute (int minLength)
    {
        if (minLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minLength), "Minimum length must be zero or greater.");
        }

        MinLength = minLength;
    }

    /// <summary> Gets the minimum string length. </summary>
    public int MinLength { get; }
}
