using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class IpcUnityEditorObservationTestFactory
{
    public static IpcUnityEditorObservation Create (
        IpcEditorLifecycleState lifecycleState = IpcEditorLifecycleState.Ready,
        DaemonEditorMode editorMode = DaemonEditorMode.Batchmode,
        string serverVersion = "0.0.1",
        string unityVersion = "6000.1.4f1",
        ProjectFingerprint? projectFingerprint = null,
        IpcCompileState? compileState = null,
        IpcUnityGenerationSnapshot? generations = null,
        IpcPlayModeSnapshot? playMode = null,
        DateTimeOffset? observedAtUtc = null)
    {
        return new IpcUnityEditorObservation(
            serverVersion: serverVersion,
            unityVersion: unityVersion,
            projectFingerprint: projectFingerprint ?? ProjectFingerprintTestFactory.Create("ipc-unity-editor-observation"),
            state: new UnityEditorStateSnapshot(
                editorMode: editorMode,
                lifecycleState: lifecycleState,
                compileState: compileState ?? ResolveCompileState(lifecycleState),
                generations: generations ?? new IpcUnityGenerationSnapshot(0, 0, 0, 0),
                playMode: playMode ?? new IpcPlayModeSnapshot(
                    IpcPlayModeState.Stopped,
                    IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: observedAtUtc ?? DateTimeOffset.UnixEpoch);
    }

    private static IpcCompileState ResolveCompileState (IpcEditorLifecycleState lifecycleState)
    {
        return lifecycleState switch
        {
            IpcEditorLifecycleState.Compiling => IpcCompileState.Compiling,
            IpcEditorLifecycleState.CompileFailed => IpcCompileState.Failed,
            _ => IpcCompileState.Ready,
        };
    }
}
