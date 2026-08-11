using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Recording;

/// <summary>Represents one public recording command payload branch.</summary>
public abstract record GameViewRecordingPayload;

/// <summary>Represents one diagnostic attached to a recording execution.</summary>
public sealed record GameViewRecordingDiagnostic
{
    [JsonConstructor]
    public GameViewRecordingDiagnostic (
        UcliCode code,
        GameViewRecordingDiagnosticSeverity severity,
        string message,
        IReadOnlyList<ArtifactRef> artifactRefs)
    {
        if (!TextVocabulary.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Recording diagnostic severity must be defined.");
        }

        Code = code ?? throw new ArgumentNullException(nameof(code));
        Severity = severity;
        Message = ContractArgumentGuard.RequireValue(message, nameof(message));
        ArtifactRefs = ContractArgumentGuard.RequireItems(artifactRefs, nameof(artifactRefs));
    }

    public UcliCode Code { get; }

    public GameViewRecordingDiagnosticSeverity Severity { get; }

    public string Message { get; }

    public IReadOnlyList<ArtifactRef> ArtifactRefs { get; }
}

/// <summary>Represents a recording execution payload shared by start, status, and stop.</summary>
public abstract record GameViewRecordingExecutionPayload : GameViewRecordingPayload
{
    protected GameViewRecordingExecutionPayload (
        UnityProjectIdentity project,
        ExecutionRef executionRef,
        Sha256Digest requestDigest,
        ArtifactRef requestRef,
        GameViewRecordingProgress progress,
        IReadOnlyList<ArtifactRef> artifactRefs,
        IReadOnlyList<GameViewRecordingDiagnostic> diagnostics,
        ExecutionLifecycle requiredLifecycle)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        _ = executionRef ?? throw new ArgumentNullException(nameof(executionRef));
        RequestDigest = requestDigest ?? throw new ArgumentNullException(nameof(requestDigest));
        RequestRef = requestRef ?? throw new ArgumentNullException(nameof(requestRef));
        _ = progress ?? throw new ArgumentNullException(nameof(progress));
        ArtifactRefs = ContractArgumentGuard.RequireItems(artifactRefs, nameof(artifactRefs));
        Diagnostics = ContractArgumentGuard.RequireItems(diagnostics, nameof(diagnostics));

        if (executionRef.Kind != GameViewRecordingExecutionContract.Kind)
        {
            throw new ArgumentException("Execution reference kind must identify GameView recording.", nameof(executionRef));
        }
        if (executionRef.Lifecycle != requiredLifecycle
            || GameViewRecordingExecutionContract.GetLifecycle(progress.State) != requiredLifecycle)
        {
            throw new ArgumentException("Execution reference, payload branch, and recording state must map to the same lifecycle.", nameof(executionRef));
        }
        if (executionRef.State != GameViewRecordingExecutionContract.ToExecutionState(progress.State))
        {
            throw new ArgumentException("Execution reference state must match recording progress state.", nameof(executionRef));
        }
        if (executionRef.DefinitionDigest != requestDigest || requestRef.Digest != requestDigest)
        {
            throw new ArgumentException("Execution definition, request digest, and request artifact digest must match.", nameof(requestDigest));
        }
        if (requestRef.Kind != GameViewRecordingArtifactKinds.Request
            || requestRef.MediaType != GameViewRecordingArtifactMediaTypes.Json)
        {
            throw new ArgumentException("Request reference must identify the normalized recording-request JSON artifact.", nameof(requestRef));
        }
        if (requiredLifecycle != ExecutionLifecycle.Terminal && executionRef.StatusLocator is null)
        {
            throw new ArgumentException("A non-terminal recording reference must carry a status locator.", nameof(executionRef));
        }
        if (requiredLifecycle == ExecutionLifecycle.Terminal
            && executionRef is not TerminalExecutionRef)
        {
            throw new ArgumentException("A terminal recording payload requires a terminal execution reference.", nameof(executionRef));
        }
        if (executionRef is TerminalExecutionRef terminal
            && (terminal.TerminalRecordRef.Kind != GameViewRecordingArtifactKinds.TerminalRecord
                || terminal.TerminalRecordRef.MediaType != GameViewRecordingArtifactMediaTypes.Json))
        {
            throw new ArgumentException("Terminal execution reference must identify the recording terminal record.", nameof(executionRef));
        }
    }

    public UnityProjectIdentity Project { get; }

    [JsonIgnore]
    public abstract ExecutionRef ExecutionReference { get; }

    public Sha256Digest RequestDigest { get; }

    public ArtifactRef RequestRef { get; }

    [JsonIgnore]
    public abstract GameViewRecordingProgress RecordingProgress { get; }

    [JsonIgnore]
    public GameViewRecordingState State => RecordingProgress.State;

    [JsonIgnore]
    public ExecutionLifecycle Lifecycle =>
        GameViewRecordingExecutionContract.GetLifecycle(State);

    [JsonIgnore]
    public bool IsTerminal => Lifecycle == ExecutionLifecycle.Terminal;

    public IReadOnlyList<ArtifactRef> ArtifactRefs { get; }

    public IReadOnlyList<GameViewRecordingDiagnostic> Diagnostics { get; }

    public bool TryGetTerminal (
        [NotNullWhen(true)]
        out GameViewRecordingTerminalPayload? terminalPayload)
    {
        terminalPayload = this as GameViewRecordingTerminalPayload;
        return terminalPayload is not null;
    }

}

/// <summary>Represents a recording that can continue normal forward progress.</summary>
public sealed record GameViewRecordingActivePayload : GameViewRecordingExecutionPayload
{
    [JsonConstructor]
    public GameViewRecordingActivePayload (
        UnityProjectIdentity project,
        ActiveExecutionRef executionRef,
        Sha256Digest requestDigest,
        ArtifactRef requestRef,
        GameViewRecordingActiveProgress progress,
        IReadOnlyList<ArtifactRef> artifactRefs,
        IReadOnlyList<GameViewRecordingDiagnostic> diagnostics)
        : base(
            project,
            executionRef,
            requestDigest,
            requestRef,
            progress,
            artifactRefs,
            diagnostics,
            ExecutionLifecycle.Active)
    {
        ExecutionRef = executionRef;
        Progress = progress;
    }

    [JsonConverter(typeof(ActiveExecutionRefBranchJsonConverter))]
    public ActiveExecutionRef ExecutionRef { get; }

    public GameViewRecordingActiveProgress Progress { get; }

    [JsonIgnore]
    public override ExecutionRef ExecutionReference => ExecutionRef;

    [JsonIgnore]
    public override GameViewRecordingProgress RecordingProgress => Progress;
}

/// <summary>Represents a recovery or terminal payload returned by a successful recording stop command.</summary>
public abstract record GameViewRecordingStopResultPayload : GameViewRecordingExecutionPayload
{
    protected GameViewRecordingStopResultPayload (
        UnityProjectIdentity project,
        ExecutionRef executionRef,
        Sha256Digest requestDigest,
        ArtifactRef requestRef,
        GameViewRecordingProgress progress,
        IReadOnlyList<ArtifactRef> artifactRefs,
        IReadOnlyList<GameViewRecordingDiagnostic> diagnostics,
        ExecutionLifecycle requiredLifecycle)
        : base(
            project,
            executionRef,
            requestDigest,
            requestRef,
            progress,
            artifactRefs,
            diagnostics,
            requiredLifecycle)
    {
    }
}

/// <summary>Represents a recording being stopped, finalized, or recovered.</summary>
public sealed record GameViewRecordingRecoveryPayload :
    GameViewRecordingStopResultPayload
{
    [JsonConstructor]
    public GameViewRecordingRecoveryPayload (
        UnityProjectIdentity project,
        RecoveryExecutionRef executionRef,
        Sha256Digest requestDigest,
        ArtifactRef requestRef,
        GameViewRecordingRecoveryProgress progress,
        IReadOnlyList<ArtifactRef> artifactRefs,
        IReadOnlyList<GameViewRecordingDiagnostic> diagnostics)
        : base(
            project,
            executionRef,
            requestDigest,
            requestRef,
            progress,
            artifactRefs,
            diagnostics,
            ExecutionLifecycle.Recovery)
    {
        ExecutionRef = executionRef;
        Progress = progress;
    }

    [JsonConverter(typeof(RecoveryExecutionRefBranchJsonConverter))]
    public RecoveryExecutionRef ExecutionRef { get; }

    public GameViewRecordingRecoveryProgress Progress { get; }

    [JsonIgnore]
    public override ExecutionRef ExecutionReference => ExecutionRef;

    [JsonIgnore]
    public override GameViewRecordingProgress RecordingProgress => Progress;
}

/// <summary>Represents a finalized recording and its terminal summary.</summary>
public sealed record GameViewRecordingTerminalPayload :
    GameViewRecordingStopResultPayload
{
    [JsonConstructor]
    public GameViewRecordingTerminalPayload (
        UnityProjectIdentity project,
        TerminalExecutionRef executionRef,
        Sha256Digest requestDigest,
        ArtifactRef requestRef,
        GameViewRecordingTerminalProgress progress,
        IReadOnlyList<ArtifactRef> artifactRefs,
        IReadOnlyList<GameViewRecordingDiagnostic> diagnostics,
        GameViewRecordingTerminalSummary terminalSummary)
        : base(
            project,
            executionRef,
            requestDigest,
            requestRef,
            progress,
            artifactRefs,
            diagnostics,
            ExecutionLifecycle.Terminal)
    {
        ExecutionRef = executionRef;
        Progress = progress;
        TerminalSummary = terminalSummary ?? throw new ArgumentNullException(nameof(terminalSummary));
        if (terminalSummary.State != progress.State)
        {
            throw new ArgumentException("Terminal summary and progress must identify the same terminal state.", nameof(terminalSummary));
        }
    }

    [JsonConverter(typeof(TerminalExecutionRefBranchJsonConverter))]
    public TerminalExecutionRef ExecutionRef { get; }

    public GameViewRecordingTerminalProgress Progress { get; }

    [JsonIgnore]
    public override ExecutionRef ExecutionReference => ExecutionRef;

    [JsonIgnore]
    public override GameViewRecordingProgress RecordingProgress => Progress;

    public GameViewRecordingTerminalSummary TerminalSummary { get; }
}

/// <summary>Represents the recording selection returned by <c>recording status</c>.</summary>
public abstract record GameViewRecordingSelection;

/// <summary>Indicates that the status lookup completed without selecting a recording.</summary>
public sealed record NoGameViewRecordingSelection : GameViewRecordingSelection;

/// <summary>Contains the recording selected by an explicit identifier or the active environment selection.</summary>
public sealed record SelectedGameViewRecordingSelection : GameViewRecordingSelection
{
    [JsonConstructor]
    public SelectedGameViewRecordingSelection (GameViewRecordingExecutionPayload recording)
    {
        Recording = recording ?? throw new ArgumentNullException(nameof(recording));
    }

    public GameViewRecordingExecutionPayload Recording { get; }
}

/// <summary>Represents the successful public payload of <c>recording status</c>.</summary>
public sealed record GameViewRecordingStatusPayload : GameViewRecordingPayload
{
    [JsonConstructor]
    public GameViewRecordingStatusPayload (
        UnityProjectIdentity project,
        GameViewRecordingCapability capability,
        GameViewRecordingSelection recordingSelection)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        RecordingSelection = recordingSelection ?? throw new ArgumentNullException(nameof(recordingSelection));
    }

    public UnityProjectIdentity Project { get; }

    public GameViewRecordingCapability Capability { get; }

    public GameViewRecordingSelection RecordingSelection { get; }
}
