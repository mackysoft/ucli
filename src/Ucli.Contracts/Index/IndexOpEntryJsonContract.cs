namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one persisted op entry in <c>ops.catalog.json</c>. </summary>
/// <param name="Name"> The operation name. </param>
/// <param name="Kind"> The operation-kind literal. </param>
/// <param name="Policy"> The operation-policy literal. </param>
/// <param name="ArgsSchemaJson"> The JSON object text that describes operation args. </param>
public sealed record IndexOpEntryJsonContract (
    string? Name,
    string? Kind,
    string? Policy,
    string? ArgsSchemaJson);