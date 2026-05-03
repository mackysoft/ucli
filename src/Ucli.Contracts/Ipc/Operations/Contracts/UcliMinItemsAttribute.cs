using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the internal minimum item-count validation for an array property. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliMinItemsAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliMinItemsAttribute" /> class. </summary>
    /// <param name="minItems"> The minimum item count. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="minItems" /> is negative. </exception>
    public UcliMinItemsAttribute (int minItems)
    {
        if (minItems < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minItems), "Minimum item count must be zero or greater.");
        }

        MinItems = minItems;
    }

    /// <summary> Gets the minimum item count. </summary>
    public int MinItems { get; }
}
