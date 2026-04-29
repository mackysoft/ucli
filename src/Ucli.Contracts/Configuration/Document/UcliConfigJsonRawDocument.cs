namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Represents raw values read from <c>.ucli/config.json</c> before product-specific validation. </summary>
/// <param name="SchemaVersion"> The raw schema-version value. </param>
/// <param name="OperationPolicy"> The raw operation-policy value. </param>
/// <param name="PlanTokenMode"> The raw plan-token-mode value. </param>
/// <param name="ReadIndexDefaultMode"> The raw read-index-default-mode value. </param>
/// <param name="OperationAllowlist"> The raw operation-allowlist values. </param>
/// <param name="IpcDefaultTimeoutMilliseconds"> The raw IPC default-timeout value. </param>
/// <param name="IpcTimeoutMillisecondsByCommand"> The raw IPC timeout override-map values. </param>
internal readonly record struct UcliConfigJsonRawDocument (
    int? SchemaVersion,
    string? OperationPolicy,
    string? PlanTokenMode,
    string? ReadIndexDefaultMode,
    string[]? OperationAllowlist,
    int? IpcDefaultTimeoutMilliseconds,
    Dictionary<string, int?>? IpcTimeoutMillisecondsByCommand);
