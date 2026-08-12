using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Holds one Program Step's durable start, reference, and terminal facts. </summary>
internal sealed record ProgramRunStepRecord (
    string Command,
    int TimeoutMilliseconds,
    ProgramStepState State,
    Verdict? Verdict,
    DateTimeOffset? PlanningStartedAtUtc,
    DateTimeOffset? DeadlineUtc,
    UnityEditorGenerationSnapshot? GenerationBefore,
    UnityEditorGenerationSnapshot? GenerationAfter,
    ExecutionApplicationState ApplicationState,
    ArtifactRef? RequestPlanRef,
    IReadOnlyList<ArtifactRef> OperationDescriptorRefs,
    ExecutionRef? LifecycleExecutionRef,
    ProgramRequestExecutionBoundary? RequestExecution,
    ExecutionRef? ChildExecutionRef,
    ArtifactRef? ResultRef,
    ArtifactRef? StepResultRef,
    IReadOnlyList<ArtifactRef> ArtifactRefs,
    string? ErrorCode,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc)
{
    /// <summary>
    /// Gets the durable logical execution identity allocated before the closed
    /// execution port is invoked. This is intentionally independent from IPC
    /// transport identifiers and from any future child execution representation.
    /// </summary>
    public ProgramStepExecutionReference? Execution { get; init; }

    /// <summary> Gets whether the closed execution port was admitted after this Step was durably planned. </summary>
    public bool ExecutionPortInvoked { get; init; }

    public ProgramRunStepRecord Validate ()
    {
        if (!TextVocabulary.IsDefined(State)
            || !TextVocabulary.IsDefined(ApplicationState)
            || (Verdict.HasValue && !TextVocabulary.IsDefined(Verdict.Value)))
        {
            throw new ArgumentOutOfRangeException("Program Step stores only defined state, application state, and verdict values.");
        }
        if (string.IsNullOrWhiteSpace(Command) || TimeoutMilliseconds < 1)
        {
            throw new ArgumentException("Program Step command and timeout must be fixed before persistence.");
        }
        if (OperationDescriptorRefs is null || ArtifactRefs is null)
        {
            throw new ArgumentNullException(nameof(OperationDescriptorRefs));
        }
        if (ChildExecutionRef is not null)
        {
            throw new ArgumentException("Program Step must not own a durable child execution in this version.");
        }
        if (State == ProgramStepState.Deferred
            && (Verdict is not null || ApplicationState != ExecutionApplicationState.NotApplied
                || ResultRef is not null || StepResultRef is not null || ArtifactRefs.Count != 0 || ErrorCode is not null || StartedAtUtc is not null || CompletedAtUtc is not null
                || Execution is not null || ExecutionPortInvoked))
        {
            throw new ArgumentException("Deferred Program Step must not contain execution or result facts.");
        }
        ValidateExecutionReferenceContract();
        if (ProgramRunStateSemantics.IsTerminal(State))
        {
            if (CompletedAtUtc is null || ResultRef is null)
            {
                throw new ArgumentException("Terminal Program Step requires a fixed terminal record reference and completion time.");
            }
            if (ResultRef.Kind.Value != "programStepTerminalRecord"
                || ResultRef.MediaType.Value != "application/json")
            {
                throw new ArgumentException("Terminal Program Step must reference its JSON terminal record.");
            }
        }
        else if (ResultRef is not null)
        {
            throw new ArgumentException("A nonterminal Program Step must not retain a terminal record reference.");
        }
        else if (State != ProgramStepState.Deferred && StartedAtUtc is null && PlanningStartedAtUtc is null)
        {
            throw new ArgumentException("An admitted Program Step requires its durable planning or start record.");
        }
        if (StartedAtUtc is not null
            && !ProgramRunStateSemantics.IsTerminal(State)
            && LifecycleExecutionRef is null
            && RequestExecution is null
            && Execution is null)
        {
            throw new ArgumentException("A started Program Step must retain its recoverable execution reference.");
        }
        RequestExecution?.Validate();
        Execution?.Validate();
        if (Execution is not null && (StartedAtUtc is null || Execution.StartedAtUtc != StartedAtUtc || Execution.DeadlineUtc != DeadlineUtc))
        {
            throw new ArgumentException("Program Step execution identity must match its durable start and deadline facts.");
        }
        if (ExecutionPortInvoked && Execution is null)
        {
            throw new ArgumentException("A Program Step can record an execution port invocation only with its durable execution identity.");
        }
        return this;
    }

    private void ValidateExecutionReferenceContract ()
    {
        if (Command != "call" && (RequestPlanRef is not null || OperationDescriptorRefs.Count != 0))
        {
            throw new ArgumentException("Only a Program call Step may retain request plan or operation descriptor references.");
        }
        if (State == ProgramStepState.Deferred)
        {
            if (RequestExecution is not null && Command != "call")
            {
                throw new ArgumentException("Only a Program call Step may retain a Request execution boundary.");
            }
            if (LifecycleExecutionRef is not null && !TryGetLifecycleKind(Command, out _))
            {
                throw new ArgumentException("Only a lifecycle Program Step may retain a Lifecycle Execution reference.");
            }
            return;
        }
        if (Command == "call")
        {
            if (LifecycleExecutionRef is not null)
            {
                throw new ArgumentException("A Program call Step must not retain a Lifecycle Execution reference.");
            }
            return;
        }
        if (TryGetLifecycleKind(Command, out var expectedKind))
        {
            if (RequestExecution is not null)
            {
                throw new ArgumentException("A lifecycle Program Step must not retain a Request execution boundary.");
            }
            if (LifecycleExecutionRef is not null
                && (!TextVocabulary.TryGetValue(LifecycleExecutionRef.Kind.Value, out LifecycleExecutionKind actualKind)
                    || actualKind != expectedKind))
            {
                throw new ArgumentException("A lifecycle Program Step must retain the matching Lifecycle Execution kind.");
            }
            if (StepResultRef is not null || ArtifactRefs.Count != 0)
            {
                throw new ArgumentException("A lifecycle Program Step must not retain synchronous result artifacts.");
            }
            return;
        }
        if (LifecycleExecutionRef is not null || RequestExecution is not null)
        {
            throw new ArgumentException("A synchronous Program Step must not retain an execution reference.");
        }
    }

    private static bool TryGetLifecycleKind (string command, out LifecycleExecutionKind kind)
    {
        switch (command)
        {
            case "refresh":
                kind = LifecycleExecutionKind.Refresh;
                return true;
            case "compile":
                kind = LifecycleExecutionKind.Compile;
                return true;
            case "play.enter":
                kind = LifecycleExecutionKind.PlayEnter;
                return true;
            case "play.exit":
                kind = LifecycleExecutionKind.PlayExit;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}

/// <summary> Identifies one started Program Step before any execution port can create a side effect. </summary>
internal sealed record ProgramStepExecutionReference (Guid ExecutionId, DateTimeOffset StartedAtUtc, DateTimeOffset DeadlineUtc)
{
    public ProgramStepExecutionReference Validate ()
    {
        if (ExecutionId == Guid.Empty || StartedAtUtc == default || StartedAtUtc.Offset != TimeSpan.Zero
            || DeadlineUtc == default || DeadlineUtc.Offset != TimeSpan.Zero || DeadlineUtc <= StartedAtUtc)
        {
            throw new ArgumentException("Program Step execution reference requires a non-empty identity and ordered UTC audit facts.");
        }
        return this;
    }
}

/// <summary> Separates a same-generation Request execution from lifecycle and transport identities. </summary>
internal sealed record ProgramRequestExecutionBoundary (
    Guid ExecutionId,
    UnityProjectIdentity Project,
    LifecycleExecutionHostRegistration Host,
    UnityEditorGenerationSnapshot StartedGeneration,
    ArtifactRef RequestPlanRef,
    IReadOnlyList<ArtifactRef> OperationDescriptorRefs,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset DeadlineUtc)
{
    public ProgramRequestExecutionBoundary Validate ()
    {
        if (ExecutionId == Guid.Empty || Project is null || Host is null || StartedGeneration is null || RequestPlanRef is null || OperationDescriptorRefs is null)
        {
            throw new ArgumentException("Request boundary requires its logical id, fixed host context, plan, and descriptors.");
        }
        if (StartedAtUtc == default || StartedAtUtc.Offset != TimeSpan.Zero || DeadlineUtc == default || DeadlineUtc.Offset != TimeSpan.Zero || DeadlineUtc <= StartedAtUtc)
        {
            throw new ArgumentException("Request boundary requires an ordered UTC start and deadline.");
        }
        return this;
    }
}
