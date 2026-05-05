using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

namespace MackySoft.Ucli.Features.Daemon.Common.CommandContracts;

/// <summary> Converts daemon-status enum to daemon command contract literals. </summary>
internal static class DaemonStatusStateCodec
{
    public const string Running = "running";

    public const string NotRunning = "notRunning";

    public const string Stale = "stale";

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
