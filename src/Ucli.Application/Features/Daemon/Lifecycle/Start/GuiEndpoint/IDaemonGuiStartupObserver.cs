using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Observes one CLI-launched GUI Unity Editor until daemon session registration succeeds or startup reaches a terminal blocker. </summary>
internal interface IDaemonGuiStartupObserver
{
    /// <summary> Waits for GUI daemon session registration while periodically classifying Unity startup blockers. </summary>
    ValueTask<DaemonGuiStartupObservationResult> WaitForStartupAsync (
        ResolvedUnityProjectContext unityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        AbsolutePath unityLogPath,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);
}
