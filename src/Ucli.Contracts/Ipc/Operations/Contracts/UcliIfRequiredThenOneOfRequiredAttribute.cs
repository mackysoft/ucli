using System;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Adds one internal conditional required-property validation rule. </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class UcliIfRequiredThenOneOfRequiredAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliIfRequiredThenOneOfRequiredAttribute" /> class. </summary>
    /// <param name="conditionPropertyName"> The property name required by the <c>if</c> clause. </param>
    /// <param name="thenPropertyNames"> The property names required by the <c>then.oneOf</c> clause. </param>
    /// <exception cref="ArgumentException"> Thrown when one property name is invalid. </exception>
    public UcliIfRequiredThenOneOfRequiredAttribute (
        string conditionPropertyName,
        params string[] thenPropertyNames)
    {
        if (string.IsNullOrWhiteSpace(conditionPropertyName))
        {
            throw new ArgumentException("Condition property name must not be null, empty, or whitespace.", nameof(conditionPropertyName));
        }

        if (thenPropertyNames == null || thenPropertyNames.Length == 0)
        {
            throw new ArgumentException("At least one then property name is required.", nameof(thenPropertyNames));
        }

        for (var i = 0; i < thenPropertyNames.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(thenPropertyNames[i]))
            {
                throw new ArgumentException("Then property names must not contain null, empty, or whitespace values.", nameof(thenPropertyNames));
            }
        }

        ConditionPropertyName = conditionPropertyName;
        ThenPropertyNames = thenPropertyNames;
    }

    /// <summary> Gets the property name required by the <c>if</c> clause. </summary>
    public string ConditionPropertyName { get; }

    /// <summary> Gets the property names required by the <c>then.oneOf</c> clause. </summary>
    public string[] ThenPropertyNames { get; }
}
