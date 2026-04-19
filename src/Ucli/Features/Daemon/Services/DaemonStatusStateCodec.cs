using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Converts daemon-status enum to daemon command contract literals. </summary>
internal static class DaemonStatusStateCodec
{
    /// <summary> Gets the daemon-status value used when daemon is running. </summary>
    public const string Running = "running";

    /// <summary> Gets the daemon-status value used when daemon is not running. </summary>
    public const string NotRunning = "notRunning";

    /// <summary> Gets the daemon-status value used when daemon session is stale. </summary>
    public const string Stale = "stale";

    /// <summary> Converts daemon-status enum to command contract literal. </summary>
    /// <param name="status"> The daemon-status enum value. </param>
    /// <param name="value"> The converted daemon-status literal when conversion succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when conversion succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryToValue (
        DaemonStatusKind status,
        [NotNullWhen(true)]
        out string? value)
    {
        value = status switch
        {
            DaemonStatusKind.Running => Running,
            DaemonStatusKind.NotRunning => NotRunning,
            DaemonStatusKind.Stale => Stale,
            _ => null,
        };
        return value is not null;
    }
}