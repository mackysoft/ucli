namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Represents the lenient plan-token projection read from <c>.ucli/config.json</c>. </summary>
/// <param name="OperationPolicy"> The raw operation-policy value. </param>
/// <param name="PlanTokenMode"> The raw plan-token-mode value. </param>
/// <param name="OperationAllowlist"> The raw operation-allowlist values. </param>
internal readonly record struct UcliPlanTokenConfigJsonRawDocument (
    string? OperationPolicy,
    string? PlanTokenMode,
    string[]? OperationAllowlist);
