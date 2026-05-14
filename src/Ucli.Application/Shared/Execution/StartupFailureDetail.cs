using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Represents structured startup failure details projected by commands that start a Unity process. </summary>
/// <param name="Startup"> The startup observation associated with the failed process startup. </param>
/// <param name="Diagnosis"> The structured diagnosis associated with the failed process startup when available. </param>
/// <param name="RetryDisposition"> The normalized retry disposition. </param>
/// <param name="SafeToRetryImmediately"> Whether the failed command can be retried immediately without external changes. </param>
internal sealed record StartupFailureDetail (
    DaemonStartupObservationOutput? Startup,
    DaemonDiagnosisOutput? Diagnosis,
    string RetryDisposition,
    bool SafeToRetryImmediately);
