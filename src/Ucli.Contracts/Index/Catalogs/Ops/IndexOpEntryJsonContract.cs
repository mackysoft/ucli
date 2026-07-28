using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one persisted op entry in <c>ops.catalog.json</c>. </summary>
/// <param name="Name"> The operation name. </param>
/// <param name="Kind"> The operation-kind literal. </param>
/// <param name="Policy"> The operation-policy literal. </param>
/// <param name="ArgsContract"> The generated operation argument contract. </param>
/// <param name="ResultContract">
/// The generated operation result contract, or <see langword="null" /> when no result is emitted.
/// </param>
/// <param name="PlayModeSupport"> The Play Mode support literal for public raw operation execution. </param>
public sealed record IndexOpEntryJsonContract (
    string? Name,
    UcliOperationKind? Kind,
    OperationPolicy? Policy,
    UcliOperationJsonContract? ArgsContract,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    UcliOperationJsonContract? ResultContract = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    UcliOperationExposure? Exposure = null,
    UcliOperationPlayModeSupport? PlayModeSupport = UcliOperationPlayModeSupport.Disallowed)
{
    /// <summary> Gets or initializes the operation purpose description. </summary>
    public string? Description { get; init; }

    /// <summary> Gets or initializes machine-readable assurance metadata. </summary>
    public UcliOperationAssuranceContract? Assurance { get; init; }

    /// <summary> Gets or initializes optional source-facing code metadata. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UcliOperationCodeContract? CodeContract { get; init; }
}
