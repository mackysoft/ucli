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
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

/// <summary> Represents the result of daemon stop operation. </summary>
/// <param name="Status"> The daemon stop outcome. </param>
/// <param name="Error"> The structured error when stop fails; otherwise <see langword="null" />. </param>
internal sealed record DaemonStopResult (
    DaemonStopStatus Status,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon stop operation succeeded. </summary>
    public bool IsSuccess => (Status == DaemonStopStatus.Stopped || Status == DaemonStopStatus.NotRunning) && Error is null;

    /// <summary> Creates a successful stopped result. </summary>
    /// <returns> The successful stopped result. </returns>
    public static DaemonStopResult Stopped ()
    {
        return new DaemonStopResult(DaemonStopStatus.Stopped, null);
    }

    /// <summary> Creates a not-running result. </summary>
    /// <returns> The not-running result. </returns>
    public static DaemonStopResult NotRunning ()
    {
        return new DaemonStopResult(DaemonStopStatus.NotRunning, null);
    }

    /// <summary> Creates a failed stop result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed stop result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStopResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStopResult(DaemonStopStatus.Failed, error);
    }
}
