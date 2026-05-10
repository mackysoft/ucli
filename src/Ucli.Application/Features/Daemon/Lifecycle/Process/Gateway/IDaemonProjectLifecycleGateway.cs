using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Gateway;

/// <summary> Defines host-backed orchestration for project-scoped daemon control. </summary>
internal interface IDaemonProjectLifecycleGateway
{
    /// <summary> Ensures one daemon is running for the specified project. </summary>
    ValueTask<DaemonStartResult> EnsureRunningAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        CancellationToken cancellationToken = default);

    /// <summary> Attempts to stop one project daemon through a host-owned lifecycle gateway. </summary>
    ValueTask<DaemonStopResult?> TryStopProjectAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
