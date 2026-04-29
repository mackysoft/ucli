namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines stable JSON property names used by <c>.ucli/config.json</c>. </summary>
internal static class UcliConfigJsonPropertyNames
{
    /// <summary> Gets the schema-version property name. </summary>
    public const string SchemaVersion = "schemaVersion";

    /// <summary> Gets the operation-policy property name. </summary>
    public const string OperationPolicy = "operationPolicy";

    /// <summary> Gets the plan-token-mode property name. </summary>
    public const string PlanTokenMode = "planTokenMode";

    /// <summary> Gets the read-index-default-mode property name. </summary>
    public const string ReadIndexDefaultMode = "readIndexDefaultMode";

    /// <summary> Gets the operation-allowlist property name. </summary>
    public const string OperationAllowlist = "operationAllowlist";

    /// <summary> Gets the IPC default-timeout property name. </summary>
    public const string IpcDefaultTimeoutMilliseconds = "ipcDefaultTimeoutMilliseconds";

    /// <summary> Gets the IPC timeout override-map property name. </summary>
    public const string IpcTimeoutMillisecondsByCommand = "ipcTimeoutMillisecondsByCommand";
}
