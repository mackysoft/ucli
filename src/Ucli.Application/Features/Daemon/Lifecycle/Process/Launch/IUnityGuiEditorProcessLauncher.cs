using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Launch;

/// <summary> Launches Unity GUI Editor processes configured to register uCLI daemon sessions. </summary>
internal interface IUnityGuiEditorProcessLauncher
{
    /// <summary> Launches one Unity GUI Editor process for the specified project context. </summary>
    ValueTask<UnityDaemonLaunchResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        AbsolutePath unityLogPath,
        CancellationToken cancellationToken = default);
}
