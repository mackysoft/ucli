using System.Text.Json.Serialization;
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Represents the durable execution returned after <c>recording.start</c>.</summary>
public sealed record IpcGameViewRecordingStartResponse
{
    [JsonConstructor]
    public IpcGameViewRecordingStartResponse (IpcGameViewRecordingSnapshot recording)
    {
        Recording = recording ?? throw new ArgumentNullException(nameof(recording));
    }

    public IpcGameViewRecordingSnapshot Recording { get; }
}
