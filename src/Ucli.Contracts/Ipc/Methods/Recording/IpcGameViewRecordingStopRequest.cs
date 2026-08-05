using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Represents an idempotent <c>recording.stop</c> request.</summary>
public sealed record IpcGameViewRecordingStopRequest
{
    [JsonConstructor]
    public IpcGameViewRecordingStopRequest (
        Guid recordingId,
        Sha256Digest requestDigest,
        int effectiveMaxDurationSeconds,
        IpcGameViewRecordingStartBinding startBinding,
        DateTimeOffset dispatchDeadlineUtc,
        IpcGameViewRecordingSnapshot? knownRecording)
    {
        RecordingId = ContractArgumentGuard.RequireNonEmptyGuid(recordingId, nameof(recordingId));
        RequestDigest = requestDigest ?? throw new ArgumentNullException(nameof(requestDigest));
        EffectiveMaxDurationSeconds = ContractArgumentGuard.RequirePositive(
            effectiveMaxDurationSeconds,
            nameof(effectiveMaxDurationSeconds));
        StartBinding = startBinding ?? throw new ArgumentNullException(nameof(startBinding));
        DispatchDeadlineUtc = ContractArgumentGuard.RequireUtcTimestamp(
            dispatchDeadlineUtc,
            nameof(dispatchDeadlineUtc));
        KnownRecording = IpcGameViewRecordingRequestContractGuard.RequireKnownRecording(
            knownRecording,
            RecordingId,
            RequestDigest,
            EffectiveMaxDurationSeconds,
            StartBinding,
            nameof(knownRecording));
    }

    public Guid RecordingId { get; }

    public Sha256Digest RequestDigest { get; }

    public int EffectiveMaxDurationSeconds { get; }

    public IpcGameViewRecordingStartBinding StartBinding { get; }

    public DateTimeOffset DispatchDeadlineUtc { get; }

    /// <summary>Gets the application's latest durable non-terminal runtime observation, when available.</summary>
    public IpcGameViewRecordingSnapshot? KnownRecording { get; }
}
