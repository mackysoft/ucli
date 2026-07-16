using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.ExistingSession;

/// <summary> Evaluates whether daemon start can reuse an existing session snapshot. </summary>
internal interface IDaemonExistingSessionGateService
{
    /// <summary>
    /// Tries to complete daemon start from an existing session.
    /// Returns <see langword="null" /> when caller should continue with the remaining start flow.
    /// </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session snapshot. </param>
    /// <param name="deadline"> The deadline shared by the daemon-start workflow. </param>
    /// <param name="editorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="progressObserver"> The optional observer for supervisor-internal start progress. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// The resolved daemon start result when workflow should complete;
    /// otherwise <see langword="null" /> when the remaining start flow should continue.
    /// </returns>
    ValueTask<DaemonStartResult?> TryHandleExistingSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        DaemonEditorMode? editorMode,
        IDaemonStartProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default);
}
