using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Defines host-backed orchestration for project-scoped daemon control. </summary>
internal interface IDaemonProjectLifecycleGateway
{
    /// <summary> Ensures one daemon is running for the specified project. </summary>
    ValueTask<DaemonStartResult> EnsureRunning (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        CancellationToken cancellationToken = default);

    /// <summary> Attempts to stop one project daemon through a host-owned lifecycle gateway. </summary>
    ValueTask<DaemonStopResult?> TryStopProject (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
