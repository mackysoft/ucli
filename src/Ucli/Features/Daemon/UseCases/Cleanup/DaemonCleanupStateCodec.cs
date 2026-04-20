using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;

/// <summary> Converts daemon-cleanup status enum to command contract literals. </summary>
internal static class DaemonCleanupStateCodec
{
    /// <summary> Gets the cleanup-status value used when cleanup completed. </summary>
    public const string Completed = "completed";

    /// <summary> Gets the cleanup-status value used when cleanup was skipped. </summary>
    public const string Skipped = "skipped";

    /// <summary> Converts daemon-cleanup status enum to command contract literal. </summary>
    /// <param name="status"> The daemon-cleanup status enum value. </param>
    /// <param name="value"> The converted cleanup-status literal when conversion succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when conversion succeeds; otherwise <see langword="false" />. </returns>
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