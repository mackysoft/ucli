using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Represents one typed Play Mode subsystem observation inside the Unity runtime. </summary>
    internal sealed record UnityEditorPlayModeSnapshot (
        IpcPlayModeState State,
        IpcPlayModeTransition Transition,
        bool IsPlaying,
        bool IsPlayingOrWillChangePlaymode,
        int Generation);
}
