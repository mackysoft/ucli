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

/// <summary> Represents the result of ensuring one worktree-local supervisor is ready. </summary>
internal sealed record SupervisorBootstrapResult (
    SupervisorInstanceManifest? Manifest,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether bootstrap completed successfully. </summary>
    public bool IsSuccess => Manifest is not null && Error is null;

    /// <summary> Creates one successful bootstrap result. </summary>
    /// <param name="manifest"> The ready supervisor manifest. </param>
    /// <returns> The successful bootstrap result. </returns>
    public static SupervisorBootstrapResult Success (SupervisorInstanceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new SupervisorBootstrapResult(manifest, null);
    }

    /// <summary> Creates one failed bootstrap result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed bootstrap result. </returns>
    public static SupervisorBootstrapResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new SupervisorBootstrapResult(null, error);
    }
}