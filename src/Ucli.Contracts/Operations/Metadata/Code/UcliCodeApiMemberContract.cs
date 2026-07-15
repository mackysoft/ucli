namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Describes one member visible on a source-facing API type. </summary>
public sealed class UcliCodeApiMemberContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliCodeApiMemberContract" /> class. </summary>
    public UcliCodeApiMemberContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliCodeApiMemberContract" /> class. </summary>
    public UcliCodeApiMemberContract (
        UcliCodeApiMemberKind? kind,
        string? name,
        string? description,
        string? type,
        string? returnType,
        IReadOnlyList<UcliCodeApiParameterContract>? parameters)
    {
        Kind = kind;
        Name = name;
        Description = description;
        Type = type;
        ReturnType = returnType;
        Parameters = parameters;
    }

    /// <summary> Gets or sets the member kind. </summary>
    public UcliCodeApiMemberKind? Kind { get; set; }

    /// <summary> Gets or sets the member name. </summary>
    public string? Name { get; set; }

    /// <summary> Gets or sets the member description. </summary>
    public string? Description { get; set; }

    /// <summary> Gets or sets the property type for property members. </summary>
    public string? Type { get; set; }

    /// <summary> Gets or sets the return type for method members. </summary>
    public string? ReturnType { get; set; }

    /// <summary> Gets or sets method parameters. </summary>
    public IReadOnlyList<UcliCodeApiParameterContract>? Parameters { get; set; }
}
