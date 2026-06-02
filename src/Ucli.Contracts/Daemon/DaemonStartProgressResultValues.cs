namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines stable result literals used by host-visible <c>daemon.start</c> progress entries. </summary>
public static class DaemonStartProgressResultValues
{
    /// <summary> Gets the result literal used when the observed host-visible step succeeded. </summary>
    public const string Succeeded = "succeeded";

    /// <summary> Gets the result literal used when the observed host-visible step failed. </summary>
    public const string Failed = "failed";
}
