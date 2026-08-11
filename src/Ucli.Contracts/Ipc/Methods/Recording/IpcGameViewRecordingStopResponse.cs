using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Represents the recovery or terminal execution returned by <c>recording.stop</c>.</summary>
public sealed record IpcGameViewRecordingStopResponse
{
    [JsonConstructor]
    public IpcGameViewRecordingStopResponse (IpcGameViewRecordingStopSnapshot recording)
    {
        Recording = recording ?? throw new ArgumentNullException(nameof(recording));
    }

    public IpcGameViewRecordingStopSnapshot Recording { get; }
}
