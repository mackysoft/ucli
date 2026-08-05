using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Fixes the Unity process, recording runtime, and Editor generation admitted for one recording start.</summary>
public sealed record IpcGameViewRecordingStartBinding
{
    [JsonConstructor]
    public IpcGameViewRecordingStartBinding (
        ProcessIdentity process,
        GameViewRecordingRuntimeIdentity runtime,
        UnityEditorGenerationSnapshot generation)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
    }

    public ProcessIdentity Process { get; }

    public GameViewRecordingRuntimeIdentity Runtime { get; }

    public UnityEditorGenerationSnapshot Generation { get; }
}
