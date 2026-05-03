using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Describes one user-facing input used to build operation args. </summary>
public sealed class UcliOperationInputContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationInputContract" /> class. </summary>
    public UcliOperationInputContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationInputContract" /> class. </summary>
    public UcliOperationInputContract (
        string? name,
        string? valueType,
        string? description,
        IReadOnlyList<UcliOperationInputConstraintContract>? constraints,
        string? argsPath = null,
        IReadOnlyList<UcliOperationInputVariantContract>? variants = null)
    {
        Name = name;
        Description = description;
        ValueType = valueType;
        Constraints = constraints;
        ArgsPath = argsPath;
        Variants = variants;
    }

    /// <summary> Gets or sets the input name. </summary>
    public string? Name { get; set; }

    /// <summary> Gets or sets the input meaning. </summary>
    public string? Description { get; set; }

    /// <summary> Gets or sets the JSON value type. </summary>
    public string? ValueType { get; set; }

    /// <summary> Gets or sets machine-readable semantic constraints for the input. </summary>
    public IReadOnlyList<UcliOperationInputConstraintContract>? Constraints { get; set; }

    /// <summary> Gets or sets the uCLI args path written in <c>steps[].args</c> when it differs from the input name. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ArgsPath { get; set; }

    /// <summary> Gets or sets selector or reference representation variants. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<UcliOperationInputVariantContract>? Variants { get; set; }
}
