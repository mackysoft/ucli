namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Defines daemon-list state literals. </summary>
internal static class DaemonListStateCodec
{
    /// <summary> Gets the state literal for reachable daemon registrations. </summary>
    public const string Running = "running";

    /// <summary> Gets the state literal for stale daemon registrations. </summary>
    public const string Stale = "stale";

    /// <summary> Gets the state literal for daemon registrations that could not be fully observed. </summary>
    public const string Error = "error";
}