namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines normalized daemon diagnosis startup-phase values. </summary>
public static class DaemonDiagnosisStartupPhaseValues
{
    /// <summary> Gets the phase value used when Unity script compilation blocks startup. </summary>
    public const string ScriptCompilation = "scriptCompilation";

    /// <summary> Gets the phase value used when Unity package resolution blocks startup. </summary>
    public const string PackageResolution = "packageResolution";

    /// <summary> Gets the phase value used when Unity Editor is waiting for user action. </summary>
    public const string UserAction = "userAction";

    /// <summary> Gets the phase value used when Unity Editor exits before bootstrap completes. </summary>
    public const string ProcessExit = "processExit";

    /// <summary> Gets the phase value used while waiting for GUI endpoint registration. </summary>
    public const string EndpointRegistration = "endpointRegistration";

    /// <summary> Determines whether one daemon diagnosis startup-phase value is supported. </summary>
    public static bool IsSupported (string value)
    {
        return string.Equals(value, ScriptCompilation, StringComparison.Ordinal)
            || string.Equals(value, PackageResolution, StringComparison.Ordinal)
            || string.Equals(value, UserAction, StringComparison.Ordinal)
            || string.Equals(value, ProcessExit, StringComparison.Ordinal)
            || string.Equals(value, EndpointRegistration, StringComparison.Ordinal);
    }
}
