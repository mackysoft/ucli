using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Launch;

/// <summary> Launches daemon process and applies startup-compensation workflow when start fails. </summary>
internal interface IDaemonLaunchService
{
    /// <summary> Launches daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The deadline shared by the daemon-start workflow. </param>
    /// <param name="editorMode"> The requested daemon Editor mode. </param>
    /// <param name="onStartupBlocked"> The startup-blocked process policy requested by the caller. </param>
    /// <param name="progressObserver"> The optional observer for supervisor-internal start progress. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon start result. </returns>
    ValueTask<DaemonStartResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonEditorMode editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonStartProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default);
}
