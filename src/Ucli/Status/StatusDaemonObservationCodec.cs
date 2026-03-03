using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Daemon;

namespace MackySoft.Ucli.Status;

/// <summary> Converts daemon status and ping payload values to status command observation contract values. </summary>
internal static class StatusDaemonObservationCodec
{
    private const string DaemonStatusRunning = "running";

    private const string DaemonStatusNotRunning = "notRunning";

    private const string DaemonStatusStale = "stale";

    /// <summary> Creates observation values for daemon states where ping details are unavailable. </summary>
    /// <param name="daemonStatus"> The daemon status enum value. </param>
    /// <returns> The observation values with null ping fields. </returns>
    public static StatusDaemonObservation CreateWithoutPing (DaemonStatusKind daemonStatus)
    {
        return new StatusDaemonObservation(
            DaemonStatus: ToDaemonStatusValue(daemonStatus),
            ServerVersion: null,
            CompileState: null,
            Runtime: null);
    }

    /// <summary> Creates observation values for daemon states where ping details are available. </summary>
    /// <param name="daemonStatus"> The daemon status enum value. </param>
    /// <param name="pingResponse"> The ping response payload. </param>
    /// <returns> The observation values projected for status payload. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pingResponse" /> is <see langword="null" />. </exception>
    public static StatusDaemonObservation CreateFromPing (
        DaemonStatusKind daemonStatus,
        IpcPingResponse pingResponse)
    {
        ArgumentNullException.ThrowIfNull(pingResponse);

        return new StatusDaemonObservation(
            DaemonStatus: ToDaemonStatusValue(daemonStatus),
            ServerVersion: StringValueNormalizer.TrimToNull(pingResponse.ServerVersion),
            CompileState: IpcCompileStateCodec.TryParse(pingResponse.CompileState, out var compileState)
                ? compileState
                : null,
            Runtime: StringValueNormalizer.TrimToNull(pingResponse.Runtime));
    }

    /// <summary> Converts daemon status enum values to status-payload literals. </summary>
    /// <param name="daemonStatus"> The daemon status enum value. </param>
    /// <returns> The daemon-status literal for status payload. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="daemonStatus" /> is unsupported. </exception>
    public static string ToDaemonStatusValue (DaemonStatusKind daemonStatus)
    {
        return daemonStatus switch
        {
            DaemonStatusKind.Running => DaemonStatusRunning,
            DaemonStatusKind.NotRunning => DaemonStatusNotRunning,
            DaemonStatusKind.Stale => DaemonStatusStale,
            _ => throw new ArgumentOutOfRangeException(nameof(daemonStatus), daemonStatus, "Unsupported daemon status."),
        };
    }
}