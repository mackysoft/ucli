namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

/// <summary> Defines daemon startup observation status values. </summary>
internal static class DaemonStartupStatusValues
{
    public const string Launching = "launching";

    public const string WaitingForEndpoint = "waitingForEndpoint";

    public const string Blocked = "blocked";

    public const string Timeout = "timeout";

    public const string Failed = "failed";

    public const string Completed = "completed";
}
