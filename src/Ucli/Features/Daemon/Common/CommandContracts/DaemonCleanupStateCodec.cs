using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

namespace MackySoft.Ucli.Features.Daemon.Common.CommandContracts;

/// <summary> Converts daemon-cleanup status enum to command contract literals. </summary>
internal static class DaemonCleanupStateCodec
{
    public const string Completed = "completed";

    public const string Skipped = "skipped";

    public static bool TryToValue (
        DaemonCleanupStatus status,
        [NotNullWhen(true)]
        out string? value)
    {
        value = status switch
        {
            DaemonCleanupStatus.Completed => Completed,
            DaemonCleanupStatus.Skipped => Skipped,
            _ => null,
        };
        return value is not null;
    }
}
