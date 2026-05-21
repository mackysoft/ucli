using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary> Converts IPC Play Mode snapshots into command payload output values. </summary>
internal static class PlayModeSnapshotOutputFactory
{
    /// <summary> Creates normalized output values from one IPC Play Mode snapshot. </summary>
    /// <param name="snapshot"> The IPC Play Mode snapshot, or <see langword="null" /> when unavailable. </param>
    /// <returns> The normalized output snapshot, or <see langword="null" /> when the snapshot is missing or malformed. </returns>
    public static PlayModeSnapshotOutput? Create (IpcPlayModeSnapshot? snapshot)
    {
        if (snapshot is null
            || !TryNormalizeState(snapshot.State, out var state)
            || !TryNormalizeTransition(snapshot.Transition, out var transition))
        {
            return null;
        }

        return new PlayModeSnapshotOutput(
            State: state!,
            Transition: transition!,
            IsPlaying: snapshot.IsPlaying,
            IsPlayingOrWillChangePlaymode: snapshot.IsPlayingOrWillChangePlaymode,
            Generation: StringValueNormalizer.TrimToNull(snapshot.Generation));
    }

    private static bool TryNormalizeState (
        string? value,
        out string? normalized)
    {
        normalized = StringValueNormalizer.TrimToNull(value);
        return normalized is IpcPlayModeStateNames.Stopped
            or IpcPlayModeStateNames.Entering
            or IpcPlayModeStateNames.Playing
            or IpcPlayModeStateNames.Exiting
            or IpcPlayModeStateNames.Unknown;
    }

    private static bool TryNormalizeTransition (
        string? value,
        out string? normalized)
    {
        normalized = StringValueNormalizer.TrimToNull(value);
        return normalized is IpcPlayModeTransitionNames.None
            or IpcPlayModeTransitionNames.Entering
            or IpcPlayModeTransitionNames.Exiting;
    }
}
