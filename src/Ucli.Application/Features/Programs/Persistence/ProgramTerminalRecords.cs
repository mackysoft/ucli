using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Captures the immutable facts of one terminal Program Step. </summary>
internal sealed record ProgramStepTerminalRecord (
    int SchemaVersion,
    Guid RunId,
    Sha256Digest DefinitionDigest,
    int StepIndex,
    string Command,
    ProgramStepState State,
    Verdict? Verdict,
    ExecutionApplicationState ApplicationState,
    UnityEditorGenerationSnapshot? GenerationBefore,
    UnityEditorGenerationSnapshot? GenerationAfter,
    ArtifactRef? RequestPlanRef,
    IReadOnlyList<ArtifactRef> OperationDescriptorRefs,
    ExecutionRef? LifecycleExecutionRef,
    ArtifactRef? StepResultRef,
    IReadOnlyList<ArtifactRef> ArtifactRefs,
    string? ErrorCode,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset CompletedAtUtc)
{
    public const int CurrentSchemaVersion = 1;

    public ProgramStepTerminalRecord Validate ()
    {
        if (SchemaVersion != CurrentSchemaVersion || RunId == Guid.Empty || DefinitionDigest is null || StepIndex < 0 || string.IsNullOrWhiteSpace(Command)
            || !ProgramRunStateSemantics.IsTerminal(State)
            || (Verdict.HasValue && !TextVocabulary.IsDefined(Verdict.Value)) || !TextVocabulary.IsDefined(ApplicationState)
            || OperationDescriptorRefs is null || ArtifactRefs is null
            || (StartedAtUtc.HasValue && (StartedAtUtc.Value == default || StartedAtUtc.Value.Offset != TimeSpan.Zero))
            || CompletedAtUtc == default || CompletedAtUtc.Offset != TimeSpan.Zero
            || (StartedAtUtc.HasValue && StartedAtUtc.Value > CompletedAtUtc))
        {
            throw new ArgumentException("Program Step Terminal Record must contain only fixed terminal facts.");
        }
        return this;
    }
}

/// <summary> Captures the immutable facts of one terminal Program Run without status-location or self references. </summary>
internal sealed record ProgramRunTerminalRecord (
    int SchemaVersion,
    UnityProjectIdentity Project,
    Guid RunId,
    Sha256Digest DefinitionDigest,
    ArtifactRef DefinitionSnapshotRef,
    DateTimeOffset DeadlineUtc,
    ProgramDefinitionSnapshotManifest SourceManifest,
    ProgramRunFixedContext FixedContext,
    ProgramRunState State,
    Verdict? Verdict,
    ExecutionApplicationState ApplicationState,
    IReadOnlyList<ProgramRunStepRecord> Steps,
    IReadOnlyList<ExecutionRef> ChildExecutionRefs,
    ProgramCancellationRecord Cancellation,
    UnityEditorGenerationSnapshot? CurrentEditorGeneration,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc)
{
    public const int CurrentSchemaVersion = 1;

    public ProgramRunTerminalRecord Validate ()
    {
        if (SchemaVersion != CurrentSchemaVersion || Project is null || RunId == Guid.Empty || DefinitionDigest is null || DefinitionSnapshotRef is null || SourceManifest is null || FixedContext is null
            || !ProgramRunStateSemantics.IsTerminal(State)
            || (Verdict.HasValue && !TextVocabulary.IsDefined(Verdict.Value)) || !TextVocabulary.IsDefined(ApplicationState)
            || Steps is null || ChildExecutionRefs is null || ChildExecutionRefs.Count != 0 || Cancellation is null
            || StartedAtUtc == default || StartedAtUtc.Offset != TimeSpan.Zero
            || DeadlineUtc == default || DeadlineUtc.Offset != TimeSpan.Zero || DeadlineUtc <= StartedAtUtc
            || CompletedAtUtc == default || CompletedAtUtc.Offset != TimeSpan.Zero || CompletedAtUtc < StartedAtUtc
            || Steps.Any(static step => ProgramRunStateSemantics.IsOngoing(step.State)))
        {
            throw new ArgumentException("Program Run Terminal Record must contain one closed terminal aggregate.");
        }
        if (DefinitionSnapshotRef.Kind.Value != "programDefinitionSnapshot"
            || DefinitionSnapshotRef.MediaType.Value != "application/json")
        {
            throw new ArgumentException("Program Run Terminal Record must retain the fixed JSON definition snapshot.");
        }
        FixedContext.Validate();
        Cancellation.Validate();
        foreach (var step in Steps)
        {
            step.Validate();
        }
        if (ApplicationState != ProgramRunRecord.DeriveApplicationState(Steps))
        {
            throw new ArgumentException("Program Run Terminal Record application state must be derived from its Steps.");
        }
        return this;
    }
}
