using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;

/// <summary> Represents the outcome of supervisor stability verification for one daemon session. </summary>
internal sealed record SupervisorStabilityVerificationResult (
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether stability verification succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates one successful stability-verification result. </summary>
    /// <returns> The successful result. </returns>
    public static SupervisorStabilityVerificationResult Success ()
    {
        return new SupervisorStabilityVerificationResult((ExecutionError?)null);
    }

    /// <summary> Creates one failed stability-verification result. </summary>
    /// <param name="error"> The structured verification error. </param>
    /// <returns> The failed result. </returns>
    public static SupervisorStabilityVerificationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new SupervisorStabilityVerificationResult(error);
    }
}