using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Play Mode subsystem snapshot. </summary>
/// <param name="State"> The Play Mode state. </param>
/// <param name="Transition"> The current Play Mode transition. </param>
/// <param name="IsPlaying"> The value observed from Unity <c>EditorApplication.isPlaying</c>. </param>
/// <param name="IsPlayingOrWillChangePlaymode"> The value observed from Unity <c>EditorApplication.isPlayingOrWillChangePlaymode</c>. </param>
public sealed record IpcPlayModeSnapshot
{
    /// <summary> Initializes a Play Mode subsystem snapshot. </summary>
    [JsonConstructor]
    public IpcPlayModeSnapshot (
        IpcPlayModeState State,
        IpcPlayModeTransition Transition,
        bool IsPlaying,
        bool IsPlayingOrWillChangePlaymode)
    {
        if (!TextVocabulary.IsDefined(State))
        {
            throw new ArgumentOutOfRangeException(nameof(State), State, "Unsupported Play Mode state.");
        }

        if (!TextVocabulary.IsDefined(Transition))
        {
            throw new ArgumentOutOfRangeException(nameof(Transition), Transition, "Unsupported Play Mode transition.");
        }

        this.State = State;
        this.Transition = Transition;
        this.IsPlaying = IsPlaying;
        this.IsPlayingOrWillChangePlaymode = IsPlayingOrWillChangePlaymode;
    }

    /// <summary> Gets the Play Mode state. </summary>
    [JsonInclude]
    [JsonRequired]
    public IpcPlayModeState State { get; private init; }

    /// <summary> Gets the current Play Mode transition. </summary>
    [JsonInclude]
    [JsonRequired]
    public IpcPlayModeTransition Transition { get; private init; }

    /// <summary> Gets the observed Unity playing flag. </summary>
    [JsonInclude]
    [JsonRequired]
    public bool IsPlaying { get; private init; }

    /// <summary> Gets the observed Unity Play Mode transition flag. </summary>
    [JsonInclude]
    [JsonRequired]
    public bool IsPlayingOrWillChangePlaymode { get; private init; }
}
