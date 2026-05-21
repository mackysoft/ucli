namespace MackySoft.Ucli.Application.Shared.CommandContracts;

/// <summary> Represents normalized Play Mode subsystem snapshot values emitted by command payloads. </summary>
/// <param name="State"> The Play Mode state literal. </param>
/// <param name="Transition"> The current Play Mode transition literal. </param>
/// <param name="IsPlaying"> The observed Unity playing flag. </param>
/// <param name="IsPlayingOrWillChangePlaymode"> The observed Unity Play Mode transition flag. </param>
/// <param name="Generation"> The opaque generation value updated after completed Play Mode enter or exit transitions. </param>
internal sealed record PlayModeSnapshotOutput (
    string State,
    string Transition,
    bool IsPlaying,
    bool IsPlayingOrWillChangePlaymode,
    string? Generation);
