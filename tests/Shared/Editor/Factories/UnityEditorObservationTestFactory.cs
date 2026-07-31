using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.TestSupport;

internal static class UnityEditorObservationTestFactory
{
    public static UnityEditorObservation Create (
        UnityEditorLifecycleState lifecycleState = UnityEditorLifecycleState.Ready,
        UnityEditorMode editorMode = UnityEditorMode.Batchmode,
        string serverVersion = "0.0.1",
        string unityVersion = "6000.1.4f1",
        ProjectFingerprint? projectFingerprint = null,
        UnityEditorCompileState? compileState = null,
        UnityEditorGenerationSnapshot? generations = null,
        UnityEditorPlayModeSnapshot? playMode = null,
        DateTimeOffset? observedAtUtc = null)
    {
        return new UnityEditorObservation(
            serverVersion: serverVersion,
            unityVersion: unityVersion,
            projectFingerprint: projectFingerprint ?? ProjectFingerprintTestFactory.Create("ipc-unity-editor-observation"),
            state: new UnityEditorStateSnapshot(
                editorMode: editorMode,
                lifecycleState: lifecycleState,
                compileState: compileState ?? ResolveCompileState(lifecycleState),
                generations: generations ?? new UnityEditorGenerationSnapshot(0, 0, 0, 0),
                playMode: playMode ?? new UnityEditorPlayModeSnapshot(
                    UnityEditorPlayModeState.Stopped,
                    UnityEditorPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: observedAtUtc ?? DateTimeOffset.UnixEpoch,
            actionRequired: null,
            primaryDiagnostic: null);
    }

    private static UnityEditorCompileState ResolveCompileState (UnityEditorLifecycleState lifecycleState)
    {
        return lifecycleState switch
        {
            UnityEditorLifecycleState.Compiling => UnityEditorCompileState.Compiling,
            UnityEditorLifecycleState.CompileFailed => UnityEditorCompileState.Failed,
            _ => UnityEditorCompileState.Ready,
        };
    }
}
