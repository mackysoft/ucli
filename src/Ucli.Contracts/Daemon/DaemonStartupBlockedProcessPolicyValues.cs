namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon startup-blocked process-policy contract literal values. </summary>
public static class DaemonStartupBlockedProcessPolicyValues
{
    /// <summary> Gets the value that applies the default process policy for the current startup context. </summary>
    public const string Auto = "auto";

    /// <summary> Gets the value that leaves the Unity process running after a startup blocker is detected. </summary>
    public const string Keep = "keep";

    /// <summary> Gets the value that terminates a manageable Unity process after a startup blocker is detected. </summary>
    public const string Terminate = "terminate";
}
