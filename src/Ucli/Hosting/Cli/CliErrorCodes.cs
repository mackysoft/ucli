namespace MackySoft.Ucli.Hosting.Cli;

/// <summary> Defines CLI-host specific machine-readable error code values. </summary>
internal static class CliErrorCodes
{
    /// <summary> Gets the error code used when command execution is canceled. </summary>
    public const string Canceled = "CANCELED";

    /// <summary> Gets the error code used when IPC execution exceeds configured timeout. </summary>
    public const string IpcTimeout = "IPC_TIMEOUT";

}