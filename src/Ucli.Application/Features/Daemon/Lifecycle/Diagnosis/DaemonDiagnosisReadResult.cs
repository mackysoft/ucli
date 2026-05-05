using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

/// <summary> Represents one daemon diagnosis read result. </summary>
/// <param name="Diagnosis"> The loaded daemon diagnosis when available. </param>
/// <param name="Error"> The structured error when read fails. </param>
internal sealed record DaemonDiagnosisReadResult (
    DaemonDiagnosis? Diagnosis,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether read succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Gets a value indicating whether diagnosis exists. </summary>
    public bool Exists => Diagnosis is not null;

    /// <summary> Creates one successful read result. </summary>
    /// <param name="diagnosis"> The loaded daemon diagnosis when available; otherwise <see langword="null" />. </param>
    /// <returns> The successful read result. </returns>
    public static DaemonDiagnosisReadResult Success (DaemonDiagnosis? diagnosis)
    {
        return new DaemonDiagnosisReadResult(diagnosis, null);
    }

    /// <summary> Creates one failed read result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed read result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonDiagnosisReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonDiagnosisReadResult(null, error);
    }
}
