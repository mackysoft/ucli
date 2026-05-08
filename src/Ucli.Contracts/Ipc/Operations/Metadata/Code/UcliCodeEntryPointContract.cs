namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Describes the required user-code entry point shape. </summary>
public sealed class UcliCodeEntryPointContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliCodeEntryPointContract" /> class. </summary>
    public UcliCodeEntryPointContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliCodeEntryPointContract" /> class. </summary>
    public UcliCodeEntryPointContract (
        string? signature,
        bool requiredStatic,
        IReadOnlyList<string>? parameterTypes,
        string? returnValue)
    {
        Signature = signature;
        RequiredStatic = requiredStatic;
        ParameterTypes = parameterTypes;
        ReturnValue = returnValue;
    }

    /// <summary> Gets or sets the C# signature shown to callers. </summary>
    public string? Signature { get; set; }

    /// <summary> Gets or sets a value indicating whether the entry point must be static. </summary>
    public bool RequiredStatic { get; set; }

    /// <summary> Gets or sets the required parameter type names in order. </summary>
    public IReadOnlyList<string>? ParameterTypes { get; set; }

    /// <summary> Gets or sets the return value contract. </summary>
    public string? ReturnValue { get; set; }
}
