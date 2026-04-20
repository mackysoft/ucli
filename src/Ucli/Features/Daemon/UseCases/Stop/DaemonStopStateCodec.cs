using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Ucli.Features.Daemon.UseCases.Stop;

/// <summary> Converts daemon-stop status enum to daemon command contract literals. </summary>
internal static class DaemonStopStateCodec
{
    /// <summary> Gets the stop-status value used when daemon was stopped. </summary>
    public const string Stopped = "stopped";

    /// <summary> Gets the stop-status value used when daemon session is not running. </summary>
    public const string NotRunning = "notRunning";

    /// <summary> Converts daemon-stop status enum to command contract literal. </summary>
    /// <param name="status"> The daemon-stop status enum value. </param>
    /// <param name="value"> The converted stop-status literal when conversion succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when conversion succeeds; otherwise <see langword="false" />. </returns>
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