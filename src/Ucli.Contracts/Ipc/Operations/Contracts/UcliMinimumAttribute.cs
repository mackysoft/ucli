using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the internal minimum numeric validation for a numeric property. </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliMinimumAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliMinimumAttribute" /> class. </summary>
    /// <param name="minimum"> The minimum numeric value. </param>
    public UcliMinimumAttribute (double minimum)
    {
        Minimum = minimum;
    }

    /// <summary> Gets the minimum numeric value. </summary>
    public double Minimum { get; }
}
