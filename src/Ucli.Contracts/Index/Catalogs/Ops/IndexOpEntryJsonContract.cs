using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one persisted op entry in <c>ops.catalog.json</c>. </summary>
/// <param name="Name"> The operation name. </param>
/// <param name="Kind"> The operation-kind literal. </param>
/// <param name="Policy"> The operation-policy literal. </param>
/// <param name="ArgsSchemaJson"> The JSON object text that describes operation args. </param>
/// <param name="ResultSchemaJson"> The JSON object text that describes operation result, or <see langword="null" /> when no result is emitted. </param>
public sealed record IndexOpEntryJsonContract (
    string? Name,
    string? Kind,
    string? Policy,
    string? ArgsSchemaJson,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ResultSchemaJson = null)
{
    /// <summary> Gets or initializes the operation purpose description. </summary>
    public string? Description { get; init; }

    /// <summary> Gets or initializes input contracts used to build <c>steps[].args</c>. </summary>
    public IReadOnlyList<UcliOperationInputContract>? Inputs { get; init; }

    /// <summary> Gets or initializes the contract for interpreting <c>opResults[].result</c>. </summary>
    public UcliOperationResultContract? ResultContract { get; init; }

    /// <summary> Gets or initializes machine-readable assurance metadata. </summary>
    public UcliOperationAssuranceContract? Assurance { get; init; }

    /// <summary> Gets or initializes optional source-facing code metadata. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UcliOperationCodeContract? CodeContract { get; init; }
}
