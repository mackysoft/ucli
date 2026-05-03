using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the internal minimum populated-property validation for an object contract. </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class UcliMinPropertiesAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliMinPropertiesAttribute" /> class. </summary>
    /// <param name="minProperties"> The minimum property count. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="minProperties" /> is negative. </exception>
    public UcliMinPropertiesAttribute (int minProperties)
    {
        if (minProperties < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minProperties), "Minimum property count must be zero or greater.");
        }

        MinProperties = minProperties;
    }

    /// <summary> Gets the minimum property count. </summary>
    public int MinProperties { get; }
}
