using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

namespace MackySoft.Ucli.Features.Daemon.Common.CommandContracts;

/// <summary> Converts daemon-start status enum to daemon command contract literals. </summary>
internal static class DaemonStartStateCodec
{
    public const string Started = "started";

    public const string AlreadyRunning = "alreadyRunning";

    public const string Attached = "attached";

    public const string Failed = "failed";

    public static bool TryToValue (
        DaemonStartStatus status,
        [NotNullWhen(true)]
        out string? value)
    {
        value = status switch
        {
            DaemonStartStatus.Started => Started,
            DaemonStartStatus.AlreadyRunning => AlreadyRunning,
            DaemonStartStatus.Attached => Attached,
            DaemonStartStatus.Failed => Failed,
            _ => null,
        };
        return value is not null;
    }
}
