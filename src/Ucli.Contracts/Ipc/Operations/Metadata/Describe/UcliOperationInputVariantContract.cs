namespace MackySoft.Ucli.Contracts.Ipc;

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
        IReadOnlyList<string>? argsPaths,
        IReadOnlyList<UcliOperationInputConstraintContract>? constraints)
    {
        Name = name;
        Description = description;
        ArgsPaths = argsPaths;
        Constraints = constraints;
    }

    /// <summary> Gets or sets the variant name. </summary>
    public string? Name { get; set; }

    /// <summary> Gets or sets the variant meaning. </summary>
    public string? Description { get; set; }

    /// <summary> Gets or sets uCLI args paths this variant writes in <c>steps[].args</c>. </summary>
    public IReadOnlyList<string>? ArgsPaths { get; set; }

    /// <summary> Gets or sets machine-readable semantic constraints for this representation. </summary>
    public IReadOnlyList<UcliOperationInputConstraintContract>? Constraints { get; set; }
}
