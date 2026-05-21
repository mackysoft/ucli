namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines application-state literals used by Play Mode lifecycle transition payloads. </summary>
public static class IpcPlayApplicationStateNames
{
    /// <summary> Gets the literal for transitions that are known not to have been applied. </summary>
    public const string NotApplied = "notApplied";

    /// <summary> Gets the literal for transitions that are known to have been applied. </summary>
    public const string Applied = "applied";

    /// <summary> Gets the literal for transitions whose application state cannot be determined from reliable evidence. </summary>
    public const string Indeterminate = "indeterminate";

    /// <summary> Gets the literal for transitions whose application state is unknown because no reliable envelope exists. </summary>
    public const string Unknown = "unknown";
}
