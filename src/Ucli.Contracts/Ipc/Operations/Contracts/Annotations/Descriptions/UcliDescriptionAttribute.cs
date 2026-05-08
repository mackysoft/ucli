namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Provides a description for operation contract documentation and linting. </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Parameter)]
public sealed class UcliDescriptionAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="UcliDescriptionAttribute" /> class. </summary>
    /// <param name="description"> The description text. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="description" /> is empty. </exception>
    public UcliDescriptionAttribute (string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description must not be null, empty, or whitespace.", nameof(description));
        }

        Description = description;
    }

    /// <summary> Gets the description text. </summary>
    public string Description { get; }
}
