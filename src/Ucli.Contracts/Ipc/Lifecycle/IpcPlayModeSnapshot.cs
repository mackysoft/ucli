namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Play Mode subsystem snapshot. </summary>
/// <param name="State"> The Play Mode state literal. </param>
/// <param name="Transition"> The current Play Mode transition literal. </param>
/// <param name="IsPlaying"> The value observed from Unity <c>EditorApplication.isPlaying</c>. </param>
/// <param name="IsPlayingOrWillChangePlaymode"> The value observed from Unity <c>EditorApplication.isPlayingOrWillChangePlaymode</c>. </param>
/// <param name="Generation"> The opaque generation value that changes after completed Play Mode enter or exit transitions. </param>
public sealed record IpcPlayModeSnapshot (
    string State,
    string Transition,
    bool IsPlaying,
    bool IsPlayingOrWillChangePlaymode,
    string? Generation);
