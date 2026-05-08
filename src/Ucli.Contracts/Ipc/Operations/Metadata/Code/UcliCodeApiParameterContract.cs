namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Describes one source-facing API method parameter. </summary>
public sealed class UcliCodeApiParameterContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliCodeApiParameterContract" /> class. </summary>
    public UcliCodeApiParameterContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliCodeApiParameterContract" /> class. </summary>
    public UcliCodeApiParameterContract (
        string? name,
        string? type,
        string? description)
    {
        Name = name;
        Type = type;
        Description = description;
    }

    /// <summary> Gets or sets the parameter name. </summary>
    public string? Name { get; set; }

    /// <summary> Gets or sets the parameter type. </summary>
    public string? Type { get; set; }

    /// <summary> Gets or sets the parameter description. </summary>
    public string? Description { get; set; }
}
