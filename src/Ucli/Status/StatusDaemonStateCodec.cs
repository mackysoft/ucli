using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Daemon;

namespace MackySoft.Ucli.Status;

/// <summary> Converts daemon status values between enum and status command contract literals. </summary>
internal static class StatusDaemonStateCodec
{
    /// <summary> Gets the daemon-status value used when daemon is running. </summary>
    public const string Running = "running";

    /// <summary> Gets the daemon-status value used when daemon is not running. </summary>
    public const string NotRunning = "notRunning";

    /// <summary> Gets the daemon-status value used when daemon session is stale. </summary>
    public const string Stale = "stale";

    /// <summary> Converts one daemon status enum value to a status contract literal. </summary>
    /// <param name="daemonStatus"> The daemon status enum value. </param>
    /// <returns> The daemon-status contract literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="daemonStatus" /> is unsupported. </exception>
    public static string ToValue (DaemonStatusKind daemonStatus)
    {
        return daemonStatus switch
        {
            DaemonStatusKind.Running => Running,
            DaemonStatusKind.NotRunning => NotRunning,
            DaemonStatusKind.Stale => Stale,
            _ => throw new ArgumentOutOfRangeException(nameof(daemonStatus), daemonStatus, "Unsupported daemon status."),
        };
    }

    /// <summary> Tries to parse one daemon-status literal to daemon status enum value. </summary>
    /// <param name="value"> The optional daemon-status literal. </param>
    /// <param name="daemonStatus"> The parsed daemon status enum when parsing succeeds; otherwise default enum value. </param>
    /// <returns> <see langword="true" /> when one supported daemon-status value is parsed; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out DaemonStatusKind daemonStatus)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalized))
        {
            daemonStatus = default;
            return false;
        }

        if (string.Equals(normalized, Running, StringComparison.Ordinal))
        {
            daemonStatus = DaemonStatusKind.Running;
            return true;
        }

        if (string.Equals(normalized, NotRunning, StringComparison.Ordinal))
        {
            daemonStatus = DaemonStatusKind.NotRunning;
            return true;
        }

        if (string.Equals(normalized, Stale, StringComparison.Ordinal))
        {
            daemonStatus = DaemonStatusKind.Stale;
            return true;
        }

        daemonStatus = default;
        return false;
    }
}