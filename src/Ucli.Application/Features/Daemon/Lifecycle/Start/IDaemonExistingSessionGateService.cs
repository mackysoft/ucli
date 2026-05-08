using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Evaluates whether daemon start can reuse an existing session snapshot. </summary>
internal interface IDaemonExistingSessionGateService
{
    /// <summary>
    /// Tries to complete daemon start from an existing session.
    /// Returns <see langword="null" /> when caller should continue with fresh launch flow.
    /// </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session snapshot. </param>
    /// <param name="timeout"> The timeout used for daemon ping and stale cleanup. </param>
    /// <param name="editorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// The resolved daemon start result when workflow should complete;
    /// otherwise <see langword="null" /> when fresh launch should continue.
    /// </returns>
    ValueTask<DaemonStartResult?> TryHandleExistingSession (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        CancellationToken cancellationToken = default);
}
