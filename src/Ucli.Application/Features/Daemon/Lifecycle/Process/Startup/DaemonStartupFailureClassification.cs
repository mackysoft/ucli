using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;

/// <summary> Represents one classified Unity startup blocker found before daemon bootstrap completed. </summary>
/// <param name="Reason"> The normalized daemon diagnosis reason. </param>
/// <param name="Message"> The human-readable failure message. </param>
/// <param name="StartupPhase"> The normalized startup phase. </param>
/// <param name="ActionRequired"> The normalized action required to resolve the blocker. </param>
/// <param name="PrimaryDiagnostic"> The primary machine-readable diagnostic when available. </param>
internal sealed record DaemonStartupFailureClassification (
    string Reason,
    string Message,
    string StartupPhase,
    string ActionRequired,
    DaemonPrimaryDiagnostic? PrimaryDiagnostic);
