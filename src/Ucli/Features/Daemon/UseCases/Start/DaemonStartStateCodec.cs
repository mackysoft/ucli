using System.Diagnostics.CodeAnalysis;

namespace MackySoft.Ucli.Features.Daemon.UseCases.Start;

/// <summary> Converts daemon-start status enum to daemon command contract literals. </summary>
internal static class DaemonStartStateCodec
{
    /// <summary> Gets the start-status value used when daemon process was started. </summary>
    public const string Started = "started";

    /// <summary> Gets the start-status value used when daemon is already running. </summary>
    public const string AlreadyRunning = "alreadyRunning";

    /// <summary> Converts daemon-start status enum to command contract literal. </summary>
    /// <param name="status"> The daemon-start status enum value. </param>
    /// <param name="value"> The converted start-status literal when conversion succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when conversion succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryToValue (
        DaemonStartStatus status,
        [NotNullWhen(true)]
        out string? value)
    {
        value = status switch
        {
            DaemonStartStatus.Started => Started,
            DaemonStartStatus.AlreadyRunning => AlreadyRunning,
            _ => null,
        };
        return value is not null;
    }
}