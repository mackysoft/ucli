using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.CommandContracts.Projection;

/// <summary> Converts IPC Play Mode snapshots into command payload output values. </summary>
internal static class PlayModeSnapshotOutputFactory
{
    /// <summary> Creates normalized output values from one IPC Play Mode snapshot. </summary>
    /// <param name="snapshot"> The IPC Play Mode snapshot, or <see langword="null" /> when unavailable. </param>
    /// <returns> The normalized output snapshot, or <see langword="null" /> when the snapshot is missing or malformed. </returns>
    public static PlayModeSnapshotOutput? Create (IpcPlayModeSnapshot? snapshot)
    {
        // NOTE: IpcPlayModeSnapshot is a wire contract and therefore stores literals.
        // Command logic parses them here so downstream decisions use typed values.
        if (snapshot is null
            || !IpcPlayModeStateCodec.TryParse(snapshot.State, out var state)
            || !IpcPlayModeTransitionCodec.TryParse(snapshot.Transition, out var transition))
        {
            return null;
        }

        return new PlayModeSnapshotOutput(
            State: IpcPlayModeStateCodec.ToValue(state),
            Transition: IpcPlayModeTransitionCodec.ToValue(transition),
            IsPlaying: snapshot.IsPlaying,
            IsPlayingOrWillChangePlaymode: snapshot.IsPlayingOrWillChangePlaymode,
            Generation: StringValueNormalizer.TrimToNull(snapshot.Generation));
    }
}
