using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Infrastructure.Execution.ReadPostcondition;

/// <summary> Classifies a mutation read-postcondition journal failure. </summary>
public enum MutationReadPostconditionJournalFailureKind
{
    /// <summary> The persisted journal is malformed or uses an unsupported schema. </summary>
    InvalidDocument = 1,

    /// <summary> The journal could not be read, locked, or atomically written. </summary>
    Storage = 2,
}

/// <summary> Describes a journal failure that must prevent admission or index use. </summary>
public sealed record MutationReadPostconditionJournalFailure (
    MutationReadPostconditionJournalFailureKind Kind,
    string Message);

/// <summary> Represents one journal read outcome. </summary>
public sealed record MutationReadPostconditionJournalReadResult (
    ExecutionReadPostcondition? ReadPostcondition,
    MutationReadPostconditionJournalFailure? Failure)
{
    /// <summary> Gets whether the journal read completed successfully. </summary>
    public bool IsSuccess => Failure is null;

    /// <summary> Creates a successful journal read outcome. </summary>
    public static MutationReadPostconditionJournalReadResult Success (ExecutionReadPostcondition? readPostcondition) => new(readPostcondition, null);

    /// <summary> Creates a failed journal read outcome. </summary>
    public static MutationReadPostconditionJournalReadResult Failed (MutationReadPostconditionJournalFailure failure) => new(null, failure ?? throw new ArgumentNullException(nameof(failure)));
}

/// <summary> Represents one journal merge outcome. </summary>
public sealed record MutationReadPostconditionJournalWriteResult (MutationReadPostconditionJournalFailure? Failure)
{
    /// <summary> Gets whether the merge completed successfully. </summary>
    public bool IsSuccess => Failure is null;

    /// <summary> Creates a successful journal merge outcome. </summary>
    public static MutationReadPostconditionJournalWriteResult Success () => new((MutationReadPostconditionJournalFailure?)null);

    /// <summary> Creates a failed journal merge outcome. </summary>
    public static MutationReadPostconditionJournalWriteResult Failed (MutationReadPostconditionJournalFailure failure) => new(failure ?? throw new ArgumentNullException(nameof(failure)));
}

/// <summary> Represents one durable eval-call admission outcome. </summary>
public sealed record EvalCallAdmissionResult (
    bool IsAdmitted,
    bool IsReplay,
    ExecutionReadPostcondition? ReadPostcondition,
    MutationReadPostconditionJournalFailure? Failure)
{
    /// <summary> Creates a successful admission. </summary>
    public static EvalCallAdmissionResult Admitted (ExecutionReadPostcondition readPostcondition) => new(true, false, readPostcondition ?? throw new ArgumentNullException(nameof(readPostcondition)), null);

    /// <summary> Creates a rejection for a previously consumed token binding. </summary>
    public static EvalCallAdmissionResult Replay () => new(false, true, null, null);

    /// <summary> Creates a fail-closed storage or document outcome. </summary>
    public static EvalCallAdmissionResult Failed (MutationReadPostconditionJournalFailure failure) => new(false, false, null, failure ?? throw new ArgumentNullException(nameof(failure)));
}
