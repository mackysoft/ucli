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

/// <summary> Classifies the durable journal admission and timeout-publication outcome for one eval call. </summary>
public enum EvalCallAdmissionOutcome
{
    /// <summary> The journal did not durably consume the eval-call binding. </summary>
    Rejected = 0,

    /// <summary> The journal durably consumed the binding and its timeout fallback was synchronously published. </summary>
    AdmittedAndPublished = 1,

    /// <summary> The journal durably consumed the binding but timeout fallback publication threw. </summary>
    DurablyAdmittedPublicationFailed = 2,
}

/// <summary> Represents one durable eval-call admission and synchronous timeout-publication outcome. </summary>
public sealed record EvalCallAdmissionResult (
    EvalCallAdmissionOutcome Outcome,
    bool IsReplay,
    ExecutionReadPostcondition? ReadPostcondition,
    MutationReadPostconditionJournalFailure? Failure,
    Exception? PublicationException)
{
    /// <summary> Gets whether the durable admission and timeout fallback publication both succeeded. </summary>
    public bool IsAdmitted => Outcome == EvalCallAdmissionOutcome.AdmittedAndPublished;

    /// <summary> Gets whether the journal durably consumed the eval-call binding. </summary>
    public bool IsDurablyAdmitted => Outcome is EvalCallAdmissionOutcome.AdmittedAndPublished
        or EvalCallAdmissionOutcome.DurablyAdmittedPublicationFailed;

    /// <summary> Creates a successful durable admission with its fallback synchronously published. </summary>
    public static EvalCallAdmissionResult AdmittedAndPublished (ExecutionReadPostcondition readPostcondition) => new(
        EvalCallAdmissionOutcome.AdmittedAndPublished,
        false,
        readPostcondition ?? throw new ArgumentNullException(nameof(readPostcondition)),
        null,
        null);

    /// <summary> Creates a durable admission whose synchronous fallback publication threw. </summary>
    public static EvalCallAdmissionResult DurablyAdmittedPublicationFailed (
        ExecutionReadPostcondition readPostcondition,
        Exception exception) => new(
        EvalCallAdmissionOutcome.DurablyAdmittedPublicationFailed,
        false,
        readPostcondition ?? throw new ArgumentNullException(nameof(readPostcondition)),
        null,
        exception ?? throw new ArgumentNullException(nameof(exception)));

    /// <summary> Creates a rejection for a previously consumed token binding. </summary>
    public static EvalCallAdmissionResult Replay () => new(EvalCallAdmissionOutcome.Rejected, true, null, null, null);

    /// <summary> Creates a fail-closed storage or document outcome. </summary>
    public static EvalCallAdmissionResult Failed (MutationReadPostconditionJournalFailure failure) => new(
        EvalCallAdmissionOutcome.Rejected,
        false,
        null,
        failure ?? throw new ArgumentNullException(nameof(failure)),
        null);
}
