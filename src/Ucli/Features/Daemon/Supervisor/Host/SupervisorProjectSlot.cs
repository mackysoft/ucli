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
namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Stores per-project synchronization and process-tracking state for the supervisor runtime. </summary>
internal sealed class SupervisorProjectSlot
{
    /// <summary> Initializes a new instance of the <see cref="SupervisorProjectSlot" /> class. </summary>
    public SupervisorProjectSlot ()
    {
        Gate = new SemaphoreSlim(1, 1);
    }

    /// <summary> Gets the exclusive gate for lifecycle operations targeting one project fingerprint. </summary>
    public SemaphoreSlim Gate { get; }

    /// <summary> Gets or sets the currently tracked managed process for this project fingerprint. </summary>
    public SupervisorManagedDaemonProcess? ManagedProcess { get; set; }

    /// <summary> Gets or sets one pending background lifecycle task for this project fingerprint. </summary>
    public Task? PendingOperation { get; set; }
}
