namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Declares one required property set in an exclusive structural group. </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class UcliExclusiveRequiredPropertySetAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliExclusiveRequiredPropertySetAttribute" /> class. </summary>
    /// <param name="propertyNames"> The property names that must be present for this required property set to match. </param>
    /// <exception cref="ArgumentException"> Thrown when no valid property name is supplied. </exception>
    public UcliExclusiveRequiredPropertySetAttribute (params string[] propertyNames)
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

        RequiredPropertyNames = propertyNames;
    }

    /// <summary> Gets the property names that must be present for this required property set to match. </summary>
    public string[] RequiredPropertyNames { get; }
}
