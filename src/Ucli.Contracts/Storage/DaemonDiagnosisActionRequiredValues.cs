namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines normalized daemon diagnosis action-required values. </summary>
public static class DaemonDiagnosisActionRequiredValues
{
    /// <summary> Gets the action value used when script compilation errors must be fixed. </summary>
    public const string FixCompileErrors = "fixCompileErrors";

    /// <summary> Gets the action value used when Unity packages must be resolved. </summary>
    public const string ResolvePackages = "resolvePackages";

    /// <summary> Gets the action value used when a Unity GUI dialog must be resolved by the user. </summary>
    public const string ResolveUnityDialog = "resolveUnityDialog";

    /// <summary> Gets the action value used when the Unity log must be inspected. </summary>
    public const string InspectUnityLog = "inspectUnityLog";

    /// <summary> Determines whether one daemon diagnosis action-required value is supported. </summary>
    public static bool IsSupported (string value)
    {
        return string.Equals(value, FixCompileErrors, StringComparison.Ordinal)
            || string.Equals(value, ResolvePackages, StringComparison.Ordinal)
            || string.Equals(value, ResolveUnityDialog, StringComparison.Ordinal)
            || string.Equals(value, InspectUnityLog, StringComparison.Ordinal);
    }
}
