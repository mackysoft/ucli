using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

namespace MackySoft.Ucli.Features.Daemon.Common.CommandContracts;

/// <summary> Converts daemon-cleanup skip-reason enum to command contract literals. </summary>
internal static class DaemonCleanupSkipReasonCodec
{
    public const string Running = "running";

    public const string UnsafeInvalidSession = "unsafeInvalidSession";

    public const string UncertainReachability = "uncertainReachability";

    public static bool TryToValue (
        DaemonCleanupSkipReason skipReason,
        [NotNullWhen(true)]
        out string? value)
    {
        value = skipReason switch
        {
            DaemonCleanupSkipReason.None => null,
            DaemonCleanupSkipReason.Running => Running,
            DaemonCleanupSkipReason.UnsafeInvalidSession => UnsafeInvalidSession,
            DaemonCleanupSkipReason.UncertainReachability => UncertainReachability,
            _ => null,
        };
        return skipReason == DaemonCleanupSkipReason.None || value is not null;
    }
}
