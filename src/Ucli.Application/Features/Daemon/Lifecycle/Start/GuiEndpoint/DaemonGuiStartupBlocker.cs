using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Represents one terminal GUI startup blocker observed before GUI daemon session registration. </summary>
/// <param name="StartupBlockingReason"> The normalized startup-blocking reason. </param>
/// <param name="Reason"> The normalized daemon diagnosis reason. </param>
/// <param name="RetryDisposition"> The normalized retry disposition. </param>
/// <param name="Message"> The human-readable blocker message. </param>
/// <param name="StartupPhase"> The normalized startup phase. </param>
/// <param name="ActionRequired"> The normalized action required to resolve the blocker. </param>
/// <param name="ProcessId"> The Unity Editor process identifier. </param>
/// <param name="ProcessStartedAtUtc"> The Unity Editor process start timestamp. </param>
/// <param name="UnityLogPath"> The Unity log path observed for the startup attempt. </param>
/// <param name="PrimaryDiagnostic"> The primary machine-readable diagnostic when available. </param>
internal sealed record DaemonGuiStartupBlocker (
    string StartupBlockingReason,
    string Reason,
    string RetryDisposition,
    string Message,
    string StartupPhase,
    string ActionRequired,
    int ProcessId,
    DateTimeOffset ProcessStartedAtUtc,
    string UnityLogPath,
    DaemonPrimaryDiagnostic? PrimaryDiagnostic);
