using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Represents one terminal GUI startup blocker observation made before daemon session registration. </summary>
internal sealed record DaemonGuiStartupBlockerObservation
{
    /// <summary> Initializes one terminal GUI startup blocker observation. </summary>
    /// <param name="classification"> The normalized startup-failure classification. </param>
    /// <param name="processId"> The Unity Editor process identifier. </param>
    /// <param name="processStartedAtUtc"> The Unity Editor process start timestamp. </param>
    /// <param name="unityLogPath"> The Unity log path observed for the startup attempt. </param>
    public DaemonGuiStartupBlockerObservation (
        DaemonStartupFailureClassification classification,
        int processId,
        DateTimeOffset processStartedAtUtc,
        string unityLogPath)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process identifier must be positive.");
        }

        if (processStartedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processStartedAtUtc),
                processStartedAtUtc,
                "Process start timestamp must be specified.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(unityLogPath);

        Classification = classification ?? throw new ArgumentNullException(nameof(classification));
        ProcessId = processId;
        ProcessStartedAtUtc = processStartedAtUtc;
        UnityLogPath = unityLogPath;
    }

    /// <summary> Gets the normalized startup-failure classification. </summary>
    public DaemonStartupFailureClassification Classification { get; }

    /// <summary> Gets the Unity Editor process identifier. </summary>
    public int ProcessId { get; }

    /// <summary> Gets the Unity Editor process start timestamp. </summary>
    public DateTimeOffset ProcessStartedAtUtc { get; }

    /// <summary> Gets the Unity log path observed for the startup attempt. </summary>
    public string UnityLogPath { get; }
}
