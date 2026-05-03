using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Adds one internal required-property alternative for exactly-one validation. </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class UcliOneOfRequiredAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliOneOfRequiredAttribute" /> class. </summary>
    /// <param name="propertyNames"> The property names required by this alternative. </param>
    /// <exception cref="ArgumentException"> Thrown when no valid property name is supplied. </exception>
    public UcliOneOfRequiredAttribute (params string[] propertyNames)
    {
        if (propertyNames == null || propertyNames.Length == 0)
        {
            throw new ArgumentException("At least one property name is required.", nameof(propertyNames));
        }

        for (var i = 0; i < propertyNames.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(propertyNames[i]))
            {
                throw new ArgumentException("Property names must not contain null, empty, or whitespace values.", nameof(propertyNames));
            }
        }

        PropertyNames = propertyNames;
    }

    /// <summary> Gets the required property names for this alternative. </summary>
    public string[] PropertyNames { get; }
}
