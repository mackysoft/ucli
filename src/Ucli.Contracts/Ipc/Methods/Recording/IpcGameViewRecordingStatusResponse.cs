using System.Text.Json.Serialization;
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Represents the recording selection returned by <c>recording.status</c>.</summary>
public sealed record IpcGameViewRecordingStatusResponse
{
    [JsonConstructor]
    public IpcGameViewRecordingStatusResponse (IpcGameViewRecordingSelection recordingSelection)
    {
        RecordingSelection = recordingSelection ?? throw new ArgumentNullException(nameof(recordingSelection));
    }

    public IpcGameViewRecordingSelection RecordingSelection { get; }
}
