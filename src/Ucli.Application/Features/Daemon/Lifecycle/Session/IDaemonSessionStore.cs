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
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Provides persistence access for daemon session metadata. </summary>
internal interface IDaemonSessionStore
{
    /// <summary> Reads daemon session metadata for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session read result. </returns>
    ValueTask<DaemonSessionReadResult> Read (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary> Writes daemon session metadata to local storage. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="session"> The daemon session metadata to persist. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session storage operation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> Write (
        string storageRoot,
        DaemonSession session,
        CancellationToken cancellationToken = default);

    /// <summary> Deletes daemon session metadata for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session storage operation result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> Delete (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default);
}
