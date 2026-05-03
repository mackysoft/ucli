using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Declares properties that must be present when a trigger property is present. </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class UcliPropertyDependencyAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliPropertyDependencyAttribute" /> class. </summary>
    /// <param name="triggerPropertyName"> The property name that activates this dependency. </param>
    /// <param name="requiredPropertyNames"> The property names required when the trigger property is present. </param>
    /// <exception cref="ArgumentException"> Thrown when one property name is invalid. </exception>
    public UcliPropertyDependencyAttribute (
        string triggerPropertyName,
        params string[] requiredPropertyNames)
    {
        if (string.IsNullOrWhiteSpace(triggerPropertyName))
        {
            throw new ArgumentException("Trigger property name must not be null, empty, or whitespace.", nameof(triggerPropertyName));
        }

        if (requiredPropertyNames == null || requiredPropertyNames.Length == 0)
        {
            throw new ArgumentException("At least one required property name is required.", nameof(requiredPropertyNames));
        }

        for (var i = 0; i < requiredPropertyNames.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(requiredPropertyNames[i]))
            {
                throw new ArgumentException("Required property names must not contain null, empty, or whitespace values.", nameof(requiredPropertyNames));
            }
        }

        TriggerPropertyName = triggerPropertyName;
        RequiredPropertyNames = requiredPropertyNames;
    }

    /// <summary> Gets the property name that activates this dependency. </summary>
    public string TriggerPropertyName { get; }

    /// <summary> Gets the property names required when the trigger property is present. </summary>
    public string[] RequiredPropertyNames { get; }
}
