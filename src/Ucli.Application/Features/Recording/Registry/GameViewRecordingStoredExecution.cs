using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.Registry;

/// <summary>Contains the durable host-owned facts required to continue one recording across CLI processes.</summary>
internal sealed record GameViewRecordingStoredExecution
{
    public const int CurrentSchemaVersion = 1;

    public GameViewRecordingStoredExecution (
        int schemaVersion,
        Guid recordingId,
        GameViewRecordingRequest request,
        string canonicalRequestJson,
        Sha256Digest requestDigest,
        PathArtifactRef requestRef,
        GameViewRecordingCapability startCapability,
        IpcGameViewRecordingStartBinding startBinding,
        DateTimeOffset startDispatchDeadlineUtc,
        IpcGameViewRecordingSnapshot? runtimeSnapshot,
        GameViewRecordingExecutionPayload payload)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Recording registry schema version must be one.");
        }

        SchemaVersion = schemaVersion;
        RecordingId = recordingId == Guid.Empty
            ? throw new ArgumentException("Recording id must not be empty.", nameof(recordingId))
            : recordingId;
        Request = request ?? throw new ArgumentNullException(nameof(request));
        CanonicalRequestJson = string.IsNullOrWhiteSpace(canonicalRequestJson)
            ? throw new ArgumentException("Canonical request JSON is required.", nameof(canonicalRequestJson))
            : canonicalRequestJson;
        RequestDigest = requestDigest ?? throw new ArgumentNullException(nameof(requestDigest));
        RequestRef = requestRef ?? throw new ArgumentNullException(nameof(requestRef));
        StartCapability = startCapability ?? throw new ArgumentNullException(nameof(startCapability));
        if (startCapability.RuntimeAdmission.State != GameViewRecordingRuntimeAdmissionState.Ready)
        {
            throw new ArgumentException("A stored recording must originate from a ready start admission.", nameof(startCapability));
        }

        StartLimits = startCapability.Limits
            ?? throw new ArgumentException("A stored recording start admission must carry numeric limits.", nameof(startCapability));
        StartBinding = startBinding ?? throw new ArgumentNullException(nameof(startBinding));
        if (startDispatchDeadlineUtc == default
            || startDispatchDeadlineUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Recording start dispatch deadline must be a non-default UTC timestamp.",
                nameof(startDispatchDeadlineUtc));
        }

        StartDispatchDeadlineUtc = startDispatchDeadlineUtc;
        RuntimeSnapshot = runtimeSnapshot;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));

        if (payload.ExecutionReference.Id != recordingId
            || payload.RequestDigest != requestDigest
            || payload.RequestRef != requestRef
            || (runtimeSnapshot is not null
                && (runtimeSnapshot.RecordingId != recordingId
                    || runtimeSnapshot.RequestDigest != requestDigest
                    || runtimeSnapshot.Runtime != startBinding.Runtime
                    || runtimeSnapshot.StartGeneration != startBinding.Generation)))
        {
            throw new ArgumentException("Stored recording identity, effective request, and public payload must agree.");
        }
    }

    public int SchemaVersion { get; }

    public Guid RecordingId { get; }

    public GameViewRecordingRequest Request { get; }

    public string CanonicalRequestJson { get; }

    public Sha256Digest RequestDigest { get; }

    public PathArtifactRef RequestRef { get; }

    public GameViewRecordingCapability StartCapability { get; }

    [JsonIgnore]
    public GameViewRecordingLimits StartLimits { get; }

    public IpcGameViewRecordingStartBinding StartBinding { get; }

    public DateTimeOffset StartDispatchDeadlineUtc { get; }

    public IpcGameViewRecordingSnapshot? RuntimeSnapshot { get; }

    public GameViewRecordingExecutionPayload Payload { get; }
}
