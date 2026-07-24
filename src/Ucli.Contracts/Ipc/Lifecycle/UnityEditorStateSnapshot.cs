using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the comparable Unity Editor state captured at one observation boundary. </summary>
/// <param name="EditorMode"> The Editor hosting mode. </param>
/// <param name="LifecycleState"> The Editor lifecycle state. </param>
/// <param name="CompileState"> The script-compilation state. </param>
/// <param name="Generations"> The lifecycle generation snapshot. </param>
/// <param name="PlayMode"> The Play Mode subsystem snapshot. </param>
public sealed record UnityEditorStateSnapshot
{
    /// <summary> Initializes a comparable Unity Editor state snapshot. </summary>
    [JsonConstructor]
    public UnityEditorStateSnapshot (
        DaemonEditorMode editorMode,
        IpcEditorLifecycleState lifecycleState,
        IpcCompileState compileState,
        IpcUnityGenerationSnapshot generations,
        IpcPlayModeSnapshot playMode)
    {
        if (!TextVocabulary.IsDefined(editorMode))
        {
            throw new ArgumentOutOfRangeException(nameof(editorMode), editorMode, "Unsupported Editor mode.");
        }

        if (!TextVocabulary.IsDefined(lifecycleState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifecycleState),
                lifecycleState,
                "Unsupported lifecycle state.");
        }

        if (!TextVocabulary.IsDefined(compileState))
        {
            throw new ArgumentOutOfRangeException(nameof(compileState), compileState, "Unsupported compile state.");
        }

        EditorMode = editorMode;
        LifecycleState = lifecycleState;
        CompileState = compileState;
        Generations = generations ?? throw new ArgumentNullException(nameof(generations));
        PlayMode = playMode ?? throw new ArgumentNullException(nameof(playMode));
    }

    /// <summary> Gets the Editor hosting mode. </summary>
    [JsonInclude]
    [JsonRequired]
    public DaemonEditorMode EditorMode { get; private init; }

    /// <summary> Gets the Editor lifecycle state. </summary>
    [JsonInclude]
    [JsonRequired]
    public IpcEditorLifecycleState LifecycleState { get; private init; }

    /// <summary> Gets the script-compilation state. </summary>
    [JsonInclude]
    [JsonRequired]
    public IpcCompileState CompileState { get; private init; }

    /// <summary> Gets the lifecycle generation snapshot. </summary>
    [JsonInclude]
    [JsonRequired]
    public IpcUnityGenerationSnapshot Generations { get; private init; }

    /// <summary> Gets the Play Mode subsystem snapshot. </summary>
    [JsonInclude]
    [JsonRequired]
    public IpcPlayModeSnapshot PlayMode { get; private init; }
}
