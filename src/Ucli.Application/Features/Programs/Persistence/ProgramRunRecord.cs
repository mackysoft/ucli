using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Contains the durable aggregate for one fixed Program definition and one execution attempt. </summary>
internal sealed record ProgramRunRecord
{
    public const int CurrentSchemaVersion = 1;

    public ProgramRunRecord (
        int schemaVersion,
        long version,
        Guid runId,
        Sha256Digest definitionDigest,
        ArtifactRef definitionSnapshotRef,
        UnityProjectIdentity project,
        ProgramRunFixedContext fixedContext,
        LifecycleExecutionHostRegistration host,
        UnityEditorGenerationSnapshot startedGeneration,
        UnityEditorGenerationSnapshot? currentEditorGeneration,
        DateTimeOffset deadlineUtc,
        DateTimeOffset startedAtUtc,
        DateTimeOffset updatedAtUtc,
        ProgramRunState state,
        int cursor,
        IReadOnlyList<ProgramRunStepRecord> steps,
        IReadOnlyList<ExecutionRef> childExecutionRefs,
        ProgramCancellationRecord cancellation,
        ArtifactRef? terminalRecordRef)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Unsupported Program Run record schema version.");
        }
        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Program Run version must not be negative.");
        }
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Program Run id must not be empty.", nameof(runId));
        }

        SchemaVersion = schemaVersion;
        Version = version;
        RunId = runId;
        DefinitionDigest = definitionDigest ?? throw new ArgumentNullException(nameof(definitionDigest));
        DefinitionSnapshotRef = definitionSnapshotRef ?? throw new ArgumentNullException(nameof(definitionSnapshotRef));
        if (DefinitionSnapshotRef.Kind.Value != "programDefinitionSnapshot"
            || DefinitionSnapshotRef.MediaType.Value != "application/json")
        {
            throw new ArgumentException("Program Run definition snapshot must be the fixed JSON definition artifact.", nameof(definitionSnapshotRef));
        }
        Project = project ?? throw new ArgumentNullException(nameof(project));
        FixedContext = (fixedContext ?? throw new ArgumentNullException(nameof(fixedContext))).Validate();
        Host = host ?? throw new ArgumentNullException(nameof(host));
        StartedGeneration = startedGeneration ?? throw new ArgumentNullException(nameof(startedGeneration));
        CurrentEditorGeneration = currentEditorGeneration;
        StartedAtUtc = RequireUtc(startedAtUtc, nameof(startedAtUtc));
        DeadlineUtc = RequireUtc(deadlineUtc, nameof(deadlineUtc));
        UpdatedAtUtc = RequireUtc(updatedAtUtc, nameof(updatedAtUtc));
        if (DeadlineUtc <= StartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(deadlineUtc), deadlineUtc, "Program Run deadline must follow its start.");
        }
        if (UpdatedAtUtc < StartedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAtUtc), updatedAtUtc, "Program Run update time must not precede its start.");
        }

        State = state;
        if (cursor < 0 || cursor > (steps?.Count ?? 0))
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), cursor, "Program Run cursor must address its fixed steps.");
        }

        Steps = steps?.ToArray() ?? throw new ArgumentNullException(nameof(steps));
        if (Steps.Count == 0)
        {
            throw new ArgumentException("Program Run must contain at least one fixed step.", nameof(steps));
        }
        Cursor = cursor;
        ChildExecutionRefs = childExecutionRefs?.ToArray() ?? throw new ArgumentNullException(nameof(childExecutionRefs));
        if (ChildExecutionRefs.Count != 0)
        {
            throw new ArgumentException("Program Run does not own durable child executions in this version.", nameof(childExecutionRefs));
        }
        Cancellation = (cancellation ?? throw new ArgumentNullException(nameof(cancellation))).Validate();
        TerminalRecordRef = terminalRecordRef;

        ValidateState();
    }

    public int SchemaVersion { get; }
    public long Version { get; }
    public Guid RunId { get; }
    public Sha256Digest DefinitionDigest { get; }
    public ArtifactRef DefinitionSnapshotRef { get; }
    public UnityProjectIdentity Project { get; }
    public ProgramRunFixedContext FixedContext { get; }
    public LifecycleExecutionHostRegistration Host { get; }
    public UnityEditorGenerationSnapshot StartedGeneration { get; }
    public UnityEditorGenerationSnapshot? CurrentEditorGeneration { get; }
    public DateTimeOffset DeadlineUtc { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
    public ProgramRunState State { get; }
    public int Cursor { get; }
    public IReadOnlyList<ProgramRunStepRecord> Steps { get; }
    public IReadOnlyList<ExecutionRef> ChildExecutionRefs { get; }
    public ProgramCancellationRecord Cancellation { get; }
    public ArtifactRef? TerminalRecordRef { get; }

    public Verdict? Verdict => ProgramRunStateSemantics.AggregateVerdict(State, Steps.Select(static step => step.Verdict));

    /// <summary> Gets the application state derived from every admitted Program Step. </summary>
    public ExecutionApplicationState ApplicationState => DeriveApplicationState(Steps);

    internal static ExecutionApplicationState DeriveApplicationState (IReadOnlyList<ProgramRunStepRecord> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var result = ExecutionApplicationState.NotApplied;
        foreach (var step in steps)
        {
            if (step.State == ProgramStepState.Deferred)
            {
                continue;
            }
            if (GetApplicationStatePrecedence(step.ApplicationState) > GetApplicationStatePrecedence(result))
            {
                result = step.ApplicationState;
            }
        }
        return result;
    }

    public ExecutionRef CreateExecutionReference (ExecutionStatusLocator statusLocator)
    {
        ArgumentNullException.ThrowIfNull(statusLocator);
        var kind = new ExecutionKind("programRun");
        var executionState = new ExecutionState(GetStateText(State));
        return ProgramRunStateSemantics.ToExecutionLifecycle(State) switch
        {
            ExecutionLifecycle.Active => new ActiveExecutionRef(kind, RunId, DefinitionDigest, executionState, statusLocator),
            ExecutionLifecycle.Recovery => new RecoveryExecutionRef(kind, RunId, DefinitionDigest, executionState, statusLocator),
            ExecutionLifecycle.Terminal => new TerminalExecutionRef(kind, RunId, DefinitionDigest, executionState, null,
                TerminalRecordRef ?? throw new InvalidOperationException("Terminal Program Run requires its terminal record.")),
            _ => throw new InvalidOperationException("Program Run state projection is not defined."),
        };
    }

    private void ValidateState ()
    {
        if (!TextVocabulary.IsDefined(State))
        {
            throw new ArgumentOutOfRangeException(nameof(State), State, "Program Run state must be defined.");
        }
        var terminal = ProgramRunStateSemantics.IsTerminal(State);
        if (terminal != (TerminalRecordRef is not null))
        {
            throw new ArgumentException("Program Run terminal state and terminal record must be established together.");
        }
        if (TerminalRecordRef is not null
            && (TerminalRecordRef.Kind.Value != "programRunTerminalRecord"
                || TerminalRecordRef.MediaType.Value != "application/json"))
        {
            throw new ArgumentException("Terminal Program Run must reference its JSON terminal record.");
        }
        if (terminal && Steps.Any(static step => ProgramRunStateSemantics.IsOngoing(step.State)))
        {
            throw new ArgumentException("A terminal Program Run cannot retain an ongoing Program Step.");
        }
        if (State == ProgramRunState.Completed
            && (Cursor != Steps.Count || Steps.Any(static step => step.State != ProgramStepState.Completed)))
        {
            throw new ArgumentException("Completed Program Run requires every fixed step to be completed and consumed.");
        }
        for (var index = 0; index < Steps.Count; index++)
        {
            if (index < Cursor && Steps[index].State != ProgramStepState.Completed)
            {
                throw new ArgumentException("Program Run cursor cannot skip a deferred, ongoing, or non-completed Step.");
            }
            if (index > Cursor && Steps[index].State != ProgramStepState.Deferred)
            {
                throw new ArgumentException("Program Run Steps after the current cursor must remain deferred.");
            }
        }
        foreach (var step in Steps)
        {
            step.Validate();
            if (step.ChildExecutionRef is not null)
            {
                throw new ArgumentException("Program Step does not own a durable child execution in this version.");
            }
            ValidateRequestBoundary(step);
        }
    }

    private void ValidateRequestBoundary (ProgramRunStepRecord step)
    {
        var boundary = step.RequestExecution;
        if (boundary is null)
        {
            return;
        }
        if (boundary.Project != Project || boundary.Host != Host || boundary.StartedGeneration != step.GenerationBefore
            || boundary.RequestPlanRef != step.RequestPlanRef
            || !boundary.OperationDescriptorRefs.SequenceEqual(step.OperationDescriptorRefs)
            || boundary.StartedAtUtc != step.StartedAtUtc || boundary.DeadlineUtc != step.DeadlineUtc
            || boundary.DeadlineUtc > DeadlineUtc)
        {
            throw new ArgumentException("Program Request boundary must retain the exact Run, Step generation, plan, descriptor, and deadline facts.");
        }
    }

    private static DateTimeOffset RequireUtc (DateTimeOffset value, string parameterName)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be a non-default UTC value.", parameterName);
        }
        return value;
    }

    private static string GetStateText (ProgramRunState state) => state switch
    {
        ProgramRunState.Created => "created",
        ProgramRunState.Running => "running",
        ProgramRunState.WaitingForRuntime => "waitingForRuntime",
        ProgramRunState.Cancelling => "cancelling",
        ProgramRunState.Completed => "completed",
        ProgramRunState.Failed => "failed",
        ProgramRunState.Cancelled => "cancelled",
        ProgramRunState.Interrupted => "interrupted",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Program Run state must be defined."),
    };

    private static int GetApplicationStatePrecedence (ExecutionApplicationState state) => state switch
    {
        ExecutionApplicationState.NotApplied => 0,
        ExecutionApplicationState.Applied => 1,
        ExecutionApplicationState.PartiallyApplied => 2,
        ExecutionApplicationState.Indeterminate => 3,
        ExecutionApplicationState.Unknown => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Program Step application state must be defined."),
    };
}

/// <summary> Represents the persisted cancellation request without performing cancellation. </summary>
internal sealed record ProgramCancellationRecord (bool Requested, DateTimeOffset? RequestedAtUtc, string? ReasonCode)
{
    public static ProgramCancellationRecord None { get; } = new(false, null, null);

    public ProgramCancellationRecord Request (DateTimeOffset requestedAtUtc, string? reasonCode)
    {
        if (Requested)
        {
            return this;
        }
        if (requestedAtUtc == default || requestedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Cancellation request time must be a non-default UTC value.", nameof(requestedAtUtc));
        }
        return new ProgramCancellationRecord(true, requestedAtUtc, reasonCode).Validate();
    }

    public ProgramCancellationRecord Validate ()
    {
        if (!Requested && (RequestedAtUtc is not null || ReasonCode is not null))
        {
            throw new ArgumentException("Unrequested Program cancellation must not contain a time or reason.");
        }
        if (Requested && (RequestedAtUtc is null || RequestedAtUtc == default || RequestedAtUtc.Value.Offset != TimeSpan.Zero))
        {
            throw new ArgumentException("Requested Program cancellation requires a non-default UTC time.");
        }
        return this;
    }
}
