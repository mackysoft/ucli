using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Launch;

/// <summary> Persists launch-session snapshots required by daemon startup workflow. </summary>
internal interface IDaemonLaunchSessionService
{
    /// <summary> Creates and persists an initial daemon session before process launch. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="editorMode"> The requested daemon Editor mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The launch-session persistence result. </returns>
    ValueTask<DaemonLaunchSessionWriteResult> InitializeAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonEditorMode editorMode,
        CancellationToken cancellationToken = default);

    /// <summary> Persists launched daemon process identity to an existing session snapshot. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session snapshot. </param>
    /// <param name="processId"> The launched process identifier when available. </param>
    /// <param name="processStartedAtUtc"> The launched process start timestamp when available. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The launch-session persistence result. </returns>
    ValueTask<DaemonLaunchSessionWriteResult> UpdateProcessIdAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        CancellationToken cancellationToken = default);
}
