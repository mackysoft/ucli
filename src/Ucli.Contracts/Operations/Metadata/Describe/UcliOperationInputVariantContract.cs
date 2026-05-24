namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Describes one representation variant for the same operation input. </summary>
public sealed class UcliOperationInputVariantContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationInputVariantContract" /> class. </summary>
    public UcliOperationInputVariantContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationInputVariantContract" /> class. </summary>
    public UcliOperationInputVariantContract (
        string? name,
        string? description,
        IReadOnlyList<UcliOperationInputVariantFieldContract>? fields)
    {
        Name = name;
        Description = description;
        Fields = fields;
    }

    /// <summary> Gets or sets the variant name. </summary>
    public string? Name { get; set; }

    /// <summary> Gets or sets the variant meaning. </summary>
    public string? Description { get; set; }

    /// <summary> Gets or sets args fields required by this representation. </summary>
    public IReadOnlyList<UcliOperationInputVariantFieldContract>? Fields { get; set; }
}
