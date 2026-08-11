using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Represents a <c>recording.start</c> request with its fixed effective definition.</summary>
public sealed record IpcGameViewRecordingStartRequest
{
    [JsonConstructor]
    public IpcGameViewRecordingStartRequest (
        Guid recordingId,
        Sha256Digest requestDigest,
        GameViewRecordingRequest request,
        IpcGameViewRecordingStartBinding startBinding,
        DateTimeOffset dispatchDeadlineUtc)
    {
        RecordingId = ContractArgumentGuard.RequireNonEmptyGuid(recordingId, nameof(recordingId));
        RequestDigest = requestDigest ?? throw new ArgumentNullException(nameof(requestDigest));
        Request = request ?? throw new ArgumentNullException(nameof(request));
        StartBinding = startBinding ?? throw new ArgumentNullException(nameof(startBinding));
        DispatchDeadlineUtc = ContractArgumentGuard.RequireUtcTimestamp(
            dispatchDeadlineUtc,
            nameof(dispatchDeadlineUtc));
    }

    public Guid RecordingId { get; }

    public Sha256Digest RequestDigest { get; }

    public GameViewRecordingRequest Request { get; }

    public IpcGameViewRecordingStartBinding StartBinding { get; }

    public DateTimeOffset DispatchDeadlineUtc { get; }
}
