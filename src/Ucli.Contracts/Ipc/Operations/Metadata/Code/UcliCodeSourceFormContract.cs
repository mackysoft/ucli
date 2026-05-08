namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Describes one accepted source form for operations that compile user code. </summary>
public sealed class UcliCodeSourceFormContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliCodeSourceFormContract" /> class. </summary>
    public UcliCodeSourceFormContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliCodeSourceFormContract" /> class. </summary>
    public UcliCodeSourceFormContract (
        string? kind,
        string? description)
    {
        Kind = kind;
        Description = description;
    }

    /// <summary> Gets or sets the source form literal. </summary>
    public string? Kind { get; set; }

    /// <summary> Gets or sets the source form description. </summary>
    public string? Description { get; set; }
}
