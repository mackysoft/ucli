using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;

namespace MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

/// <summary> Identifies the result of registering one Lifecycle Execution start record. </summary>
internal enum LifecycleExecutionStartOutcome
{
    Registered = 1,
    Reconnected,
    InvalidDefinition,
    DefinitionConflict,
    ProjectMismatch,
    HostMismatch,
}

/// <summary> Carries the authoritative start binding when registration or reconnection succeeds. </summary>
internal sealed record LifecycleExecutionStartResult (
    LifecycleExecutionStartOutcome Outcome,
    LifecycleExecutionStartBinding? Binding)
{
    public bool IsSuccess =>
        Outcome is LifecycleExecutionStartOutcome.Registered
            or LifecycleExecutionStartOutcome.Reconnected;
}

/// <summary> Identifies the result of a compare-and-swap execution-reference update. </summary>
internal enum LifecycleExecutionReferenceUpdateOutcome
{
    Updated = 1,
    Missing,
    Conflict,
    AlreadyTerminal,
}

/// <summary>
/// Identifies the durable result of an action-owned request to enter Lifecycle Execution recovery.
/// </summary>
internal enum LifecycleExecutionRecoveryTransitionOutcome
{
    Entered = 1,
    AlreadyRecovering,
    SideEffectAdmissionRequired,
    TerminalOrPublishing,
    Missing,
}

/// <summary>
/// Identifies whether one caller acquired the durable right to issue an action-specific side effect.
/// </summary>
internal enum LifecycleExecutionSideEffectRightOutcome
{
    Acquired = 1,
    Contended,
    TerminalOrPublishing,
    Missing,
}

/// <summary>
/// Carries the authoritative durable execution observed while resolving one side-effect right.
/// </summary>
internal sealed record LifecycleExecutionSideEffectRightResult (
    LifecycleExecutionSideEffectRightOutcome Outcome,
    StoredLifecycleExecution? AuthoritativeExecution);

/// <summary> Identifies the result of accepting a successor endpoint registration. </summary>
internal enum LifecycleExecutionEndpointAdvanceOutcome
{
    Advanced = 1,
    AlreadyCurrent,
    Missing,
    AlreadyTerminal,
    TerminalPublicationFixed,
    ProjectMismatch,
    HostMismatch,
    GenerationMismatch,
    RecoveryLeaseExpired,
}

/// <summary> Identifies the result of publishing and reverifying an immutable terminal record. </summary>
internal enum LifecycleExecutionTerminalPublicationOutcome
{
    Published = 1,
    Reconnected,
    PublicationFailed,
    Missing,
    Conflict,
    NotPublishing,
}

/// <summary>
/// Carries either the reverified terminal reference or the fixed record, reconnectable
/// publishing reference, publication failure, or authoritative execution observed on a
/// conditional-publication conflict.
/// </summary>
internal sealed record LifecycleExecutionTerminalPublicationResult (
    LifecycleExecutionTerminalPublicationOutcome Outcome,
    TerminalExecutionRef? TerminalReference,
    LifecycleExecutionTerminalRecord? TerminalRecord,
    ExecutionRef? ReconnectableReference = null,
    Exception? Failure = null,
    StoredLifecycleExecution? AuthoritativeExecution = null)
{
    public bool IsSuccess =>
        Outcome is LifecycleExecutionTerminalPublicationOutcome.Published
            or LifecycleExecutionTerminalPublicationOutcome.Reconnected;
}

/// <summary> Identifies one persisted Lifecycle Execution entry before its record is read. </summary>
internal readonly record struct LifecycleExecutionStoreEntry (
    LifecycleExecutionKind Kind,
    Guid ExecutionId);

/// <summary> Carries the durable current projection of one Lifecycle Execution. </summary>
internal sealed record StoredLifecycleExecution (
    LifecycleExecutionStartBinding Start,
    TerminalExecutionRef? TerminalReference,
    Guid? SideEffectRightOwnerEndpointRegistrationGenerationId)
{
    public ExecutionRef CurrentReference => TerminalReference ?? Start.LifecycleExecutionRef;

    public bool IsTerminal => TerminalReference is not null;

    public bool IsPublishing =>
        !IsTerminal
        && CurrentReference.Lifecycle == ExecutionLifecycle.Recovery
        && string.Equals(
            CurrentReference.State.Value,
            TextVocabulary.GetText(LifecycleExecutionState.Publishing),
            StringComparison.Ordinal);
}
