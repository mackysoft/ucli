using System.Text.Json;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

/// <summary> Receives supervisor IPC progress frames for caller-visible daemon-start projection. </summary>
internal interface IDaemonStartSupervisorProgressObserver
{
    /// <summary> Emits one supervisor progress frame as a caller-visible daemon-start progress entry. </summary>
    ValueTask EmitSupervisorProgressAsync (
        string eventName,
        JsonElement payload,
        CancellationToken cancellationToken);
}
