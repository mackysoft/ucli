using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Represents serializable JSON DTO values for <c>.ucli/config.json</c>. </summary>
/// <param name="SchemaVersion"> The config schema version. </param>
/// <param name="OperationPolicy"> The operation-policy value. </param>
/// <param name="PlanTokenMode"> The plan-token-mode value. </param>
/// <param name="ReadIndexDefaultMode"> The read-index default mode value. </param>
/// <param name="OperationAllowlist"> The operation-name allowlist. </param>
/// <param name="IpcDefaultTimeoutMilliseconds"> The IPC default timeout value in milliseconds. </param>
/// <param name="IpcTimeoutMillisecondsByCommand">
/// <para> The per-command IPC timeout overrides in milliseconds. </para>
/// <para> <see langword="null" /> means that default command entries are generated during parse. </para>
/// </param>
internal sealed record UcliConfigDocument (
    int SchemaVersion,
    string OperationPolicy,
    string PlanTokenMode,
    string? ReadIndexDefaultMode,
    string[] OperationAllowlist,
    int? IpcDefaultTimeoutMilliseconds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Dictionary<string, int?>? IpcTimeoutMillisecondsByCommand);
