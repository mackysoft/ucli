using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

namespace MackySoft.Ucli.Features.Daemon.Common.CommandContracts;

/// <summary> Converts daemon-stop status enum to daemon command contract literals. </summary>
internal static class DaemonStopStateCodec
{
    public const string Stopped = "stopped";

    public const string NotRunning = "notRunning";

    public static bool TryToValue (
        DaemonStopStatus status,
        [NotNullWhen(true)]
        out string? value)
    {
        value = status switch
        {
            DaemonStopStatus.Stopped => Stopped,
            DaemonStopStatus.NotRunning => NotRunning,
            _ => null,
        };
        return value is not null;
    }
}
