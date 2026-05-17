namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines application-state literals for execute contract violation payloads. </summary>
public static class IpcExecuteContractViolationApplicationStateNames
{
    /// <summary> The operation result was applied. </summary>
    public const string Applied = "applied";

    /// <summary> The operation result was not applied. </summary>
    public const string NotApplied = "notApplied";

    /// <summary> The operation result leaves application state indeterminate. </summary>
    public const string Indeterminate = "indeterminate";

    /// <summary> The operation result did not provide enough evidence to classify application state. </summary>
    public const string Unknown = "unknown";
}
