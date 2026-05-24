namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Describes one args field required by an input representation variant. </summary>
public sealed class UcliOperationInputVariantFieldContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationInputVariantFieldContract" /> class. </summary>
    public UcliOperationInputVariantFieldContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationInputVariantFieldContract" /> class. </summary>
    public UcliOperationInputVariantFieldContract (
        string? name,
        string? argsPath,
        string? description,
        IReadOnlyList<UcliOperationInputConstraintContract>? constraints)
    {
        Name = name;
        ArgsPath = argsPath;
        Description = description;
        Constraints = constraints;
    }

    /// <summary> Gets or sets the field name within the variant input object. </summary>
    public string? Name { get; set; }

    /// <summary> Gets or sets the uCLI args path written in <c>steps[].args</c>. </summary>
    public string? ArgsPath { get; set; }

    /// <summary> Gets or sets the field meaning. </summary>
    public string? Description { get; set; }

    /// <summary> Gets or sets machine-readable semantic constraints for this field value. </summary>
    public IReadOnlyList<UcliOperationInputConstraintContract>? Constraints { get; set; }
}
