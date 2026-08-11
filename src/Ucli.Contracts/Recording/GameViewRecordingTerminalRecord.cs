using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Recording;

/// <summary>Represents the immutable record used to reverify one terminal recording result.</summary>
public sealed record GameViewRecordingTerminalRecord
{
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public GameViewRecordingTerminalRecord (
        int schemaVersion,
        ExecutionKind executionKind,
        Guid recordingId,
        Sha256Digest requestDigest,
        UnityProjectIdentity project,
        GameViewRecordingRuntimeIdentity runtime,
        UnityEditorGenerationSnapshot startGeneration,
        UnityEditorGenerationSnapshot terminalGeneration,
        GameViewRecordingTerminalSummary terminalSummary,
        ArtifactRef requestRef,
        IReadOnlyList<ArtifactRef> artifactRefs,
        IReadOnlyList<GameViewRecordingDiagnostic> diagnostics)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Recording terminal-record schema version must be one.");
        }
        if (executionKind != GameViewRecordingExecutionContract.Kind)
        {
            throw new ArgumentException("Terminal record execution kind must identify GameView recording.", nameof(executionKind));
        }

        SchemaVersion = schemaVersion;
        ExecutionKind = executionKind;
        RecordingId = ContractArgumentGuard.RequireNonEmptyGuid(recordingId, nameof(recordingId));
        RequestDigest = requestDigest ?? throw new ArgumentNullException(nameof(requestDigest));
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        StartGeneration = startGeneration ?? throw new ArgumentNullException(nameof(startGeneration));
        TerminalGeneration = terminalGeneration ?? throw new ArgumentNullException(nameof(terminalGeneration));
        TerminalSummary = terminalSummary ?? throw new ArgumentNullException(nameof(terminalSummary));
        RequestRef = requestRef ?? throw new ArgumentNullException(nameof(requestRef));
        ArtifactRefs = ContractArgumentGuard.RequireItems(artifactRefs, nameof(artifactRefs));
        Diagnostics = ContractArgumentGuard.RequireItems(diagnostics, nameof(diagnostics));

        if (requestRef.Digest != requestDigest
            || requestRef.Kind != GameViewRecordingArtifactKinds.Request
            || requestRef.MediaType != GameViewRecordingArtifactMediaTypes.Json)
        {
            throw new ArgumentException("Terminal record request reference must match the normalized recording request.", nameof(requestRef));
        }
    }

    public int SchemaVersion { get; }

    public ExecutionKind ExecutionKind { get; }

    public Guid RecordingId { get; }

    public Sha256Digest RequestDigest { get; }

    public UnityProjectIdentity Project { get; }

    public GameViewRecordingRuntimeIdentity Runtime { get; }

    public UnityEditorGenerationSnapshot StartGeneration { get; }

    public UnityEditorGenerationSnapshot TerminalGeneration { get; }

    public GameViewRecordingTerminalSummary TerminalSummary { get; }

    public ArtifactRef RequestRef { get; }

    public IReadOnlyList<ArtifactRef> ArtifactRefs { get; }

    public IReadOnlyList<GameViewRecordingDiagnostic> Diagnostics { get; }
}
