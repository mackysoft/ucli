using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Converts daemon-cleanup skip-reason enum to command contract literals. </summary>
internal static class DaemonCleanupSkipReasonCodec
{
    /// <summary> Gets the skip-reason literal used when daemon is running. </summary>
    public const string Running = "running";

    /// <summary> Gets the skip-reason literal used when invalid session may still belong to a live daemon. </summary>
    public const string UnsafeInvalidSession = "unsafeInvalidSession";

    /// <summary> Gets the skip-reason literal used when daemon reachability is uncertain. </summary>
    public const string UncertainReachability = "uncertainReachability";

    /// <summary> Converts daemon-cleanup skip-reason enum to command contract literal. </summary>
    /// <param name="skipReason"> The daemon-cleanup skip-reason enum value. </param>
    /// <param name="value"> The converted skip-reason literal when conversion succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when conversion succeeds; otherwise <see langword="false" />. </returns>
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