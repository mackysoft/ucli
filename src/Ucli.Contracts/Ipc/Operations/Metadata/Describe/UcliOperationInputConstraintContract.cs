using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Describes one machine-readable semantic constraint for an operation input. </summary>
public sealed class UcliOperationInputConstraintContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationInputConstraintContract" /> class. </summary>
    public UcliOperationInputConstraintContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationInputConstraintContract" /> class. </summary>
    /// <param name="kind"> The constraint kind literal. </param>
    public UcliOperationInputConstraintContract (string? kind)
    {
        Kind = kind;
    }

    /// <summary> Gets or sets the constraint kind literal. </summary>
    public string? Kind { get; set; }

    /// <summary> Gets or sets the asset kind parameter. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssetKind { get; set; }

    /// <summary> Gets or sets the reference target kind parameter. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetKind { get; set; }

    /// <summary> Gets or sets the type kind parameter. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeKind { get; set; }

    /// <summary> Gets or sets the serialized property access parameter. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Access { get; set; }

    /// <summary> Gets or sets the inclusive minimum range value. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Min { get; set; }

    /// <summary> Gets or sets the inclusive maximum range value. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Max { get; set; }
}
