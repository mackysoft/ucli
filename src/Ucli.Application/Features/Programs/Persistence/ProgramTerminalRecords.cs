using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
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
    /// <summary>
    /// Gets the final attached-supervisor connection and availability snapshot.
    /// Process liveness, when observed, remains a separate fact.
    /// </summary>
    public ProgramAttachedSupervisorSnapshot FinalSupervisorSnapshot { get; init; } = null!;

    /// <summary> Gets the final attached-Supervisor liveness observation known when the Run became terminal. </summary>
    public ProgramProcessLivenessObservation? FinalSupervisorObservation { get; init; }
    /// <summary> Gets the final fixed-host liveness observation known when the Run became terminal. </summary>
    public ProgramProcessLivenessObservation? FinalHostObservation { get; init; }
    /// <summary> Gets the stable reason that selected this Run terminalization, when one is recorded. </summary>
    public string? ReasonCode { get; init; }
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
            || FinalSupervisorSnapshot is null || (ReasonCode is not null && string.IsNullOrWhiteSpace(ReasonCode))
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
        FinalSupervisorSnapshot.Validate();
        FinalSupervisorObservation?.Validate();
        FinalHostObservation?.Validate();
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

/// <summary>
/// Defines the closed, externally readable representation of a Program Step
/// terminal artifact. Durable run records deliberately retain additional
/// recovery facts; none of those facts belong to the immutable public artifact.
/// </summary>
internal sealed record ProgramStepTerminalArtifact (
    Guid RunId,
    Sha256Digest DefinitionDigest,
    string Command,
    ProgramStepState State,
    Verdict? Verdict,
    ExecutionApplicationState ApplicationState,
    UnityEditorGenerationSnapshot? GenerationBefore,
    UnityEditorGenerationSnapshot? GenerationAfter,
    ArtifactRef? RequestPlanRef,
    IReadOnlyList<ArtifactRef> OperationDescriptorRefs,
    ExecutionRef? LifecycleExecutionRef,
    ExecutionRef? ChildExecutionRef,
    JsonElement? StepResult,
    IReadOnlyList<ArtifactRef> ArtifactRefs,
    string? ErrorCode,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset CompletedAtUtc)
{
    public ProgramStepTerminalArtifact Validate ()
    {
        if (RunId == Guid.Empty || DefinitionDigest is null || string.IsNullOrWhiteSpace(Command)
            || !ProgramRunStateSemantics.IsTerminal(State)
            || (Verdict.HasValue && !TextVocabulary.IsDefined(Verdict.Value))
            || !TextVocabulary.IsDefined(ApplicationState)
            || OperationDescriptorRefs is null || ArtifactRefs is null
            || ChildExecutionRef is not null
            || (StepResult.HasValue && StepResult.Value.ValueKind != JsonValueKind.Object)
            || (StartedAtUtc.HasValue && (StartedAtUtc.Value == default || StartedAtUtc.Value.Offset != TimeSpan.Zero))
            || CompletedAtUtc == default || CompletedAtUtc.Offset != TimeSpan.Zero
            || (StartedAtUtc.HasValue && StartedAtUtc > CompletedAtUtc))
        {
            throw new ArgumentException("Program Step terminal artifact must contain only its closed public contract.");
        }
        if (LifecycleExecutionRef is not null && StepResult is not null)
        {
            throw new ArgumentException("Lifecycle Program Steps must not duplicate their terminal result.");
        }
        return this;
    }
}

/// <summary> Defines the closed, externally readable representation of a Program Run terminal artifact. </summary>
internal sealed record ProgramRunTerminalArtifact (
    UnityProjectIdentity Project,
    Guid RunId,
    Sha256Digest DefinitionDigest,
    ArtifactRef DefinitionSnapshotRef,
    ProgramEffectiveAuthorizationSnapshot Authorization,
    ProgramEffectiveConfigurationSnapshot Configuration,
    DateTimeOffset DeadlineUtc,
    ProgramDefinitionSnapshotManifest SourceManifest,
    ProgramRunState State,
    Verdict? Verdict,
    ExecutionApplicationState ApplicationState,
    IReadOnlyList<ProgramRunTerminalStepArtifact> Steps,
    IReadOnlyList<ExecutionRef> ChildExecutionRefs,
    ProgramAttachedSupervisorSnapshot Supervisor,
    UnityEditorGenerationSnapshot? CurrentEditorGeneration,
    ProgramCancellationRecord Cancellation,
    ProgramRunTerminalSummary Terminal,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc)
{
    public ProgramRunTerminalArtifact Validate ()
    {
        if (Project is null || RunId == Guid.Empty || DefinitionDigest is null || DefinitionSnapshotRef is null
            || Authorization is null || Configuration is null || SourceManifest is null || !ProgramRunStateSemantics.IsTerminal(State)
            || (Verdict.HasValue && !TextVocabulary.IsDefined(Verdict.Value)) || !TextVocabulary.IsDefined(ApplicationState)
            || Steps is null || ChildExecutionRefs is null || ChildExecutionRefs.Count != 0 || Supervisor is null || Cancellation is null || Terminal is null
            || DeadlineUtc == default || DeadlineUtc.Offset != TimeSpan.Zero || StartedAtUtc == default || StartedAtUtc.Offset != TimeSpan.Zero
            || CompletedAtUtc == default || CompletedAtUtc.Offset != TimeSpan.Zero || CompletedAtUtc < StartedAtUtc)
        {
            throw new ArgumentException("Program Run terminal artifact must contain only its closed public contract.");
        }
        Authorization.Validate();
        Configuration.Validate();
        Supervisor.Validate();
        Cancellation.Validate();
        Terminal.Validate(State, Verdict, ApplicationState, Steps, CompletedAtUtc);
        foreach (var step in Steps)
        {
            step.Validate();
        }
        return this;
    }
}

/// <summary> Projects durable step status without exposing its execution boundary or persistence-only result reference. </summary>
internal sealed record ProgramRunTerminalStepArtifact (
    string Command,
    int TimeoutMilliseconds,
    ProgramStepState State,
    Verdict? Verdict,
    DateTimeOffset? PlanningStartedAtUtc,
    DateTimeOffset? StepDeadlineAtUtc,
    UnityEditorGenerationSnapshot? GenerationBefore,
    UnityEditorGenerationSnapshot? GenerationAfter,
    ExecutionApplicationState ApplicationState,
    ArtifactRef? RequestPlanRef,
    IReadOnlyList<ArtifactRef> OperationDescriptorRefs,
    ExecutionRef? LifecycleExecutionRef,
    ExecutionRef? ChildExecutionRef,
    ArtifactRef? ResultRef,
    string? ErrorCode,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc)
{
    public ProgramRunTerminalStepArtifact Validate ()
    {
        if (string.IsNullOrWhiteSpace(Command) || TimeoutMilliseconds < 1 || !TextVocabulary.IsDefined(State)
            || (Verdict.HasValue && !TextVocabulary.IsDefined(Verdict.Value)) || !TextVocabulary.IsDefined(ApplicationState)
            || OperationDescriptorRefs is null || ChildExecutionRef is not null)
        {
            throw new ArgumentException("Program Run terminal Step must contain only its closed public status contract.");
        }
        return this;
    }
}

/// <summary> Captures the terminal fields without a self-reference or status locator. </summary>
internal sealed record ProgramRunTerminalSummary (
    ProgramRunState State,
    Verdict? Verdict,
    string? ReasonCode,
    ExecutionApplicationState ApplicationState,
    int CompletedStepCount,
    int UnstartedStepCount,
    DateTimeOffset CompletedAtUtc)
{
    public void Validate (ProgramRunState runState, Verdict? runVerdict, ExecutionApplicationState applicationState,
        IReadOnlyList<ProgramRunTerminalStepArtifact> steps, DateTimeOffset completedAtUtc)
    {
        if (State != runState || Verdict != runVerdict || ApplicationState != applicationState || CompletedAtUtc != completedAtUtc
            || CompletedStepCount != steps.Count(static step => step.State == ProgramStepState.Completed)
            || UnstartedStepCount != steps.Count(static step => step.StartedAtUtc is null))
        {
            throw new ArgumentException("Program Run terminal summary must agree with the terminal aggregate.");
        }
    }
}
