namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Describes a type or member exposed through the operation code API. </summary>
[AttributeUsage(
    AttributeTargets.Class
    | AttributeTargets.Struct
    | AttributeTargets.Property
    | AttributeTargets.Method
    | AttributeTargets.Parameter)]
public sealed class UcliCodeDescriptionAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliCodeDescriptionAttribute" /> class. </summary>
    /// <param name="description"> The code API description. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="description" /> is empty. </exception>
    public UcliCodeDescriptionAttribute (string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Code API description must not be null, empty, or whitespace.", nameof(description));
        }

        Description = description;
    }

    /// <summary> Gets the code API description. </summary>
    public string Description { get; }
}
