namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines normalized daemon diagnosis primary-diagnostic kind values. </summary>
public static class DaemonDiagnosisPrimaryDiagnosticKindValues
{
    /// <summary> Gets the kind value used for C# compiler diagnostics. </summary>
    public const string Compiler = "compiler";

    /// <summary> Gets the kind value used for Unity package resolution diagnostics. </summary>
    public const string PackageResolution = "packageResolution";

    /// <summary> Gets the kind value used for uCLI plugin dependency diagnostics. </summary>
    public const string PluginDependency = "pluginDependency";

    /// <summary> Gets the kind value used for Unity GUI user-action diagnostics. </summary>
    public const string UnityDialog = "unityDialog";

    /// <summary> Gets the kind value used when the Unity process exits before bootstrap completes. </summary>
    public const string ProcessExit = "processExit";

    /// <summary> Determines whether one daemon diagnosis primary-diagnostic kind value is supported. </summary>
    public static bool IsSupported (string value)
    {
        return string.Equals(value, Compiler, StringComparison.Ordinal)
            || string.Equals(value, PackageResolution, StringComparison.Ordinal)
            || string.Equals(value, PluginDependency, StringComparison.Ordinal)
            || string.Equals(value, UnityDialog, StringComparison.Ordinal)
            || string.Equals(value, ProcessExit, StringComparison.Ordinal);
    }
}
