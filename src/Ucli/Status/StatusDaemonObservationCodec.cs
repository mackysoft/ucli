using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Daemon;

namespace MackySoft.Ucli.Status;

/// <summary> Converts daemon status and ping payload values to status command observation contract values. </summary>
internal static class StatusDaemonObservationCodec
{
    /// <summary> Creates observation values for daemon states where ping details are unavailable. </summary>
    /// <param name="daemonStatus"> The daemon status enum value. </param>
    /// <returns> The observation values with null ping fields. </returns>
    public static StatusDaemonObservation CreateWithoutPing (DaemonStatusKind daemonStatus)
    {
        return new StatusDaemonObservation(
            DaemonStatus: StatusDaemonStateCodec.ToValue(daemonStatus),
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
            DaemonStatus: StatusDaemonStateCodec.ToValue(daemonStatus),
            ServerVersion: StringValueNormalizer.TrimToNull(pingResponse.ServerVersion),
            CompileState: IpcCompileStateCodec.TryParse(pingResponse.CompileState, out var compileState)
                ? compileState
                : null,
            Runtime: StringValueNormalizer.TrimToNull(pingResponse.Runtime));
    }
}