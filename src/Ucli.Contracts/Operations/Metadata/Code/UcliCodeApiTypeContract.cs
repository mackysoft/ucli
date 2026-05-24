namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Describes one API type visible to compiled operation source. </summary>
public sealed class UcliCodeApiTypeContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliCodeApiTypeContract" /> class. </summary>
    public UcliCodeApiTypeContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliCodeApiTypeContract" /> class. </summary>
    public UcliCodeApiTypeContract (
        string? name,
        string? fullName,
        string? description,
        IReadOnlyList<UcliCodeApiMemberContract>? members)
    {
        Name = name;
        FullName = fullName;
        Description = description;
        Members = members;
    }

    /// <summary> Gets or sets the API type name. </summary>
    public string? Name { get; set; }

    /// <summary> Gets or sets the API type full name. </summary>
    public string? FullName { get; set; }

    /// <summary> Gets or sets the API type description. </summary>
    public string? Description { get; set; }

    /// <summary> Gets or sets the public API members exposed by this type. </summary>
    public IReadOnlyList<UcliCodeApiMemberContract>? Members { get; set; }
}
